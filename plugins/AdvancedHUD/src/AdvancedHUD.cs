using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BigCityLegacy.UI;
using System;
using System.Reflection;
using UnityEngine;

namespace AdvancedHUD
{
    [BepInPlugin("com.mods.advancedhud", "AdvancedHUD", "1.4.0")]
    [BepInDependency("com.bigcitylegacy.baseplugin", "1.4.0")]
    public class AdvancedHUDPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigFile Settings;
        private GameObject guiObject;

        private void Awake()
        {
            Log = Logger;
            Settings = Config;
            LegacyInput.Init();
            var go = guiObject = new GameObject("AdvancedHUDGui");
            DontDestroyOnLoad(go);
            go.AddComponent<AdvancedHUDGui>();
        }

        private void OnDestroy() { if (guiObject) Destroy(guiObject); }

        // ─────────────────────────────────────────────────────────────────────
        public class AdvancedHUDGui : MonoBehaviour
        {
            // ── Widget indices ────────────────────────────────────────────────
            const int W_VEHICLE = 0;
            const int W_HEALTH = 1;
            const int W_WEAPONS = 2;
            const int W_POSITION = 3;
            const int W_PERF = 4;
            const int W_SESSION = 5;
            const int W_CLOCK = 6;
            const int WIDGET_COUNT = 7;

            static readonly string[] WIDGET_NAMES =
                { "Vehicle", "Health", "Weapons", "Position", "Performance", "Session", "Clock" };

            // ── Windows ───────────────────────────────────────────────────────
            LegacyUIWindow[] _windows;
            LegacyUIWindow _settingsWin;
            readonly PluginUiSupport _uiSupport = new PluginUiSupport();
            readonly LegacyUIScaleSettings _scale = new LegacyUIScaleSettings();
            ConfigEntry<bool> _blur;
            ConfigEntry<float> _plainAlpha, _blurAlpha;
            LegacyUITheme _speedTheme;
            readonly LegacyUIScrollView _weaponsScroll = new LegacyUIScrollView(new Rect())
                { DrawBackground = false, ShowHorizontalScrollbar = false };

            // ── ScrollView для настроек (авто-размер контента) ────────────────
            LegacyUIScrollView _settingsScroll;

            // ── FPS / session ─────────────────────────────────────────────────
            float _fps, _frameTimeMs, _lowFpsPct;
            float _fpsTimer;
            int _fpsFrames, _lowFrames, _totalFrames;
            float _sessionStart;

            // ═════════════════════════════════════════════════════════════════
            // КОМПАС — состояние
            // ═════════════════════════════════════════════════════════════════
            bool _compassVisible = true;
            bool _showDegrees = true;
            bool _showCardinals = true;
            bool _showMarkerLine = true;
            bool _showCurrentDeg = true;
            bool _showTickLines = true;
            bool _showBorder = true;
            bool _compassSettingsExpanded = false;

            float _compassY = 10f;
            float _compassX = 0f;
            float _compassWidth = 600f;
            float _compassHeight = 36f;
            float _fontSizeBig = 20f;
            float _fontSizeSmall = 12f;
            float _fontSizeDeg = 14f;
            float _fieldOfView = 90f;
            float _degreeStep = 30f;
            float _borderThickness = 1f;

            Color _colorNorth = Color.red;
            Color _colorCardinal = Color.white;
            Color _colorDegree = new Color(0.7f, 0.7f, 0.7f);
            Color _colorBar = new Color(0f, 0f, 0f, 0.55f);
            Color _colorMarker = Color.yellow;
            Color _colorBorder = new Color(1f, 1f, 1f, 0.3f);
            Color _colorTickBig = new Color(1f, 1f, 1f, 0.8f);
            Color _colorTickSmall = new Color(1f, 1f, 1f, 0.35f);
            Color _colorCurrentDeg = Color.white;

            float _yaw = 0f;

            // Кэш стилей компаса
            GUIStyle _cStyleBig, _cStyleSmall, _cStyleDeg;
            float _cCachedBig = -1f, _cCachedSmall = -1f, _cCachedDeg = -1f;

            // ── Reflection flags ──────────────────────────────────────────────
            static readonly BindingFlags BF_INST = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            static readonly BindingFlags BF_STATIC = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // ── Types ─────────────────────────────────────────────────────────
            static readonly Type t_PlayerControl = FindType("PlayerControl");
            static readonly Type t_Control = FindType("Control");
            static readonly Type t_CarControl = FindType("CarControl");
            static readonly Type t_CarEngine = FindType("CarEngine");

