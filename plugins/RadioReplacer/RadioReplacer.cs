using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BigCityLegacy.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// ============================================================
//  Radio Replacer v3.0 — by Mr_Dodep
//  Портировано на LegacyUIFramework
//  Открыть/закрыть: Alt + M
//  Папки треков: BepInEx/plugins/RadioReplacer/tracks_1 .. tracks_5
//  Привязки сохраняются автоматически
// ============================================================

[BepInPlugin("com.mrdodep.radioreplacer", "Radio Replacer", "3.0.0")]
[BepInDependency("com.bigcitylegacy.baseplugin", BepInDependency.DependencyFlags.HardDependency)]
public class RadioReplacerPlugin : BaseUnityPlugin
{
    public static ManualLogSource Log;
    public static ConfigFile Cfg;

    public static ConfigEntry<int> BindRock;
    public static ConfigEntry<int> BindTechno;
    public static ConfigEntry<int> BindBlue;
    public static ConfigEntry<int> BindDubStep;
    public static ConfigEntry<int> BindHipHop;
    public static ConfigEntry<int> BindElectro;

    private void Awake()
    {
        Log = Logger;
        Cfg = Config;

        LegacyInput.Init();

        BindRock = Config.Bind("Bindings", "Rock", 0, "Папка для Rock (0=нет, 1-5=папка)");
        BindTechno = Config.Bind("Bindings", "Techno", 0, "Папка для Techno");
        BindBlue = Config.Bind("Bindings", "Blue", 0, "Папка для Blue");
        BindDubStep = Config.Bind("Bindings", "DubStep", 0, "Папка для DubStep");
        BindHipHop = Config.Bind("Bindings", "HipHop", 0, "Папка для HipHop");
        BindElectro = Config.Bind("Bindings", "Electro", 0, "Папка для Electro");

        Logger.LogInfo("Radio Replacer v3.0 загружен! Alt + M для открытия.");

        var go = new GameObject("RadioReplacerGui");
        DontDestroyOnLoad(go);
        go.AddComponent<RadioReplacerMenu>();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

public class RadioReplacerMenu : MonoBehaviour
{
    // ── Состояние ────────────────────────────────────────────────────────────
    private RadioStation[] stations = new RadioStation[0];
    private Dictionary<string, int> stationBindings = new Dictionary<string, int>();
    private Dictionary<string, string> stationStatus = new Dictionary<string, string>();

    private string pluginFolder;
    private const int FOLDER_COUNT = 5;
    private readonly int[] trackCounts = new int[FOLDER_COUNT + 1];
    private readonly string[] folderLabels = new string[FOLDER_COUNT + 1];
    private readonly string[] bindingLabels = new string[FOLDER_COUNT + 1];
    private readonly Dictionary<string, string> stationLabels = new Dictionary<string, string>();
    private string[] stationNames = new string[0];
    private Action<Rect> drawContent;
    private const string FOLDER_HINT = "Папки: ...\\BepInEx\\plugins\\RadioReplacer\\tracks_1 .. tracks_5";

    private bool isLoading = false;
    private string globalStatus = "";

    // ── LegacyUI ─────────────────────────────────────────────────────────────
    private LegacyUIWindow _window;

    // Темы для цветных кнопок
    private LegacyUITheme _themeGreen;
    private LegacyUITheme _themeRed;
    private LegacyUITheme _themeBlue;

    // ── Reflection: RadioStation ─────────────────────────────────────────────
    private FieldInfo fi_musics;
    private FieldInfo fi_voices;
    private FieldInfo fi_efir;
    private FieldInfo fi_fullLen;

    // ── Reflection: RadioStation.Item ────────────────────────────────────────
    private FieldInfo fi_item_clip;
    private FieldInfo fi_item_clipName;
    private FieldInfo fi_item_clip_len;
    private FieldInfo fi_item_randomInt;

    // ── Reflection: Radio ────────────────────────────────────────────────────
    private FieldInfo fi_radio_me;
    private MethodInfo mi_radio_update;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        pluginFolder = Path.GetDirectoryName(typeof(RadioReplacerPlugin).Assembly.Location);

        for (int i = 1; i <= FOLDER_COUNT; i++)
        {
            string dir = Path.Combine(pluginFolder, $"tracks_{i}");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        // Reflection: RadioStation
        var rsType = typeof(RadioStation);
        fi_musics = rsType.GetField("musics", BindingFlags.Public | BindingFlags.Instance);
        fi_voices = rsType.GetField("voices", BindingFlags.Public | BindingFlags.Instance);
        fi_efir = rsType.GetField("efir", BindingFlags.NonPublic | BindingFlags.Instance);
        fi_fullLen = rsType.GetField("fullLen", BindingFlags.NonPublic | BindingFlags.Instance);

        // Reflection: RadioStation.Item
        var itemType = typeof(RadioStation.Item);
        fi_item_clip = itemType.GetField("clip", BindingFlags.Public | BindingFlags.Instance);
        fi_item_clipName = itemType.GetField("clipName", BindingFlags.NonPublic | BindingFlags.Instance);
        fi_item_clip_len = itemType.GetField("clip_len", BindingFlags.NonPublic | BindingFlags.Instance);
        fi_item_randomInt = itemType.GetField("randomInt", BindingFlags.NonPublic | BindingFlags.Instance);

        // Reflection: Radio
        var radioType = typeof(Radio);
        fi_radio_me = radioType.GetField("me", BindingFlags.NonPublic | BindingFlags.Static);
        mi_radio_update = radioType.GetMethod("UpdateCurRadioStationPosTime",
                            BindingFlags.NonPublic | BindingFlags.Instance);

        LoadBindingsFromConfig();
        RefreshFolderCounts();
        drawContent = DrawContent;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // ── Создаём LegacyUIWindow ───────────────────────────────────────────
        int sw = Screen.width, sh = Screen.height;

        _window = new LegacyUIWindow(
            "Radio Replacer (wav/ogg)",
            new Rect(sw / 2 - 300, sh / 2 - 280, 600, 560),
            new LegacyUIWindowOptions
            {
                Draggable = true,
                ClampToScreen = true,
                ShowCloseButton = true,
                FadeWhileDragging = true,
                DragAlpha = 0.6f,
                BackgroundAlpha = 0.95f,
                RightHint = "[Alt + M]",
                InputBlockMode = LegacyUIInputBlockMode.Window,
                PlayerPrefsKey = "RadioReplacer.Window",
            });
        _window.Closed += () => { };   // закрытие через × уже сохраняет Visible
        _window.Visible = false;

        // Темы
        _themeGreen = MakeTheme(
            new Color(0.12f, 0.38f, 0.20f, 1f),
            new Color(0.16f, 0.48f, 0.26f, 1f));
        _themeRed = MakeTheme(
            new Color(0.50f, 0.18f, 0.18f, 1f),
            new Color(0.60f, 0.22f, 0.22f, 1f));
        _themeBlue = MakeTheme(
            new Color(0.14f, 0.35f, 0.65f, 1f),
            new Color(0.18f, 0.44f, 0.80f, 1f));
    }

    private LegacyUITheme MakeTheme(Color btn, Color btnHover)
    {
        var p = new LegacyUIPalette();
        p.Button = btn;
        p.ButtonHover = btnHover;
        return new LegacyUITheme(p);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ── Загрузка сцены ────────────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RadioReplacerPlugin.Log.LogInfo($"Сцена загружена: {scene.name} — применяю привязки...");
        StartCoroutine(AutoApplyAfterDelay());
    }

    private IEnumerator AutoApplyAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        FindStations();
        int applied = 0;
        foreach (var st in stations)
        {
            if (stationBindings.TryGetValue(st.name, out int fn) && fn > 0)
            {
                yield return StartCoroutine(LoadAndApply(st, fn, silent: true));
                applied++;
            }
        }
        if (applied > 0)
        {
            globalStatus = $"Автозагрузка: применено {applied} привязок";
            RadioReplacerPlugin.Log.LogInfo(globalStatus);
        }
    }

    // ── Конфиг ────────────────────────────────────────────────────────────────
    private void LoadBindingsFromConfig()
    {
        stationBindings["Rock"] = RadioReplacerPlugin.BindRock.Value;
        stationBindings["Techno"] = RadioReplacerPlugin.BindTechno.Value;
        stationBindings["Blue"] = RadioReplacerPlugin.BindBlue.Value;
        stationBindings["DubStep"] = RadioReplacerPlugin.BindDubStep.Value;
        stationBindings["HipHop"] = RadioReplacerPlugin.BindHipHop.Value;
        stationBindings["Electro"] = RadioReplacerPlugin.BindElectro.Value;
    }

    private void SaveBindingsToConfig()
    {
        RadioReplacerPlugin.BindRock.Value = GetBinding("Rock");
        RadioReplacerPlugin.BindTechno.Value = GetBinding("Techno");
        RadioReplacerPlugin.BindBlue.Value = GetBinding("Blue");
        RadioReplacerPlugin.BindDubStep.Value = GetBinding("DubStep");
        RadioReplacerPlugin.BindHipHop.Value = GetBinding("HipHop");
        RadioReplacerPlugin.BindElectro.Value = GetBinding("Electro");
        RadioReplacerPlugin.Cfg.Save();
        RefreshStationLabels();
    }

    private int GetBinding(string n) => stationBindings.TryGetValue(n, out int v) ? v : 0;
    private string GetFolderPath(int fn) => Path.Combine(pluginFolder, $"tracks_{fn}");
    // UI reads cached values only. Disk access happens on explicit refresh/load.
    private int GetTrackCount(int fn) => fn >= 1 && fn <= FOLDER_COUNT ? trackCounts[fn] : 0;

    private List<string> ReadTrackFiles(int fn)
    {
        var files = new List<string>();
        if (fn < 1 || fn > FOLDER_COUNT) return files;
        string directory = GetFolderPath(fn);
        try
        {
            if (Directory.Exists(directory))
            {
                files.AddRange(Directory.EnumerateFiles(directory, "*.wav"));
                files.AddRange(Directory.EnumerateFiles(directory, "*.ogg"));
            }
        }
        catch (IOException ex)
        {
            files.Clear();
            RadioReplacerPlugin.Log.LogWarning($"Не удалось прочитать tracks_{fn}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            files.Clear();
            RadioReplacerPlugin.Log.LogWarning($"Нет доступа к tracks_{fn}: {ex.Message}");
        }

        if (folderLabels[fn] == null || trackCounts[fn] != files.Count)
        {
            trackCounts[fn] = files.Count;
            folderLabels[fn] = $"{fn}\n({files.Count})";
            bindingLabels[fn] = $"tracks_{fn} ({files.Count} тр.)";
        }
        return files;
    }

    private void RefreshFolderCounts()
    {
        for (int fn = 1; fn <= FOLDER_COUNT; fn++) ReadTrackFiles(fn);
        RefreshStationLabels();
    }

    private void FindStations()
    {
        stations = FindObjectsOfType<RadioStation>();
        stationNames = new string[stations.Length];
        for (int i = 0; i < stations.Length; i++) stationNames[i] = stations[i].name;
        RefreshStationLabels();
    }

    private void RefreshStationLabels()
    {
        stationLabels.Clear();
        foreach (string name in stationNames)
        {
            int binding = GetBinding(name);
            stationLabels[name] = stationStatus.TryGetValue(name, out string applied)
                ? "✓ " + applied
                : binding >= 1 && binding <= FOLDER_COUNT ? bindingLabels[binding] : "— нет —";
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if ((LegacyInput.GetKey("left alt") || LegacyInput.GetKey("right alt")) && LegacyInput.GetKeyDown("m"))
        {
            _window.Visible = !_window.Visible;
            if (_window.Visible) RefreshStations();
            _window.SavePrefs();
        }
    }

    private void RefreshStations()
    {
        RefreshFolderCounts();
        FindStations();
        globalStatus = $"Найдено станций: {stations.Length}";
    }

    // ── Применить треки ───────────────────────────────────────────────────────
    private void ApplyToStation(RadioStation station, int folderNum)
    {
        if (isLoading) { globalStatus = "Загрузка уже идёт..."; return; }
        StartCoroutine(LoadAndApply(station, folderNum, silent: false));
    }

    private IEnumerator LoadAndApply(RadioStation station, int folderNum, bool silent)
    {
        isLoading = true;
        var trackFiles = ReadTrackFiles(folderNum);
        RefreshStationLabels();
        if (trackFiles.Count == 0)
        {
            globalStatus = $"Папка tracks_{folderNum} пуста или недоступна!";
            isLoading = false;
            yield break;
        }

        if (!silent) globalStatus = $"Загружаю tracks_{folderNum}...";

        var clips = new List<AudioClip>();
        foreach (string path in trackFiles)
        {
            string ext = Path.GetExtension(path).ToLower();
            AudioType at = ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV;
            string uri = "file:///" + path.Replace("\\", "/");

            using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, at))
            {
                yield return req.SendWebRequest();
                if (!req.isNetworkError && !req.isHttpError)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    clips.Add(clip);
                }
                else
                    RadioReplacerPlugin.Log.LogWarning($"Ошибка: {Path.GetFileName(path)} — {req.error}");
            }
        }

        if (clips.Count == 0)
        {
            stationStatus[station.name] = "Ошибка загрузки";
            RefreshStationLabels();
            globalStatus = $"Не удалось загрузить треки из tracks_{folderNum}!";
            isLoading = false;
            yield break;
        }

        var itemType = typeof(RadioStation.Item);
        Array newMusics = Array.CreateInstance(itemType, clips.Count);
        for (int i = 0; i < clips.Count; i++)
        {
            object item = Activator.CreateInstance(itemType);
            fi_item_clip.SetValue(item, clips[i]);
            if (fi_item_clipName != null) fi_item_clipName.SetValue(item, clips[i].name);
            if (fi_item_clip_len != null) fi_item_clip_len.SetValue(item, clips[i].length);
            newMusics.SetValue(item, i);
        }

        if (fi_musics != null) fi_musics.SetValue(station, newMusics);
        if (fi_voices != null) fi_voices.SetValue(station, Array.CreateInstance(itemType, 0));
        RebuildEfir(station, newMusics);

        if (fi_radio_me != null && mi_radio_update != null)
        {
            object radioMe = fi_radio_me.GetValue(null);
            if (radioMe != null) mi_radio_update.Invoke(radioMe, null);
        }

        stationStatus[station.name] = $"tracks_{folderNum} ({clips.Count} тр.)";
        RefreshStationLabels();
        if (!silent) globalStatus = $"✓ [{station.name}] ← tracks_{folderNum} ({clips.Count} треков)";
        isLoading = false;
    }

