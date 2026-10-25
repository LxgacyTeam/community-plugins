using BepInEx;
using BigCityLegacy.UI;
using UnityEngine;
using System.Reflection;
using HarmonyLib;

// ============================================================
//  Cinematic Camera Plus v5.0
//  Все эффекты через OnGUI / GUI.DrawTexture
//  Наклон камеры, 5 новых фильтров
//  Alt + C — открыть/закрыть
// ============================================================

[BepInPlugin("com.mrdodep.cinematiccameraplus", "Cinematic Camera Plus", "5.0.0")]
[BepInDependency("com.bigcitylegacy.baseplugin", BepInDependency.DependencyFlags.HardDependency)]
public class CinematicCameraPlus : BaseUnityPlugin
{
    // ── Камера ───────────────────────────────────────────────
    private float _fov = 60f;
    private float _defaultFov = 60f;
    private bool _fovFound = false;

    // ── Цветокоррекция ───────────────────────────────────────
    public float Brightness = 0f;    // -1..+1
    public float Contrast = 1f;    // 0.5..2
    public float Saturation = 1f;    // 0..2  (0=ч/б, 1=норм, 2=насыщ)
    public float Gamma = 1f;    // 0.5..2

    // ── Эффекты ──────────────────────────────────────────────
    public bool Vignette = false;
    public float VignetteInt = 0.5f;
    public bool ChromAb = false;
    public float ChromAbInt = 0.03f;
    public bool Scanlines = false;
    public float ScanlinesInt = 0.3f;
    public bool FilmGrain = false;
    public float FilmGrainInt = 0.08f;
    public bool NightVision = false;
    public bool Letterbox = false;
    public float LetterboxH = 0.105f;

    // ── Новые фильтры ────────────────────────────────────────
    public bool Sepia = false;   // Сепия
    public float SepiaInt = 0.8f;
    public bool Infrared = false;   // Инфракрасный
    public bool FogFilter = false;   // Туман/дымка
    public float FogInt = 0.4f;
    public bool OldTV = false;   // Старое ТВ (засветка + помехи)
    public bool Sunset = false;   // Закат (тёплый градиент снизу)
    public float SunsetInt = 0.5f;

    // ── Наклон камеры ────────────────────────────────────────
    public float CameraRoll = 0f;      // -45..+45 градусов
    private float _defaultRoll = 0f;
    //private Quaternion _baseRotation;

    // ── UI ───────────────────────────────────────────────────
    //private bool _menuOpen = false;
    private int _tab = 0;
    private LegacyUIWindow _window;
    private LegacyUIScaleSettings _uiScale;

    // ── Камера ───────────────────────────────────────────────
    private Camera _gameCamera;
    private float _camRetryTimer = 0f;
    private float _dumpTimer = 0f;
    public string CameraStatus = "Ищу...";

    // ── Текстуры ─────────────────────────────────────────────
    private Texture2D _white;      // 1x1 белая
    private Texture2D _scanTex;    // 1x4 для сканлайнов
    private Texture2D _vignTex;    // 64x64 радиальная виньетка

    // ── Пресеты ──────────────────────────────────────────────
    private int _activePreset = -1;
    private static readonly string[] PresetNames = {
        "Cinematic","Noir","Sunset","Cold Night","Vintage","Horror","Vivid","Default"
    };

    // ── Harmony ──────────────────────────────────────────────
    private Harmony _harmony;