            // ── Cached fields / props ─────────────────────────────────────────
            static readonly PropertyInfo p_curPlayer = SafeProp(t_PlayerControl, "curPlyaer", BF_STATIC);
            static readonly FieldInfo f_parent_control = SafeField(t_PlayerControl, "parent_control", BF_INST);
            static readonly FieldInfo f_weapones = SafeField(t_Control, "weapones", BF_INST);
            static readonly FieldInfo f_selectWeapone = SafeField(t_Control, "selectWeapone", BF_INST);
            static readonly FieldInfo f_live = SafeField(t_Control, "_live", BF_INST);
            static readonly FieldInfo f_body = SafeField(t_Control, "body", BF_INST);
            static readonly FieldInfo f_body_pos = SafeField(t_Control, "body_pos", BF_INST);
            static readonly FieldInfo f_azot_mul = SafeField(t_CarControl, "azot_mul", BF_INST);
            static readonly FieldInfo f_ph_car = SafeField(t_CarControl, "ph_car", BF_INST);
            static readonly FieldInfo f_engine = FindField("CarPh", "engine", BF_INST);
            static readonly FieldInfo f_rpm = SafeField(t_CarEngine, "rpm", BF_INST);
            static readonly FieldInfo f_cur_gear = SafeField(t_CarEngine, "cur_gear", BF_INST);
            static readonly FieldInfo f_cur_state = SafeField(t_CarEngine, "cur_state", BF_INST);

            // ── Position cache ────────────────────────────────────────────────
            static readonly Type t_FlyCamera = FindType("FlyCamera");
            static readonly FieldInfo f_flyMe = SafeField(t_FlyCamera, "me", BF_STATIC);
            static readonly FieldInfo f_flyCam = SafeField(t_FlyCamera, "cam", BF_INST);
            static readonly Type t_CamCurrent = FindType("CamCurrent");
            static readonly FieldInfo f_unityCam = SafeField(t_CamCurrent, "unityCam", BF_INST);
            static readonly Type t_Cam = FindType("Cam");
            static readonly FieldInfo f_camMe = SafeField(t_Cam, "me", BF_STATIC);

            Camera _cachedCamera;
            bool _cachedIsFly;
            Vector3 _cachedPlayerPos;
            Vector3 _cachedCamPos;
            int _posCacheFrame = -999;

            // ── Reflection helpers ────────────────────────────────────────────
            static Type FindType(string name)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    try { foreach (var t in asm.GetTypes()) if (t.Name == name) return t; } catch { }
                return null;
            }
            static FieldInfo SafeField(Type t, string n, BindingFlags f)
            { try { return t?.GetField(n, f); } catch { return null; } }
            static FieldInfo FindField(string typeName, string fieldName, BindingFlags f)
            { var t = FindType(typeName); return SafeField(t, fieldName, f); }
            static PropertyInfo SafeProp(Type t, string n, BindingFlags f)
            { try { return t?.GetProperty(n, f); } catch { return null; } }
            static object GetVal(FieldInfo fi, object obj)
            { try { return fi?.GetValue(obj); } catch { return null; } }
            static object GetProp(PropertyInfo pi, object obj)
            { try { return pi?.GetValue(obj, null); } catch { return null; } }
            static object WalkField(object obj, string name)
            {
                if (obj == null) return null;
                Type t = obj.GetType();
                while (t != null)
                {
                    try
                    {
                        var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (fi != null) return fi.GetValue(obj);
                    }
                    catch { }
                    t = t.BaseType;
                }
                return null;
            }

            object GetPlayer() { return GetProp(p_curPlayer, null); }
            object GetCar(object player) { return GetVal(f_parent_control, player); }