    private void RebuildEfir(RadioStation station, Array items)
    {
        if (fi_efir == null || fi_fullLen == null) return;
        var itemType = typeof(RadioStation.Item);
        var listType = typeof(List<>).MakeGenericType(itemType);
        object efir = Activator.CreateInstance(listType);
        var addMethod = listType.GetMethod("Add");

        float totalLen = 0f;
        int[] randomOrder = BuildRandomOrder(items.Length, new System.Random());

        for (int i = 0; i < items.Length; i++)
        {
            object item = items.GetValue(i);
            if (fi_item_randomInt != null) fi_item_randomInt.SetValue(item, randomOrder[i]);
            float len = fi_item_clip_len != null ? (float)fi_item_clip_len.GetValue(item) : 0f;
            totalLen += len;
            addMethod.Invoke(efir, new object[] { item });
        }

        fi_efir.SetValue(station, efir);
        fi_fullLen.SetValue(station, totalLen);
    }

    // Unique shuffle keys in bounded time, including playlists over 100 tracks.
    private static int[] BuildRandomOrder(int count, System.Random rng)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }
        return order;
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        if (_window == null) return;
        if (!_window.Visible)
        {
            // Preserve the framework's input-blocker cleanup without setting up GUI state.
            _window.Draw(null);
            return;
        }
        using (new LegacyUIGuiScope())
        {
            _window.Draw(drawContent);
        }
    }

    private void DrawContent(Rect content)
    {
        LegacyUILayout ui = new LegacyUILayout(content, 6f);

        // Статус
        LegacyUI.Status(ui.Row(18f), string.IsNullOrEmpty(globalStatus) ? " " : globalStatus);
        LegacyUI.Separator(ui.Row(1f));
        ui.Space(2f);

        // Заголовки колонок
        DrawColumnHeaders(ui.Row(18f));
        LegacyUI.Separator(ui.Row(1f));
        ui.Space(2f);

        // Статичный список: все станции отображаются без прокрутки.
        float listHeight = Mathf.Max(0f, stations.Length * 48f - 4f);
        Rect listRect = ui.Row(listHeight);
        DrawStationList(listRect);

        ui.Space(4f);
        LegacyUI.Separator(ui.Row(1f));
        ui.Space(4f);

        // Нижние кнопки
        Rect bottomRow = ui.Row(28f);
        float halfW = (bottomRow.width - 8f) / 2f;

        if (LegacyUI.Button(new Rect(bottomRow.x, bottomRow.y, halfW, 28f), "↺  Обновить станции"))
            RefreshStations();

        if (!isLoading)
        {
            using (LegacyUI.WithTheme(_themeBlue))
                if (LegacyUI.Button(new Rect(bottomRow.x + halfW + 8f, bottomRow.y, halfW, 28f), "▶  Применить все"))
                    StartCoroutine(ApplyAllBindings());
        }
        else
        {
            LegacyUI.MiniHint(new Rect(bottomRow.x + halfW + 8f, bottomRow.y, halfW, 28f), "Загрузка...");
        }

        ui.Space(4f);
        LegacyUI.MiniHint(ui.Row(14f), FOLDER_HINT);
    }

    // ── Заголовки колонок ─────────────────────────────────────────────────────
    private void DrawColumnHeaders(Rect row)
    {
        float c1 = row.width * 0.28f;
        float c2 = row.width * 0.35f;
        // c3 = остаток — кнопки папок + кнопка сброса

        LegacyUI.MiniHint(new Rect(row.x, row.y, c1, row.height), "Станция");
        LegacyUI.MiniHint(new Rect(row.x + c1, row.y, c2, row.height), "Привязка / статус");
        LegacyUI.MiniHint(new Rect(row.x + c1 + c2, row.y, row.width - c1 - c2, row.height), "Папка  1–5");
    }

    // ── Список станций ────────────────────────────────────────────────────────
    private void DrawStationList(Rect area)
    {
        const float ROW_H = 44f;
        const float SPACING = 4f;

        float contentW = area.width;

        float c1 = contentW * 0.28f;
        float c2 = contentW * 0.35f;
        float c3 = contentW - c1 - c2;

        for (int i = 0; i < stations.Length; i++)
        {
            float ry = area.y + i * (ROW_H + SPACING);

            RadioStation st = stations[i];
            if (st == null) continue;
            string stationName = stationNames[i];

            // Фон строки
            LegacyUI.Panel(new Rect(area.x, ry, contentW, ROW_H));

            // Название станции
            LegacyUI.Label(new Rect(area.x + 6f, ry + 12f, c1 - 8f, 20f), stationName);

            // Текущий статус привязки
            int curBind = GetBinding(stationName);
            stationLabels.TryGetValue(stationName, out string bindLabel);
            LegacyUI.MiniHint(new Rect(area.x + c1, ry + 12f, c2 - 6f, 20f), bindLabel);

            // Кнопки папок 1–5
            float btnW = (c3 - 34f) / FOLDER_COUNT;
            for (int f = 1; f <= FOLDER_COUNT; f++)
            {
                float bx = area.x + c1 + c2 + (f - 1) * (btnW + 2f);
                string lbl = folderLabels[f];
                Rect br = new Rect(bx, ry + 4f, btnW, ROW_H - 8f);

                if (curBind == f)
                {
                    using (LegacyUI.WithTheme(_themeGreen))
                        if (LegacyUI.Button(br, lbl))
                        {
                            stationBindings[st.name] = f;
                            SaveBindingsToConfig();
                            ApplyToStation(st, f);
                        }
                }
                else
                {
                    if (LegacyUI.Button(br, lbl))
                    {
                        stationBindings[st.name] = f;
                        SaveBindingsToConfig();
                        ApplyToStation(st, f);
                    }
                }
            }

            // Кнопка сброса ×
            if (curBind != 0)
            {
                float xbx = area.x + c1 + c2 + c3 - 32f;
                using (LegacyUI.WithTheme(_themeRed))
                {
                    if (LegacyUI.Button(new Rect(xbx, ry + 8f, 28f, 28f), "✕"))
                    {
                        stationBindings[st.name] = 0;
                        stationStatus.Remove(st.name);
                        SaveBindingsToConfig();
                        globalStatus = $"Привязка [{st.name}] сброшена";
                    }
                }
            }
        }
    }

    // ── Применить все ─────────────────────────────────────────────────────────
    private IEnumerator ApplyAllBindings()
    {
        if (isLoading) yield break;
        RefreshFolderCounts();
        int applied = 0;
        foreach (var st in stations)
        {
            int fn = GetBinding(st.name);
            if (fn > 0 && GetTrackCount(fn) > 0)
            {
                yield return StartCoroutine(LoadAndApply(st, fn, silent: false));
                applied++;
            }
        }
        globalStatus = applied > 0
            ? $"✓ Применено {applied} привязок"
            : "Нет активных привязок";
    }
}