    // ═══════════════════════════════════════════════════════════
    //  Awake
    // ═══════════════════════════════════════════════════════════
    void Awake()
    {
        Logger.LogInfo("Cinematic Camera Plus v5.0  Alt+C=открыть");
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LegacyInput.Init();

        _uiScale = new LegacyUIScaleSettings { Mode = LegacyUIScaleMode.Automatic, BaseScale = 1f };
        _window = new LegacyUIWindow(
            "Cinematic Camera Plus",
            new Rect(60f, 40f, 520f, 680f),
            new LegacyUIWindowOptions
            {
                ShowCloseButton = true,
                RightHint = "[Alt + C]",
                FadeWhileDragging = true,
                DragAlpha = 0.55f,
                BackgroundAlpha = 0.85f,
                InputBlockMode = LegacyUIInputBlockMode.Window,
                PlayerPrefsKey = "CinematicCameraPlus.Window"
            });
        _window.Visible = false;

        // Регистрация в OnGuiGlobal
        var gui = gameObject.AddComponent<CinematicGUI>();
        gui.plugin = this;
        var fi2 = typeof(OnGuiGlobal).GetField("items",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi2 != null)
        {
            var list = fi2.GetValue(null);
            list?.GetType().GetMethod("Add")?.Invoke(list, new object[] { gui });
        }

        BuildTextures();

        // Патч на сеттер Camera.fieldOfView — перехватываем любую запись FOV
        _harmony = new Harmony("com.mods.cinematiccameraplus");
        _harmony.PatchAll(typeof(CinematicCameraPlus).Assembly);
        Logger.LogInfo("CCP: Harmony PatchAll применён");
    }