            // ── Lifecycle ─────────────────────────────────────────────────────
            void Awake()
            {
                _sessionStart = Time.realtimeSinceStartup;
                _blur = AdvancedHUDPlugin.Settings.Bind("Appearance", "BackdropBlur", false, "Blur widget backgrounds using BCL's scene backdrop.");
                _plainAlpha = AdvancedHUDPlugin.Settings.Bind("Appearance", "BackgroundAlpha", 0.75f, new ConfigDescription("Widget background opacity without blur.", new AcceptableValueRange<float>(0f, 1f)));
                _blurAlpha = AdvancedHUDPlugin.Settings.Bind("Appearance", "BlurBackgroundAlpha", 1f, new ConfigDescription("Widget background opacity with blur. BlurTintAlpha is always 0.75.", new AcceptableValueRange<float>(0f, 1f)));
                int sw = Mathf.RoundToInt(Screen.width / _scale.ResolveScale()), sh = Mathf.RoundToInt(Screen.height / _scale.ResolveScale());

                var opts = new LegacyUIWindowOptions
                {
                    Draggable = true,
                    ClampToScreen = true,
                    ShowCloseButton = false,
                    FadeWhileDragging = true,
                    DragAlpha = 0.55f,
                    BackgroundAlpha = _plainAlpha.Value,
                    BackdropBlur = _blur.Value,
                    BlurTintAlpha = 0.75f,
                    InputBlockMode = LegacyUIInputBlockMode.Window,
                };

                _windows = new LegacyUIWindow[WIDGET_COUNT];
                _windows[W_VEHICLE] = new LegacyUIWindow("Vehicle", new Rect(20, sh - 285, 260, 265), opts);
                _windows[W_HEALTH] = new LegacyUIWindow("Health", new Rect(20, 20, 240, 165), opts);
                _windows[W_WEAPONS] = new LegacyUIWindow("Weapons", new Rect(sw - 260, 20, 240, 240), opts);
                _windows[W_POSITION] = new LegacyUIWindow("Position", new Rect(sw - 260, 270, 240, 235), opts);
                _windows[W_PERF] = new LegacyUIWindow("Performance", new Rect(sw - 260, sh - 210, 240, 190), opts);
                _windows[W_SESSION] = new LegacyUIWindow("Session", new Rect(sw / 2 - 110, sh - 110, 220, 90), opts);
                _windows[W_CLOCK] = new LegacyUIWindow("Clock", new Rect(sw / 2 - 110, 20, 220, 90), opts);
                foreach (var w in _windows) w.Visible = false;

                // Настройки — окно с прокруткой; высота 480 вмещает все секции
                _settingsWin = new LegacyUIWindow(
                    "⚙  AdvancedHUD",
                    new Rect(sw / 2 - 170, sh / 2 - 240, 340, 480),
                    new LegacyUIWindowOptions
                    {
                        Draggable = true,
                        ClampToScreen = true,
                        ShowCloseButton = true,
                        FadeWhileDragging = true,
                        DragAlpha = 0.6f,
                        BackgroundAlpha = 0.95f,
                        RightHint = "[Alt+X]",
                        InputBlockMode = LegacyUIInputBlockMode.Window,
                    });
                _settingsWin.Visible = false;

                // ScrollView занимает весь контент окна (без заголовка ~38px)
                _settingsScroll = new LegacyUIScrollView(new Rect(0, 0, 340, 440))
                {
                    Padding = 6f,
                    ShowHorizontalScrollbar = false,
                    ShowVerticalScrollbar = true,
                    DrawBackground = false,
                };
            }

            void Update()
            {
                bool gameplay = LegacyHelpers.IsGameplayRunning;
                if (gameplay && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.X))
                {
                    _settingsWin.Visible = !_settingsWin.Visible;
                    _settingsWin.SavePrefs();
                }

                _uiSupport.SetCursor(this, gameplay && _settingsWin.Visible);
                if (!gameplay) return;

                // Позиция кэшируется раз в 10 кадров
                if (_windows[W_POSITION].Visible)
                    UpdatePositionCache(GetPlayer());

                // Yaw для компаса из камеры
                UpdateYaw();