    // ── Текстуры ─────────────────────────────────────────────
    void BuildTextures()
    {
        // Белая 1x1
        _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _white.SetPixel(0, 0, Color.white);
        _white.Apply();

        // Сканлайны 1x4
        _scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        _scanTex.SetPixel(0, 0, new Color(0, 0, 0, 0));
        _scanTex.SetPixel(0, 1, new Color(0, 0, 0, 0));
        _scanTex.SetPixel(0, 2, new Color(0, 0, 0, 0.8f));
        _scanTex.SetPixel(0, 3, new Color(0, 0, 0, 0.8f));
        _scanTex.filterMode = FilterMode.Point;
        _scanTex.wrapMode = TextureWrapMode.Repeat;
        _scanTex.Apply();

        // Виньетка — 64x64 радиальный градиент
        int sz = 64;
        _vignTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float dx = (x / (float)(sz - 1)) * 2f - 1f;
                float dy = (y / (float)(sz - 1)) * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy); // 0=центр, ~1.41=угол
                float a = Mathf.Clamp01((d - 0.4f) / 0.8f); // темнеет с r=0.4
                _vignTex.SetPixel(x, y, new Color(0, 0, 0, a));
            }
        _vignTex.filterMode = FilterMode.Bilinear;
        _vignTex.Apply();
    }

    // ── Поиск камеры ─────────────────────────────────────────
    Camera FindGameCamera()
    {
        if (Camera.main != null) return Camera.main;
        foreach (var cam in Camera.allCameras)
        {
            if (!cam.enabled || cam.orthographic) continue;
            if ((cam.cullingMask & ~(1 << 5)) == 0) continue;
            return cam;
        }
        string[] names ={"Camera","Main Camera","PlayerCamera","ChaseCamera",
                         "GameCamera","ChaseCam","FollowCamera","ThirdPersonCamera"};
        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null) { var c = go.GetComponent<Camera>(); if (c != null && !c.orthographic) return c; }
        }
        Camera best = null; float bd = float.MaxValue;
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            if (!cam.enabled || cam.orthographic) continue;
            if (cam.depth < bd) { bd = cam.depth; best = cam; }
        }
        return best;
    }

    // ── Update ───────────────────────────────────────────────
    void Update()
    {
        if ((LegacyInput.GetKey("left alt") || LegacyInput.GetKey("right alt")) && LegacyInput.GetKeyDown("c"))
        {
            _window.Visible = !_window.Visible;
            _window.SavePrefs();
        }

        // Поиск камеры раз в секунду
        _camRetryTimer += Time.deltaTime;
        if (_camRetryTimer >= 1f)
        {
            _camRetryTimer = 0f;

            // Дамп ВСЕХ камер в лог каждые 5 секунд
            _dumpTimer += 1f;
            if (_dumpTimer <= 3f)
            {
                Logger.LogInfo("CCP === CAMERA DUMP ===");
                Logger.LogInfo("Camera.main = " + (Camera.main != null ? Camera.main.name : "NULL"));
                foreach (var cam in Camera.allCameras)
                    Logger.LogInfo("  allCam: " + cam.name + " tag=" + cam.tag + " depth=" + cam.depth
                        + " ortho=" + cam.orthographic + " enabled=" + cam.enabled
                        + " fov=" + cam.fieldOfView + " mask=" + cam.cullingMask);
                var all = FindObjectsOfType<Camera>();
                Logger.LogInfo("  FindObjectsOfType count=" + all.Length);
                foreach (var cam in all)
                    Logger.LogInfo("  found: " + cam.name + " active=" + cam.gameObject.activeInHierarchy
                        + " enabled=" + cam.enabled + " fov=" + cam.fieldOfView);
            }

            if (_gameCamera == null || !_gameCamera.isActiveAndEnabled)
            {
                var cam = FindGameCamera();
                if (cam != null)
                {
                    _gameCamera = cam;
                    CameraStatus = "✓ " + cam.name;
                    Logger.LogInfo("CCP: камера выбрана — " + cam.name + " tag=" + cam.tag + " fov=" + cam.fieldOfView);
                }
                else CameraStatus = "не найдена";
            }
        }

        // Запоминаем дефолтный FOV при первом обнаружении камеры
        if (_gameCamera != null && !_fovFound)
        {
            _defaultFov = _gameCamera.fieldOfView;
            _fovFound = true;
            _fov = _defaultFov;
            _defaultRoll = _gameCamera.transform.localEulerAngles.z;
            Logger.LogInfo("CCP: defaultFov=" + _defaultFov + " cam=" + _gameCamera.name);
        }

        // Применяем наклон камеры
        ApplyCameraRoll();
    }

    void ApplyCameraRoll()
    {
        if (_gameCamera == null) return;
        // Берём текущие углы и заменяем только Z
        Vector3 angles = _gameCamera.transform.localEulerAngles;
        angles.z = _defaultRoll + CameraRoll;
        _gameCamera.transform.localEulerAngles = angles;
    }

    // ── Экземпляр для Harmony-патча ───────────────────────────
    static CinematicCameraPlus _instance;

    // ── FOV override флаг ─────────────────────────────────────
    // true когда пользователь изменил FOV от дефолта
    public bool FovOverrideActive => _fovFound && Mathf.Abs(_fov - _defaultFov) > 0.5f;

    // ════════════════════════════════════════════════════════════
    //  Harmony prefix на Camera.set_fieldOfView
    //  Перехватывает ЛЮБУЮ запись FOV в ЛЮБУЮ камеру
    //  Если это наша игровая камера — подменяем значение на наш FOV
    // ════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Camera), "set_fieldOfView")]
    static class CameraFovPatch
    {
        static void Prefix(Camera __instance, ref float value)
        {
            if (_instance == null) return;
            if (!_instance.FovOverrideActive) return;
            if (__instance != _instance._gameCamera) return;
            // Подменяем значение ещё до того, как оно записывается в камеру
            value = _instance._fov;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  DrawAll — вызывается из OnGuiGlobal
    // ═══════════════════════════════════════════════════════════
    public void DrawAll()
    {
        if (Event.current.type != EventType.Repaint)
        {
            // Меню рисуем в любой момент
            DrawMenu();
            return;
        }

        // ── Все визуальные эффекты только в Repaint ──────────
        int sw = Screen.width;
        int sh = Screen.height;

        // 1. Насыщенность
        // Desaturation: нейтральный серый (0.5,0.5,0.5) вытягивает цвет к серому без синевы
        // Oversaturation: тёплый насыщенный оверлей в режиме Multiply-имитации
        if (Saturation < 0.99f)
        {
            float a = (1f - Saturation) * 0.85f;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, a);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }
        else if (Saturation > 1.01f)
        {
            // Увеличиваем насыщенность: слой Screen-blend через тёплый цвет
            // без синего — смешиваем только R и G чтобы не было синевы
            float a = (Saturation - 1f) * 0.18f;
            GUI.color = new Color(1f, 0.85f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 2. Яркость
        if (Brightness > 0.01f)
        {
            GUI.color = new Color(1, 1, 1, Brightness * 0.75f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }
        else if (Brightness < -0.01f)
        {
            GUI.color = new Color(0, 0, 0, -Brightness * 0.75f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 3. Контраст
        // Высокий контраст: тёмные тени (чёрный оверлей) + виньетка по краям
        // Низкий контраст: серый оверлей поднимает чёрные к серому (как fade)
        if (Contrast > 1.05f)
        {
            float ca = (Contrast - 1f) * 0.45f;
            // Затемняем тени — чёрный оверлей
            GUI.color = new Color(0, 0, 0, ca * 0.6f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            // Дополнительно — виньетка по краям усиливает ощущение контраста
            if (_vignTex != null)
            {
                GUI.color = new Color(0, 0, 0, ca * 0.5f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _vignTex);
            }
            GUI.color = Color.white;
        }
        else if (Contrast < 0.95f)
        {
            // Поднимаем чёрный — серый оверлей (fade-эффект)
            float ca = (1f - Contrast) * 0.5f;
            GUI.color = new Color(0.55f, 0.55f, 0.55f, ca);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 4. Гамма — осветляем или затемняем mid-тона
        if (Gamma > 1.05f)
        {
            float ga = (Gamma - 1f) * 0.4f;
            GUI.color = new Color(0, 0, 0, ga);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }
        else if (Gamma < 0.95f)
        {
            float ga = (1f - Gamma) * 0.5f;
            GUI.color = new Color(1, 1, 1, ga);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 5. Ночное зрение — зелёный тон + затемнение
        if (NightVision)
        {
            GUI.color = new Color(0, 0, 0, 0.4f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = new Color(0f, 0.9f, 0.1f, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 6. Хроматическая аберрация
        if (ChromAb)
        {
            float shift = sw * ChromAbInt;
            GUI.color = new Color(1, 0, 0, 0.12f);
            GUI.DrawTexture(new Rect(-shift, 0, sw, sh), _white);
            GUI.color = new Color(0, 0, 1, 0.12f);
            GUI.DrawTexture(new Rect(shift, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 7. Виньетка — используем предрассчитанную текстуру с градиентом
        if (Vignette)
        {
            GUI.color = new Color(1, 1, 1, VignetteInt * 1.2f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _vignTex);
            GUI.color = Color.white;
        }

        // 8. Сканлайны
        if (Scanlines)
        {
            GUI.color = new Color(1, 1, 1, ScanlinesInt);
            float tileH = (float)sh / 4f;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), _scanTex,
                new Rect(0, 0, 1, tileH));
            GUI.color = Color.white;
        }

        // 9. Film grain — случайный шум каждый кадр
        if (FilmGrain)
        {
            float a = FilmGrainInt * 0.7f;
            float r = Random.value;
            if (r < 0.5f)
            {
                GUI.color = new Color(1, 1, 1, a * r * 2f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            }
            else
            {
                GUI.color = new Color(0, 0, 0, a * (r - 0.5f) * 2f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            }
            GUI.color = Color.white;
        }

        // 10. Letterbox
        if (Letterbox)
        {
            float bh = sh * LetterboxH;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, bh), _white);
            GUI.DrawTexture(new Rect(0, sh - bh, sw, bh), _white);
            GUI.color = Color.white;
        }

        // 11. Сепия — коричнево-жёлтый тон поверх (нейтрализует синий)
        if (Sepia)
        {
            float a = SepiaInt * 0.55f;
            GUI.color = new Color(0.72f, 0.52f, 0.22f, a);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            // Убираем синий — дополнительный слой поверх
            GUI.color = new Color(0.3f, 0.15f, 0f, a * 0.4f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 12. Инфракрасный — красно-белый инвертированный вид
        if (Infrared)
        {
            // Тёплый красный оверлей + небольшое осветление
            GUI.color = new Color(0.9f, 0.05f, 0.05f, 0.35f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = new Color(1f, 0.8f, 0.7f, 0.15f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            // Виньетка жёсткая
            if (_vignTex != null)
            {
                GUI.color = new Color(0.5f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _vignTex);
            }
            GUI.color = Color.white;
        }

        // 13. Туман/дымка — молочный белый оверлей
        if (FogFilter)
        {
            GUI.color = new Color(0.85f, 0.88f, 0.92f, FogInt * 0.6f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);
            GUI.color = Color.white;
        }

        // 14. Старое ТВ — заметные помехи + горизонтальные полосы + пожелтение
        if (OldTV)
        {
            // Пожелтение/потускнение
            GUI.color = new Color(0.85f, 0.78f, 0.4f, 0.18f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _white);

            // Сканлайны — каждая вторая строка тёмная (имитация развёртки)
            GUI.color = new Color(0, 0, 0, 0.25f);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), _scanTex, new Rect(0, 0, 1, (float)sh / 4f));

            // 4-6 крупных горизонтальных полос помех
            int numBands = 4 + Mathf.FloorToInt(Random.value * 3f);
            for (int i = 0; i < numBands; i++)
            {
                float bandY = Random.value * sh;
                float bandH = 4f + Random.value * 20f;
                float bandA = 0.15f + Random.value * 0.35f;
                bool bright = Random.value > 0.4f;
                GUI.color = bright
                    ? new Color(1f, 1f, 1f, bandA)
                    : new Color(0f, 0f, 0f, bandA);
                GUI.DrawTexture(new Rect(0, bandY, sw, bandH), _white);
            }

            // Одна широкая "прокатывающаяся" полоса — движется по экрану
            float rollSpeed = 0.4f;
            float rollY = ((Time.time * rollSpeed) % 1f) * sh;
            GUI.color = new Color(1f, 1f, 0.9f, 0.18f);
            GUI.DrawTexture(new Rect(0, rollY, sw, sh * 0.07f), _white);
            // Хвост полосы — затухающий
            GUI.color = new Color(1f, 1f, 0.9f, 0.07f);
            GUI.DrawTexture(new Rect(0, rollY + sh * 0.07f, sw, sh * 0.05f), _white);

            // Случайные вертикальные смещения (дрожание строк)
            if (Random.value > 0.6f)
            {
                float jitterY = Random.value * sh;
                float jitterH = 2f + Random.value * 6f;
                float jitterX = (Random.value - 0.5f) * sw * 0.04f;
                GUI.color = new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(jitterX, jitterY, sw, jitterH), _white);
            }

            // Виньетка по краям — старые ТВ темнели по углам
            if (_vignTex != null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _vignTex);
            }

            GUI.color = Color.white;
        }

        // 15. Закат — тёплый оранжевый градиент снизу вверх
        if (Sunset)
        {
            float a = SunsetInt * 0.5f;
            // Нижняя треть — яркий оранжево-красный
            GUI.color = new Color(1f, 0.35f, 0f, a * 0.7f);
            GUI.DrawTexture(new Rect(0, sh * 0.6f, sw, sh * 0.4f), _white);
            // Средняя часть — золотой
            GUI.color = new Color(1f, 0.7f, 0.1f, a * 0.35f);
            GUI.DrawTexture(new Rect(0, sh * 0.3f, sw, sh * 0.35f), _white);
            // Верх — чуть фиолетового (небо на закате)
            GUI.color = new Color(0.4f, 0.1f, 0.5f, a * 0.2f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.35f), _white);
            GUI.color = Color.white;
        }

        // Меню
        DrawMenu();
    }

    void DrawMenu()
    {
        if (!_window.Visible || _window == null) return;
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            _window.Visible = false; _window.SavePrefs(); return;
        }
        using (new LegacyUIGuiScope(-10000, _uiScale))
            _window.Draw(DrawContent);
    }

    // ── Контент окна ─────────────────────────────────────────
    void DrawContent(Rect area)
    {
        var ui = new LegacyUILayout(area, 4f);
        string[] tabs = { "Camera", "Color", "Effects", "Presets" };
        Rect tr = ui.Row(30f);
        float tw = tr.width / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
            if (LegacyUI.TabButton(new Rect(tr.x + i * tw, tr.y, tw - 2f, tr.height), tabs[i], _tab == i)) _tab = i;
        ui.Space(4f);
        switch (_tab)
        {
            case 0: DrawCameraTab(ui, area); break;
            case 1: DrawColorTab(ui, area); break;
            case 2: DrawEffectsTab(ui, area); break;
            case 3: DrawPresetsTab(ui, area); break;
        }
    }

    void DrawCameraTab(LegacyUILayout ui, Rect area)
    {
        LegacyUI.Label(ui.Row(22f), "— FIELD OF VIEW —");
        LegacyUI.Status(ui.Row(20f), "FOV: " + _fov.ToString("F0") + "°  |  " + CameraStatus);
        _fov = LegacyUI.HorizontalSlider(ui.Row(26f), _fov, 20f, 120f);

        float bw = (area.width - 8f) / 3f;
        Rect r = ui.Row(40f);
        if (LegacyUI.Button(new Rect(r.x, r.y, bw, r.height), "60°")) _fov = 60f;
        if (LegacyUI.Button(new Rect(r.x + bw + 4f, r.y, bw, r.height), "75°")) _fov = 75f;
        if (LegacyUI.Button(new Rect(r.x + (bw + 4f) * 2, r.y, bw, r.height), "90°")) _fov = 90f;
        r = ui.Row(40f);
        if (LegacyUI.Button(new Rect(r.x, r.y, bw, r.height), "45° Cinematic")) _fov = 45f;
        if (LegacyUI.Button(new Rect(r.x + bw + 4f, r.y, bw, r.height), "35° Portrait")) _fov = 35f;
        if (LegacyUI.DangerButton(new Rect(r.x + (bw + 4f) * 2, r.y, bw, r.height), "Reset")) _fov = _defaultFov;

        LegacyUI.Separator(ui.Row(1f));
        LegacyUI.Label(ui.Row(22f), "— НАКЛОН КАМЕРЫ (ROLL) —");
        LegacyUI.Label(ui.Row(20f), "Наклон: " + CameraRoll.ToString("F1") + "°");
        CameraRoll = LegacyUI.HorizontalSlider(ui.Row(26f), CameraRoll, -45f, 45f);
        float rw = (area.width - 8f) / 3f;
        Rect rr = ui.Row(40f);
        if (LegacyUI.Button(new Rect(rr.x, rr.y, rw, rr.height), "-15°")) CameraRoll = -15f;
        if (LegacyUI.Button(new Rect(rr.x + rw + 4f, rr.y, rw, rr.height), "0° (норм)")) CameraRoll = 0f;
        if (LegacyUI.Button(new Rect(rr.x + (rw + 4f) * 2, rr.y, rw, rr.height), "+15°")) CameraRoll = 15f;

        LegacyUI.Separator(ui.Row(1f));
        LegacyUI.Label(ui.Row(22f), "— LETTERBOX —");
        if (LegacyUI.Button(ui.Row(40f), "Letterbox: " + (Letterbox ? "ON" : "OFF"))) Letterbox = !Letterbox;
        if (Letterbox)
        {
            LegacyUI.Label(ui.Row(20f), "Высота: " + (LetterboxH * 100f).ToString("F0") + "%");
            LetterboxH = LegacyUI.HorizontalSlider(ui.Row(24f), LetterboxH, 0.03f, 0.2f);
            float lw = (area.width - 8f) / 3f;
            Rect lr = ui.Row(40f);
            if (LegacyUI.Button(new Rect(lr.x, lr.y, lw, lr.height), "2.39:1")) LetterboxH = 0.105f;
            if (LegacyUI.Button(new Rect(lr.x + lw + 4f, lr.y, lw, lr.height), "2.35:1")) LetterboxH = 0.120f;
            if (LegacyUI.Button(new Rect(lr.x + (lw + 4f) * 2, lr.y, lw, lr.height), "1.85:1")) LetterboxH = 0.073f;
        }
    }

    void DrawColorTab(LegacyUILayout ui, Rect area)
    {
        LegacyUI.Label(ui.Row(22f), "— КОРРЕКЦИЯ ЦВЕТА —");
        LegacyUI.MiniHint(ui.Row(22f), "Камера: " + CameraStatus);

        LegacyUI.Label(ui.Row(20f), "Яркость: " + Brightness.ToString("F2"));
        Brightness = LegacyUI.HorizontalSlider(ui.Row(24f), Brightness, -1f, 1f);

        LegacyUI.Label(ui.Row(20f), "Контраст: " + Contrast.ToString("F2"));
        Contrast = LegacyUI.HorizontalSlider(ui.Row(24f), Contrast, 0.5f, 2f);

        LegacyUI.Label(ui.Row(20f), "Насыщенность: " + Saturation.ToString("F2"));
        Saturation = LegacyUI.HorizontalSlider(ui.Row(24f), Saturation, 0f, 2f);

        LegacyUI.Label(ui.Row(20f), "Гамма: " + Gamma.ToString("F2"));
        Gamma = LegacyUI.HorizontalSlider(ui.Row(24f), Gamma, 0.5f, 2f);

        LegacyUI.Separator(ui.Row(1f));
        if (LegacyUI.DangerButton(ui.Row(40f), "Сбросить всё"))
        { Brightness = 0f; Contrast = 1f; Saturation = 1f; Gamma = 1f; }
    }

    private LegacyUIScrollView fxScroll = new LegacyUIScrollView(new Rect(0f, 0f, 100f, 100f))
    {
        Padding = 8f,
        ShowHorizontalScrollbar = false,
        ShowVerticalScrollbar = true
    };

    void DrawEffectsTab(LegacyUILayout ui, Rect area)
    {
        float x = area.x, y = area.y, width = area.width;

        fxScroll.ViewRect = new Rect(ui.Row(535f));
        fxScroll.Draw(content =>
        {
            float x = content.x, y = content.y, width = content.width;
            LegacyUI.Label(new Rect(x, y, width, 40f), "— ЭФФЕКТЫ —");
            y += 40f;

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Виньетка: " + (Vignette ? "ON" : "OFF"))) Vignette = !Vignette;
            if (Vignette)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + VignetteInt.ToString("F2"));
                y += 20f;
                VignetteInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), VignetteInt, 0.1f, 1f);
                y += 30f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Хром. аберрация: " + (ChromAb ? "ON" : "OFF"))) ChromAb = !ChromAb;
            if (ChromAb)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + ChromAbInt.ToString("F3"));
                y += 20f;
                ChromAbInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), ChromAbInt, 0.002f, 0.08f);
                y += 35f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Сканлайны: " + (Scanlines ? "ON" : "OFF"))) Scanlines = !Scanlines;
            if (Scanlines)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + ScanlinesInt.ToString("F2"));
                y += 20f;
                ScanlinesInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), ScanlinesInt, 0.05f, 0.8f);
                y += 30f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Зернистость: " + (FilmGrain ? "ON" : "OFF"))) FilmGrain = !FilmGrain;
            if (FilmGrain)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + FilmGrainInt.ToString("F2"));
                y += 20f;
                FilmGrainInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), FilmGrainInt, 0.01f, 0.3f);
                y += 30f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Ночное зрение: " + (NightVision ? "ON" : "OFF"))) NightVision = !NightVision;

            y += 45f;
            LegacyUI.Separator(new Rect(x, y, width, 1f));
            y += 5f;
            LegacyUI.Label(new Rect(x, y, width, 22f), "— НОВЫЕ ФИЛЬТРЫ —");
            y += 27f;

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Сепия: " + (Sepia ? "ON" : "OFF"))) Sepia = !Sepia;
            if (Sepia)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + SepiaInt.ToString("F2"));
                y += 20f;
                SepiaInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), SepiaInt, 0.1f, 1f);
                y += 30f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Инфракрасный: " + (Infrared ? "ON" : "OFF"))) Infrared = !Infrared;
            y += 45f;

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Туман/дымка: " + (FogFilter ? "ON" : "OFF"))) FogFilter = !FogFilter;
            if (FogFilter)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Густота: " + FogInt.ToString("F2"));
                y += 20f;
                FogInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), FogInt, 0.05f, 0.9f);
                y += 30f;
            }
            else { y += 45f; }

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Старое ТВ: " + (OldTV ? "ON" : "OFF"))) OldTV = !OldTV;
            y += 45f;

            if (LegacyUI.Button(new Rect(x, y, width, 40f), "Закат: " + (Sunset ? "ON" : "OFF"))) Sunset = !Sunset;
            if (Sunset)
            {
                y += 45f;
                LegacyUI.Label(new Rect(x, y, width, 20f), "Интенсивность: " + SunsetInt.ToString("F2"));
                y += 20f;
                SunsetInt = LegacyUI.HorizontalSlider(new Rect(x, y, width, 22f), SunsetInt, 0.1f, 1f);
                y += 30f;
            }
            else { y += 45f; }
        });

        LegacyUI.Separator(ui.Row(1f));
        if (LegacyUI.DangerButton(ui.Row(33f), "Выключить все эффекты"))
        {
            Vignette = false; ChromAb = false; Scanlines = false; FilmGrain = false; NightVision = false;
            Sepia = false; Infrared = false; FogFilter = false; OldTV = false; Sunset = false;
        }
    }

    void DrawPresetsTab(LegacyUILayout ui, Rect area)
    {
        LegacyUI.Label(ui.Row(22f), "— КИНЕМАТОГРАФИЧЕСКИЕ ПРЕСЕТЫ —");
        LegacyUI.MiniHint(ui.Row(28f), "Меняет FOV, цвет и эффекты одновременно.");
        string[] descs ={
            "FOV 50, контраст+, виньетка — классика",
            "Ч/Б, контраст+ — нуар/детектив",
            "Тёплые тона, насыщ+ — золотой час",
            "Синий тон, тёмный — ночная сцена",
            "Зерно, контраст— — старая плёнка",
            "Тёмный, зелёный тон — хоррор",
            "Яркий, насыщ+ — мультяшный",
            "Все параметры по умолчанию"
        };
        for (int i = 0; i < PresetNames.Length; i++)
        {
            LegacyUI.Label(ui.Row(18f), descs[i]);
            string lbl = (_activePreset == i) ? "✓ " + PresetNames[i] + " (активен)" : PresetNames[i];
            if (LegacyUI.Button(ui.Row(38f), lbl)) ApplyPreset(i);
            ui.Space(2f);
        }
    }

    void ApplyPreset(int idx)
    {
        _activePreset = idx;
        Brightness = 0f; Contrast = 1f; Saturation = 1f; Gamma = 1f;
        Vignette = false; ChromAb = false; Scanlines = false; FilmGrain = false; NightVision = false; Letterbox = false;
        switch (idx)
        {
            case 0: _fov = 50f; Contrast = 1.2f; Brightness = -0.05f; Saturation = 0.9f; Vignette = true; VignetteInt = 0.5f; Letterbox = true; LetterboxH = 0.105f; break;
            case 1: _fov = 55f; Saturation = 0f; Contrast = 1.5f; Brightness = -0.1f; Vignette = true; VignetteInt = 0.7f; FilmGrain = true; FilmGrainInt = 0.08f; break;
            case 2: _fov = 60f; Brightness = 0.05f; Contrast = 1.15f; Saturation = 1.4f; Gamma = 0.9f; Vignette = true; VignetteInt = 0.35f; break;
            case 3: _fov = 65f; Brightness = -0.15f; Contrast = 1.1f; Saturation = 0.7f; Gamma = 1.2f; Vignette = true; VignetteInt = 0.6f; ChromAb = true; ChromAbInt = 0.015f; break;
            case 4: _fov = 58f; Contrast = 0.85f; Saturation = 0.75f; Brightness = 0.05f; Gamma = 1.1f; FilmGrain = true; FilmGrainInt = 0.12f; Scanlines = true; ScanlinesInt = 0.15f; Vignette = true; VignetteInt = 0.45f; break;
            case 5: _fov = 70f; Brightness = -0.2f; Contrast = 1.3f; Saturation = 0.5f; Gamma = 1.15f; Vignette = true; VignetteInt = 0.8f; FilmGrain = true; FilmGrainInt = 0.06f; ChromAb = true; ChromAbInt = 0.02f; break;
            case 6: _fov = 65f; Saturation = 1.6f; Contrast = 1.1f; Brightness = 0.05f; Gamma = 0.95f; break;
            case 7: _fov = _defaultFov > 1f ? _defaultFov : 60f; _activePreset = -1; break;
        }
    }

    // ── OnGuiGlobal ──────────────────────────────────────────
    public class CinematicGUI : MonoBehaviour, OnGuiGlobal.IOnGuiNeed
    {
        public CinematicCameraPlus plugin;
        public bool isNeedOnGUI() => plugin != null;
        public void OnGUI_Manual()
        {
            try { plugin?.DrawAll(); }
            catch (System.Exception e) { GUI.Box(new Rect(10, 80, 600, 50), "CCP ERR: " + e.Message); }
        }
    }
}