                // FPS
                _fpsFrames++; _totalFrames++;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && 1f / dt < 30f) _lowFrames++;
                _fpsTimer += dt;
                if (_fpsTimer >= 1f)
                {
                    _fps = _fpsFrames / _fpsTimer;
                    _frameTimeMs = (_fpsTimer / _fpsFrames) * 1000f;
                    _lowFpsPct = _totalFrames > 0 ? _lowFrames / (float)_totalFrames * 100f : 0f;
                    _fpsFrames = _lowFrames = _totalFrames = 0;
                    _fpsTimer = 0f;
                }
            }

            void UpdateYaw()
            {
                // Приоритет: Camera.main → allCameras
                if (Camera.main != null) { _yaw = Camera.main.transform.eulerAngles.y; return; }
                foreach (var c in Camera.allCameras)
                    if (c != null && c.isActiveAndEnabled) { _yaw = c.transform.eulerAngles.y; return; }
            }

            void OnDisable() { _uiSupport.Dispose(); }
            void OnDestroy() { _uiSupport.Dispose(); }

            void OnGUI()
            {
                if (!LegacyHelpers.IsGameplayRunning) return;
                foreach (var window in _windows)
                {
                    window.Options.BackdropBlur = _blur.Value;
                    window.Options.BlurTintAlpha = 0.75f;
                    window.Options.BackgroundAlpha = _blur.Value ? _blurAlpha.Value : _plainAlpha.Value;
                }
                using (new LegacyUIGuiScope(-10000, _scale, null, _uiSupport.Backdrop))
                {
                    // Компас рисуется поверх всего, вне LegacyUIWindow
                    if (_compassVisible)
                        DrawCompass();

                    object player = GetPlayer();
                    object car = player != null ? GetCar(player) : null;

                    if (_windows[W_VEHICLE].Visible) _windows[W_VEHICLE].Draw(r => DrawVehicle(r, car));
                    if (_windows[W_HEALTH].Visible) _windows[W_HEALTH].Draw(r => DrawHealth(r, player, car));
                    if (_windows[W_WEAPONS].Visible) _windows[W_WEAPONS].Draw(r => DrawWeapons(r, player));
                    if (_windows[W_POSITION].Visible) _windows[W_POSITION].Draw(r => DrawPosition(r, player));
                    if (_windows[W_PERF].Visible) _windows[W_PERF].Draw(r => DrawPerf(r));
                    if (_windows[W_SESSION].Visible) _windows[W_SESSION].Draw(r => DrawSession(r));

                    if (_windows[W_CLOCK].IsRendered) _windows[W_CLOCK].Draw(r => LegacyUI.Title(r, DateTime.Now.ToString("HH:mm:ss")));

                    // Настройки с прокруткой
                    if (_settingsWin.IsRendered)
                        _settingsWin.Draw(DrawSettings);
                }
            }

            // ═════════════════════════════════════════════════════════════════
            // КОМПАС
            // ═════════════════════════════════════════════════════════════════

            void EnsureCompassStyles()
            {
                if (!Mathf.Approximately(_cCachedBig, _fontSizeBig))
                {
                    _cCachedBig = _fontSizeBig;
                    _cStyleBig = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = Mathf.RoundToInt(_fontSizeBig)
                    };
                }
                if (!Mathf.Approximately(_cCachedSmall, _fontSizeSmall))
                {
                    _cCachedSmall = _fontSizeSmall;
                    _cStyleSmall = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = Mathf.RoundToInt(_fontSizeSmall)
                    };
                }
                if (!Mathf.Approximately(_cCachedDeg, _fontSizeDeg))
                {
                    _cCachedDeg = _fontSizeDeg;
                    _cStyleDeg = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = Mathf.RoundToInt(_fontSizeDeg)
                    };
                }
            }

            void DrawCompass()
            {
                if (Event.current.type == EventType.Repaint)
                    EnsureCompassStyles();

                if (_cStyleBig == null) return;
                if (Event.current.type != EventType.Repaint) return;

                float scale = LegacyUI.Scale > 0f ? LegacyUI.Scale : 1f;
                float cx = Screen.width / scale / 2f + _compassX;
                float barX = cx - _compassWidth / 2f;
                float barY = _compassY;

                // The compass follows the same background settings as all widgets.
                Rect bar = new Rect(barX, barY, _compassWidth, _compassHeight);
                bool blurred = _blur.Value && _uiSupport.Backdrop != null && _uiSupport.Backdrop.Draw(bar);
                float alpha = _blur.Value ? _blurAlpha.Value : _plainAlpha.Value;
                GUI.color = new Color(_colorBar.r, _colorBar.g, _colorBar.b, alpha * (blurred ? 0.75f : 1f));
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Рамка
                if (_showBorder)
                {
                    float t = _borderThickness;
                    GUI.color = _colorBorder;
                    GUI.DrawTexture(new Rect(barX, barY, _compassWidth, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(barX, barY + _compassHeight - t, _compassWidth, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(barX, barY, t, _compassHeight), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(barX + _compassWidth - t, barY, t, _compassHeight), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // Шаг 1° — рисуем каждый градус, текст только на кратных degreeStep
                // Это исправляет баг «через градус пропадает текст»
                for (int angle = -180; angle <= 180; angle++)
                {
                    float worldAngle = (_yaw + angle + 360f) % 360f;
                    float screenX = cx + (angle / (_fieldOfView / 2f)) * (_compassWidth / 2f);
                    if (screenX < barX + 2f || screenX > barX + _compassWidth - 2f) continue;

                    int wa = Mathf.RoundToInt(worldAngle) % 360;
                    bool isN = wa == 0;
                    bool isCard = wa == 90 || wa == 180 || wa == 270;
                    bool isBig = wa % Mathf.Max(1, Mathf.RoundToInt(_degreeStep)) == 0;
                    bool is10 = wa % 10 == 0;

                    if (_showCardinals && (isN || isCard))
                    {
                        string lbl = isN ? "N" : wa == 90 ? "E" : wa == 180 ? "S" : "W";
                        _cStyleBig.normal.textColor = isN ? _colorNorth : _colorCardinal;
                        GUI.Label(new Rect(screenX - 20f, barY, 40f, _compassHeight), lbl, _cStyleBig);

                        if (_showTickLines)
                        {
                            GUI.color = isN ? _colorNorth : _colorTickBig;
                            GUI.DrawTexture(new Rect(screenX - 1f, barY, 2f, _compassHeight * 0.55f), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                        }
                    }
                    else if (_showDegrees && isBig)
                    {
                        _cStyleSmall.normal.textColor = _colorDegree;
                        GUI.Label(new Rect(screenX - 18f, barY, 36f, _compassHeight), wa + "°", _cStyleSmall);

                        if (_showTickLines)
                        {
                            GUI.color = _colorTickBig;
                            GUI.DrawTexture(new Rect(screenX - 1f, barY + _compassHeight * 0.55f, 1f, _compassHeight * 0.45f), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                        }
                    }
                    else if (_showTickLines && is10)
                    {
                        GUI.color = _colorTickSmall;
                        GUI.DrawTexture(new Rect(screenX, barY + _compassHeight * 0.72f, 1f, _compassHeight * 0.28f), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                }

                // Центральный маркер
                if (_showMarkerLine)
                {
                    GUI.color = _colorMarker;
                    GUI.DrawTexture(new Rect(cx - 1f, barY, 2f, _compassHeight), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 5f, barY + _compassHeight, 10f, 6f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // Текущий градус
                if (_showCurrentDeg)
                {
                    _cStyleDeg.normal.textColor = _colorCurrentDeg;
                    GUI.Label(new Rect(cx - 35f, barY + _compassHeight + 6f, 70f, 24f),
                        Mathf.RoundToInt(_yaw) + "°", _cStyleDeg);
                }
            }

            // ═════════════════════════════════════════════════════════════════
            // HUD виджеты
            // ═════════════════════════════════════════════════════════════════

            void DrawVehicle(Rect content, object car)
            {
                LegacyUILayout ui = new LegacyUILayout(content, 5f);

                float speed = 0f;
                if (car != null)
                {
                    var rb = GetVal(f_body, car) as Rigidbody;
                    if (rb != null) speed = rb.velocity.magnitude * 3.6f;
                }
                if (_speedTheme == null)
                {
                    _speedTheme = new LegacyUITheme();
                    _speedTheme.Styles.Title.fontSize = 38;
                    _speedTheme.Styles.Title.alignment = TextAnchor.MiddleCenter;
                }
                using (LegacyUI.WithTheme(_speedTheme)) LegacyUI.Title(ui.Row(52f), $"{speed:F0} km/h");

                string gear = "N"; float rpm = 0f; string engineStr = "—";
                if (car != null)
                {
                    object phCar = GetVal(f_ph_car, car);
                    if (phCar != null)
                    {
                        object eng = GetVal(f_engine, phCar);
                        if (eng != null)
                        {
                            object rpmVal = GetVal(f_rpm, eng);
                            if (rpmVal != null) try { rpm = Convert.ToSingle(rpmVal); } catch { }
                            object gearVal = GetVal(f_cur_gear, eng);
                            if (gearVal != null)
                            {
                                int g = 0; try { g = Convert.ToInt32(gearVal); } catch { }
                                gear = g < 0 ? "R" : g == 0 ? "N" : g.ToString();
                            }
                            object stateVal = GetVal(f_cur_state, eng);
                            if (stateVal != null)
                            {
                                int s = 0; try { s = Convert.ToInt32(stateVal); } catch { }
                                if (s == 0) engineStr = "Off";
                                else if (s == 1) engineStr = "Starting";
                                else if (s == 2) engineStr = "On";
                                else engineStr = s.ToString();
                            }
                        }
                    }
                }
                LegacyUI.Label(ui.Row(20f), $"Gear:    {gear}");
                LegacyUI.Label(ui.Row(20f), $"RPM:     {rpm:F0}");
                LegacyUI.ProgressBar(ui.Row(16f), rpm, 0d, 9000f);
                float nitro = 1f;
                if (car != null)
                {
                    object nVal = GetVal(f_azot_mul, car);
                    if (nVal != null) try { nitro = Convert.ToSingle(nVal); } catch { }
                }
                LegacyUI.Label(ui.Row(20f), $"Nitro:   {nitro:F1}x");
                LegacyUI.Label(ui.Row(20f), $"Engine:  {engineStr}");
            }

            void DrawHealth(Rect content, object player, object car)
            {
                LegacyUILayout ui = new LegacyUILayout(content, 5f);
                float pHp = 0f;
                if (player != null)
                { object v = GetVal(f_live, player); if (v != null) try { pHp = Convert.ToSingle(v); } catch { } }
                LegacyUI.Label(ui.Row(20f), $"Player:  {pHp:F0} / 100");
                LegacyUI.ProgressBar(ui.Row(16f), pHp, 0d, 100f);
                ui.Space(6f);
                float cHp = 0f;
                if (car != null)
                { object v = GetVal(f_live, car); if (v != null) try { cHp = Convert.ToSingle(v); } catch { } }
                LegacyUI.Label(ui.Row(20f), $"Car:     {cHp:F0} / 100");
                LegacyUI.ProgressBar(ui.Row(16f), cHp, 0d, 100f);
            }

            void DrawWeapons(Rect content, object player)
            {
                _weaponsScroll.ViewRect = content;
                _weaponsScroll.Draw(inner => DrawWeaponsContent(inner, player));
            }

            void DrawWeaponsContent(Rect content, object player)
            {
                LegacyUILayout ui = new LegacyUILayout(content, 4f);
                if (player == null) { LegacyUI.MiniHint(ui.Row(20f), "No player"); return; }
                var list = GetVal(f_weapones, player) as System.Collections.IList;
                object curObj = GetVal(f_selectWeapone, player);
                int curId = -1;
                if (curObj is UnityEngine.Object uo) curId = uo.GetInstanceID();
                if (list == null) { LegacyUI.MiniHint(ui.Row(20f), "No data"); return; }
                for (int i = 0; i < list.Count; i++)
                {
                    object w = list[i]; if (w == null) continue;
                    object typeObj = WalkField(w, "type");
                    string wName = typeObj?.ToString() ?? "?";
                    if (wName == "Player_Hand" || wName == "Unknow" || wName == "?" || wName == "Unknown") continue;
                    if (wName.StartsWith("Player_")) wName = wName.Substring(7);
                    object ammoVal = WalkField(w, "totalBulletsCount") ?? WalkField(w, "ammo")
                                  ?? WalkField(w, "ammoCount") ?? WalkField(w, "count");
                    string ammo = ammoVal != null ? ammoVal.ToString() : "∞";
                    bool isCur = curId >= 0 && w is UnityEngine.Object wo2 && wo2.GetInstanceID() == curId;
                    string line = (isCur ? "▶ " : "  ") + wName + "  [" + ammo + "]";
                    if (isCur)
                        using (LegacyUI.WithTheme(GetHighlightTheme()))
                            LegacyUI.Label(ui.Row(20f), line);
                    else
                        LegacyUI.Label(ui.Row(20f), line);
                }
            }

            LegacyUITheme _highlightTheme;
            LegacyUITheme GetHighlightTheme()
            {
                if (_highlightTheme != null) return _highlightTheme;
                var p = new LegacyUIPalette { Text = Color.yellow, TextBright = Color.yellow };
                return _highlightTheme = new LegacyUITheme(p);
            }

            // ── Position cache ────────────────────────────────────────────────
            void UpdatePositionCache(object player)
            {
                if (Time.frameCount - _posCacheFrame < 10) return;
                _posCacheFrame = Time.frameCount;

                _cachedPlayerPos = Vector3.zero;
                if (player != null)
                {
                    object v = GetVal(f_body_pos, player);
                    if (v is Vector3 vp && vp != Vector3.zero) _cachedPlayerPos = vp;
                    else
                    {
                        var rb = GetVal(f_body, player) as Rigidbody;
                        if (rb != null) _cachedPlayerPos = rb.position;
                        else if (player is Component comp) _cachedPlayerPos = comp.transform.position;
                    }
                }

                _cachedIsFly = false;
                Vector3 flyCamPos = Vector3.zero;
                try
                {
                    object flyMe = GetVal(f_flyMe, null);
                    if (flyMe != null)
                    {
                        _cachedIsFly = true;
                        object camCurrent = GetVal(f_flyCam, flyMe);
                        if (camCurrent != null)
                        {
                            var unityCam = GetVal(f_unityCam, camCurrent) as Camera;
                            if (unityCam != null) flyCamPos = unityCam.transform.position;
                        }
                    }
                }
                catch { }

                if (_cachedIsFly)
                {
                    _cachedCamPos = flyCamPos;
                }
                else
                {
                    if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
                    {
                        _cachedCamera = null;
                        try
                        {
                            object camObj = GetVal(f_camMe, null);
                            if (camObj is Component cc)
                            {
                                var found = cc.GetComponentInChildren<Camera>(true);
                                if (found != null && found.enabled && found.gameObject.activeInHierarchy)
                                    _cachedCamera = found;
                            }
                        }
                        catch { }
                        if (_cachedCamera == null)
                            foreach (Camera cam in Camera.allCameras)
                                if (cam.enabled && cam.gameObject.activeInHierarchy)
                                { _cachedCamera = cam; break; }
                    }
                    _cachedCamPos = _cachedCamera != null ? _cachedCamera.transform.position : Vector3.zero;
                }
            }

            void DrawPosition(Rect content, object player)
            {
                LegacyUILayout ui = new LegacyUILayout(content, 5f);
                Vector3 pos = _cachedIsFly ? _cachedCamPos : _cachedPlayerPos;
                LegacyUI.MiniHint(ui.Row(16f), _cachedIsFly ? "Free Camera" : "Player");
                LegacyUI.Label(ui.Row(20f), $"X:  {pos.x:F1}");
                LegacyUI.Label(ui.Row(20f), $"Y:  {pos.y:F1}");
                LegacyUI.Label(ui.Row(20f), $"Z:  {pos.z:F1}");
                ui.Space(4f);
                string playerBtn = $"Player  {_cachedPlayerPos.x:F1} {_cachedPlayerPos.y:F1} {_cachedPlayerPos.z:F1}";
                string cameraBtn = $"Camera  {_cachedCamPos.x:F1} {_cachedCamPos.y:F1} {_cachedCamPos.z:F1}";
                if (LegacyUI.Button(ui.Row(26f), playerBtn))
                {
                    string s = $"{_cachedPlayerPos.x:F2}, {_cachedPlayerPos.y:F2}, {_cachedPlayerPos.z:F2}";
                    GUIUtility.systemCopyBuffer = s;
                    Log.LogInfo($"[Position] Player: {s}");
                }
                ui.Space(4f);
                if (LegacyUI.Button(ui.Row(26f), cameraBtn))
                {
                    string s = $"{_cachedCamPos.x:F2}, {_cachedCamPos.y:F2}, {_cachedCamPos.z:F2}";
                    GUIUtility.systemCopyBuffer = s;
                    Log.LogInfo($"[Position] Camera: {s}");
                }
            }

            void DrawPerf(Rect content)
            {
                LegacyUILayout ui = new LegacyUILayout(content, 5f);
                LegacyUI.Label(ui.Row(20f), $"FPS:       {_fps:F1}");
                LegacyUI.Label(ui.Row(20f), $"Frame ms:  {_frameTimeMs:F2}");
                LegacyUI.Label(ui.Row(20f), $"Low FPS%:  {_lowFpsPct:F1}%");
                LegacyUI.Label(ui.Row(20f), $"Mono MB:   {(System.GC.GetTotalMemory(false) / 1048576f):F0}");
                ui.Space(4f);
                LegacyUI.ProgressBar(ui.Row(16f), _fps, 0d, 120f);
            }

            void DrawSession(Rect content)
            {
                float e = Time.realtimeSinceStartup - _sessionStart;
                int h = (int)(e / 3600), m = (int)(e % 3600 / 60), s = (int)(e % 60);
                LegacyUI.Label(content, $"⏱  {h:D2}:{m:D2}:{s:D2}");
            }

            // ═════════════════════════════════════════════════════════════════
            // НАСТРОЙКИ — окно со скроллом
            // ═════════════════════════════════════════════════════════════════
            void DrawSettings(Rect content)
            {
                // Обновляем размер и позицию ScrollView под контент окна
                _settingsScroll.ViewRect = content;

                _settingsScroll.Draw(inner =>
                {
                    LegacyUILayout ui = new LegacyUILayout(inner, 6f);

                    LegacyUI.Title(ui.Row(20f), "Widget appearance");
                    bool blur = LegacyUI.Toggle(ui.Row(22f), _blur.Value, "Backdrop Blur");
                    if (blur != _blur.Value) _blur.Value = blur;
                    ConfigEntry<float> alpha = blur ? _blurAlpha : _plainAlpha;
                    LegacyUI.Label(ui.Row(20f), $"Background opacity: {alpha.Value:0.00}");
                    float nextAlpha = LegacyUI.HorizontalSlider(ui.Row(18f), alpha.Value, 0f, 1f);
                    if (!Mathf.Approximately(nextAlpha, alpha.Value)) alpha.Value = nextAlpha;
                    ui.Space(6f);

                    // ── HUD Widgets ───────────────────────────────────────────
                    LegacyUI.Title(ui.Row(20f), "Toggle widgets:");
                    ui.Space(2f);
                    LegacyUI.Separator(ui.Row(1f));
                    ui.Space(4f);

                    for (int i = 0; i < WIDGET_COUNT; i++)
                    {
                        bool cur = _windows[i].Visible;
                        bool next = LegacyUI.Toggle(ui.Row(22f), cur, WIDGET_NAMES[i]);
                        if (next != cur) _windows[i].Visible = next;
                    }

                    ui.Space(6f);
                    LegacyUI.Separator(ui.Row(1f));
                    ui.Space(6f);

                    // ── Compass toggle ────────────────────────────────────────
                    _compassVisible = LegacyUI.Toggle(ui.Row(22f), _compassVisible, "CompassHUD");

                    // ── Compass Settings (сворачиваемая секция) ───────────────
                    if (LegacyUI.Button(ui.Row(22f), (_compassSettingsExpanded ? "▼" : "▶") + "  Compass Settings"))
                        _compassSettingsExpanded = !_compassSettingsExpanded;

                    if (_compassSettingsExpanded)
                    {
                        ui.Space(2f);

                        // Отображение
                        LegacyUI.MiniHint(ui.Row(14f), "— Display —");
                        _showDegrees = LegacyUI.Toggle(ui.Row(20f), _showDegrees, "Degrees on bar");
                        _showCardinals = LegacyUI.Toggle(ui.Row(20f), _showCardinals, "N / E / S / W");
                        _showMarkerLine = LegacyUI.Toggle(ui.Row(20f), _showMarkerLine, "Center marker");
                        _showCurrentDeg = LegacyUI.Toggle(ui.Row(20f), _showCurrentDeg, "Current degree");
                        _showTickLines = LegacyUI.Toggle(ui.Row(20f), _showTickLines, "Tick lines");
                        _showBorder = LegacyUI.Toggle(ui.Row(20f), _showBorder, "Border");

                        ui.Space(4f);

                        // Позиция и размер
                        LegacyUI.MiniHint(ui.Row(14f), "— Position & Size —");
                        LegacyUI.Label(ui.Row(16f), $"Y: {_compassY:F0}");
                        _compassY = LegacyUI.HorizontalSlider(ui.Row(14f), _compassY, 0f, 400f);
                        LegacyUI.Label(ui.Row(16f), $"Offset X: {_compassX:F0}");
                        _compassX = LegacyUI.HorizontalSlider(ui.Row(14f), _compassX, -400f, 400f);
                        LegacyUI.Label(ui.Row(16f), $"Width: {_compassWidth:F0}");
                        _compassWidth = LegacyUI.HorizontalSlider(ui.Row(14f), _compassWidth, 150f, Screen.width);
                        LegacyUI.Label(ui.Row(16f), $"Height: {_compassHeight:F0}");
                        _compassHeight = LegacyUI.HorizontalSlider(ui.Row(14f), _compassHeight, 20f, 80f);
                        LegacyUI.Label(ui.Row(16f), $"Degree step: {_degreeStep:F0}");
                        _degreeStep = LegacyUI.HorizontalSlider(ui.Row(14f), _degreeStep, 10f, 90f);
                        LegacyUI.Label(ui.Row(16f), $"Field of view: {_fieldOfView:F0}");
                        _fieldOfView = LegacyUI.HorizontalSlider(ui.Row(14f), _fieldOfView, 30f, 180f);

                        ui.Space(4f);

                        // Шрифты
                        LegacyUI.MiniHint(ui.Row(14f), "— Fonts —");
                        LegacyUI.Label(ui.Row(16f), $"N/E/S/W size: {_fontSizeBig:F0}");
                        _fontSizeBig = LegacyUI.HorizontalSlider(ui.Row(14f), _fontSizeBig, 8f, 36f);
                        LegacyUI.Label(ui.Row(16f), $"Degree size: {_fontSizeSmall:F0}");
                        _fontSizeSmall = LegacyUI.HorizontalSlider(ui.Row(14f), _fontSizeSmall, 6f, 28f);
                        LegacyUI.Label(ui.Row(16f), $"Current deg size: {_fontSizeDeg:F0}");
                        _fontSizeDeg = LegacyUI.HorizontalSlider(ui.Row(14f), _fontSizeDeg, 8f, 30f);
                        LegacyUI.Label(ui.Row(16f), $"Border thickness: {_borderThickness:F0}");
                        _borderThickness = LegacyUI.HorizontalSlider(ui.Row(14f), _borderThickness, 1f, 6f);

                        ui.Space(4f);

                        // Цвета
                        LegacyUI.MiniHint(ui.Row(14f), "— Colors —");
                        DrawColorSliders(ref ui, "North (N)", ref _colorNorth);
                        DrawColorSliders(ref ui, "Cardinals E/S/W", ref _colorCardinal);
                        DrawColorSliders(ref ui, "Degrees", ref _colorDegree);
                        DrawColorSliders(ref ui, "Center marker", ref _colorMarker);
                        DrawColorSliders(ref ui, "Ticks big", ref _colorTickBig);
                        DrawColorSliders(ref ui, "Ticks small", ref _colorTickSmall);
                        DrawColorSliders(ref ui, "Current degree", ref _colorCurrentDeg);
                        DrawColorSliders(ref ui, "Border", ref _colorBorder);


                        ui.Space(4f);
                    }

                    // ── Кнопки ────────────────────────────────────────────────
                    LegacyUI.Separator(ui.Row(1f));
                    ui.Space(6f);

                    if (LegacyUI.Button(ui.Row(26f), "↺  Reset Positions"))
                        ResetPositions();

                    ui.Space(4f);
                    LegacyUI.MiniHint(ui.Row(18f), "Alt+X — toggle this menu");
                });
            }

            void DrawColorSliders(ref LegacyUILayout ui, string label, ref Color col)
            {
                LegacyUI.Label(ui.Row(14f), label);
                Rect row = ui.Row(14f);
                var left = ui.SplitLeft(row, 14f);
                var right = ui.SplitRight(row, 14f, 4f);
                LegacyUI.Label(left, "R"); col.r = LegacyUI.HorizontalSlider(right, col.r, 0f, 1f);
                row = ui.Row(14f);
                left = ui.SplitLeft(row, 14f); right = ui.SplitRight(row, 14f, 4f);
                LegacyUI.Label(left, "G"); col.g = LegacyUI.HorizontalSlider(right, col.g, 0f, 1f);
                row = ui.Row(14f);
                left = ui.SplitLeft(row, 14f); right = ui.SplitRight(row, 14f, 4f);
                LegacyUI.Label(left, "B"); col.b = LegacyUI.HorizontalSlider(right, col.b, 0f, 1f);
                ui.Space(2f);
            }

            void ResetPositions()
            {
                int sw = Mathf.RoundToInt(Screen.width / _scale.ResolveScale()), sh = Mathf.RoundToInt(Screen.height / _scale.ResolveScale());
                _windows[W_VEHICLE].Rect = new Rect(20, sh - 285, 260, 265);
                _windows[W_HEALTH].Rect = new Rect(20, 20, 240, 165);
                _windows[W_WEAPONS].Rect = new Rect(sw - 260, 20, 240, 240);
                _windows[W_POSITION].Rect = new Rect(sw - 260, 270, 240, 235);
                _windows[W_PERF].Rect = new Rect(sw - 260, sh - 210, 240, 190);
                _windows[W_SESSION].Rect = new Rect(sw / 2 - 110, sh - 110, 220, 90);
                _windows[W_CLOCK].Rect = new Rect(sw / 2 - 110, 20, 220, 90);
            }
        }
    }
}
