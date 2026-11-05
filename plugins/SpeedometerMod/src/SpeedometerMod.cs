using BepInEx;
using BepInEx.Configuration;
using BigCityLegacy.UI;
using UnityEngine;
using System;
using System.Reflection;

namespace SpeedometerMod
{
    [BepInPlugin("com.user_rp.speedometer", "Speedometer Mod", "1.3.1")]
    [BepInDependency("com.bigcitylegacy.baseplugin", "1.4.0")]

    public class SpeedometerPlugin : BaseUnityPlugin
    {
        private const float SettingsWindowHeight = 320f;
        private const string SettingsWindowPrefsKey = "SpeedometerMod.Window";
        private float displaySpeed = 0f;
        private float baseMultiplier = 3.6f;

        private int displayMode = 0;
        private int positionMode = 1;
        private int languageMode = 0;

        private bool isRebinding = false;
        private bool isActuallyInCar = false;

        private ConfigEntry<KeyboardShortcut> menuKeyConfig;
        private ConfigEntry<int> displayModeConfig;
        private ConfigEntry<int> positionModeConfig;
        private ConfigEntry<int> languageModeConfig;
        private KeyboardShortcut menuKey;
        private int rebindAfterFrame;
        private readonly PluginUiSupport uiSupport = new PluginUiSupport();
        private LegacyUIDropdown positionDropdown, displayDropdown, languageDropdown;

        private Texture2D whiteTex;
        private Texture2D redTex;
        private Texture2D gradientBgTex;

        private GUIStyle _speedTextStyle;
        private GUIStyle _kmhStyle;
        private GUIStyle _tickTextStyle;

        private LegacyUIWindow window;
        private LegacyUIScaleSettings scaleSettings;
        private readonly LegacyUIAnimationSettings animations = new LegacyUIAnimationSettings();

        private void Awake()
        {
            whiteTex = MakeTex(1, 1, Color.white);
            redTex = MakeTex(1, 1, new Color(0.9f, 0.1f, 0.1f));
            gradientBgTex = CreateGradientTex(184, 138);

            menuKeyConfig = Config.Bind("Settings", "MenuShortcut", new KeyboardShortcut(KeyCode.S, KeyCode.LeftAlt), "Комбинация открытия меню. Escape отменяет переназначение.");
            displayModeConfig = Config.Bind("Settings", "DisplayMode", 0, "Режим");
            positionModeConfig = Config.Bind("Settings", "PositionMode", 1, "Позиция");
            languageModeConfig = Config.Bind("Settings", "LanguageMode", 0, "Язык (0-RU, 1-EN, 2-CN)");

            menuKey = menuKeyConfig.Value;
            displayMode = Mathf.Clamp(displayModeConfig.Value, 0, 4);
            positionMode = Mathf.Clamp(positionModeConfig.Value, 0, 2);
            languageMode = Mathf.Clamp(languageModeConfig.Value, 0, 2);

            LegacyInput.Init();
            scaleSettings = new LegacyUIScaleSettings();

            // LegacyUIWindow restores saved dimensions on its first Draw. Upgrade
            // an older height before that restore while preserving position and visibility.
            string heightKey = SettingsWindowPrefsKey + ".h";
            if (PlayerPrefs.HasKey(heightKey) && PlayerPrefs.GetFloat(heightKey) < SettingsWindowHeight)
            {
                PlayerPrefs.SetFloat(heightKey, SettingsWindowHeight);
                PlayerPrefs.Save();
            }

            window = new LegacyUIWindow(
                "Speedometer Mod",
                new Rect(120f, 90f, 300f, SettingsWindowHeight),
                new LegacyUIWindowOptions
                {
                    ShowCloseButton = true,
                    RightHint = "[" + menuKey + "]",
                    PlayerPrefsKey = SettingsWindowPrefsKey,
                    FadeWhileDragging = true,
                    DragAlpha = 0.65f,
                    InputBlockMode = LegacyUIInputBlockMode.Window
                });
            window.Visible = false;
            window.Closed += () => { isRebinding = false; CloseDropdowns(); };
            positionDropdown = new LegacyUIDropdown(new string[3], positionMode);
            displayDropdown = new LegacyUIDropdown(new string[5], displayMode);
            languageDropdown = new LegacyUIDropdown(new[] { "Русский", "English", "中文" }, languageMode);
            positionDropdown.SelectionChanged += index => { positionMode = index; positionModeConfig.Value = index; };
            displayDropdown.SelectionChanged += index => { displayMode = index; displayModeConfig.Value = index; };
            languageDropdown.SelectionChanged += index => { languageMode = index; languageModeConfig.Value = index; };
        }

        private void CloseDropdowns()
        {
            if (positionDropdown == null) return;
            positionDropdown.IsOpen = displayDropdown.IsOpen = languageDropdown.IsOpen = false;
        }

        private void OnDisable() { uiSupport.Dispose(); }
        private void OnDestroy() { uiSupport.Dispose(); }

        private void Update()
        {
            bool gameplay = LegacyHelpers.IsGameplayRunning;
            uiSupport.SetCursor(this, gameplay && window.Visible);
            if (!gameplay) { isRebinding = false; CloseDropdowns(); return; }

            if (isRebinding)
            {
                if (Time.frameCount > rebindAfterFrame && Input.anyKeyDown)
                    foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (!Input.GetKeyDown(key)) continue;
                        if (key == KeyCode.Escape) { isRebinding = false; break; }
                        if (key == KeyCode.None || MenuShortcut.IsModifier(key) || (int)key >= (int)KeyCode.Mouse0) continue;
                        menuKey = MenuShortcut.Capture(key);
                        menuKeyConfig.Value = menuKey;
                        window.Options.RightHint = "[" + menuKey + "]";
                        Config.Save();
                        isRebinding = false;
                        break;
                    }
                return;
            }

            if (MenuShortcut.IsDown(menuKey))
            {
                window.Visible = !window.Visible;
                if (!window.Visible) CloseDropdowns();
                uiSupport.SetCursor(this, window.Visible);
            }

            Rigidbody body = GetPlayerCarBody();
            isActuallyInCar = (body != null);

            if (isActuallyInCar)
            {
                float highestSpeed = body.velocity.magnitude;
                float rawSpeed = highestSpeed * baseMultiplier;
                float finalSpeed = rawSpeed;

                if (rawSpeed > 30f) finalSpeed = rawSpeed * 1.05f;
                if (rawSpeed > 50f) finalSpeed = rawSpeed * 1.1f;
                if (rawSpeed > 70f) finalSpeed = rawSpeed * 1.12f;
                if (rawSpeed > 90f) finalSpeed = rawSpeed * 1.14f;
                if (rawSpeed > 140f) finalSpeed = rawSpeed * 1.26f;
                if (rawSpeed > 170f) finalSpeed = rawSpeed * 1.35f;
                if (rawSpeed > 200f) finalSpeed = finalSpeed * 1.34f;

                displaySpeed = Mathf.Lerp(displaySpeed, finalSpeed, Time.deltaTime * 10f);
            }
            else
            {
                displaySpeed = Mathf.Lerp(displaySpeed, 0f, Time.deltaTime * 10f);
            }
        }

        private Rigidbody GetPlayerCarBody()
        {
            try
            {
                Type pcType = Type.GetType("PlayerControl, Assembly-CSharp");
                if (pcType == null) return null;
                var prop = pcType.GetProperty("curPlyaer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var player = prop?.GetValue(null) as Component;
                if (player == null) return null;
                var parentField = player.GetType().GetField("parent_control", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var parentControl = parentField?.GetValue(player) as Component;
                if (parentControl == null) return null;
                var bodyField = parentControl.GetType().GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return bodyField?.GetValue(parentControl) as Rigidbody;
            }
            catch { return null; }
        }

        private void OnGUI()
        {
            if (!LegacyHelpers.IsGameplayRunning) return;
            if (isActuallyInCar)
            {
                if (displayMode == 0) DrawCustomSpeedometer();
                else if (displayMode == 1) DrawOnlyNumbers();
                else if (displayMode == 2) DrawChineseSpeedometer();
                else if (displayMode == 3) DrawDigitalGauge();
            }

            if (window == null || !window.IsRendered) return;

            using (new LegacyUIGuiScope(-10000, scaleSettings, animations, uiSupport.Backdrop))
            {
                positionDropdown.HandlePopupInput();
                displayDropdown.HandlePopupInput();
                languageDropdown.HandlePopupInput();
                window.Draw(DrawWindowContent);
                if (window.Visible)
                {
                    positionDropdown.DrawPopup();
                    displayDropdown.DrawPopup();
                    languageDropdown.DrawPopup();
                }
            }
        }

        private void DrawWindowContent(Rect content)
        {
            LegacyUILayout ui = new LegacyUILayout(content);

            positionDropdown.Items = new[] { GetText("Слева", "Left", "左边"), GetText("Центр", "Center", "中心"), GetText("Справа", "Right", "右边") };
            displayDropdown.Items = new[] { GetText("Точный", "Precise", "精确"), GetText("Цифры", "Numbers", "数字"), GetText("Реалистичный", "Realistic", "真实"), GetText("Цифровой", "Digital", "数码"), GetText("Выключен", "Disabled", "关闭") };
            LegacyUI.Label(ui.Row(18f), GetText("Позиция", "Position", "位置"));
            DrawDropdown(positionDropdown, ui.Row(30f));
            LegacyUI.Label(ui.Row(18f), GetText("Вид", "View", "视图"));
            DrawDropdown(displayDropdown, ui.Row(30f));
            LegacyUI.Label(ui.Row(18f), GetText("Язык", "Language", "语言"));
            DrawDropdown(languageDropdown, ui.Row(30f));
            string bindText = isRebinding ? GetText("Нажмите комбинацию (Esc: отмена)", "Press shortcut (Esc: cancel)", "请按组合键 (Esc: 取消)") : GetText("Клавиши меню: ", "Menu shortcut: ", "菜单快捷键: ") + menuKey;
            if (LegacyUI.Button(ui.Row(30f), bindText))
            { CloseDropdowns(); isRebinding = true; rebindAfterFrame = Time.frameCount; }

            ui.Space(5f);
            LegacyUI.DangerButton(ui.Row(28f), "by UserRP / 一只car");
        }

        private string GetText(string ru, string en, string cn)
        {
            if (languageMode == 0) return ru;
            if (languageMode == 1) return en;
            return cn;
        }

        private void DrawDropdown(LegacyUIDropdown dropdown, Rect rect)
        {
            bool open = dropdown.IsOpen;
            dropdown.Draw(rect);
            if (!open && dropdown.IsOpen)
            {
                if (dropdown != positionDropdown) positionDropdown.IsOpen = false;
                if (dropdown != displayDropdown) displayDropdown.IsOpen = false;
                if (dropdown != languageDropdown) languageDropdown.IsOpen = false;
            }
        }

        // === 4-Й ВИД СПИДОМЕТРА ===
        private void DrawDigitalGauge()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 1.5f) * 0.98f;
            float width = 400f * scale;
            float height = 280f * scale;

            float x = 50f * scale;
            if (positionMode == 1) x = (Screen.width - width) / 2f;
            else if (positionMode == 2) x = Screen.width - width - 50f * scale;

            float y = Screen.height - height - 100f * scale;

            Vector2 center = new Vector2(x + 120f * scale, y + 140f * scale);

            if (_speedTextStyle == null) InitChineseStyles(scale);

            // 1. ШКАЛА
            float radius = 100f * scale;
            int totalTicks = 11;

            for (int i = 0; i < totalTicks; i++)
            {
                float normalized = (float)i / (totalTicks - 1);
                float tickAngle = Mathf.Lerp(240f, 0f, normalized);
                float rad = tickAngle * Mathf.Deg2Rad;

                Color zoneColor = (i >= 8) ? new Color(0.8f, 0.1f, 0.2f) : Color.white;

                float tickLength = 15f * scale;
                float tickWidth = 3f * scale;

                float startX = center.x + Mathf.Cos(rad) * radius;
                float startY = center.y - Mathf.Sin(rad) * radius;
                float endX = center.x + Mathf.Cos(rad) * (radius - tickLength);
                float endY = center.y - Mathf.Sin(rad) * (radius - tickLength);

                DrawLine(new Vector2(startX, startY), new Vector2(endX, endY), whiteTex, tickWidth, zoneColor);

                float textRadius = radius + 15f * scale;
                float tx = center.x + Mathf.Cos(rad) * textRadius;
                float ty = center.y - Mathf.Sin(rad) * textRadius;

                GUIStyle tickText = new GUIStyle(_tickTextStyle);
                tickText.fontSize = Mathf.RoundToInt(16f * scale);
                tickText.normal.textColor = zoneColor;
                GUI.Label(new Rect(tx - 15f * scale, ty - 10f * scale, 30f * scale, 20f * scale), i.ToString(), tickText);
            }

            // 2. ЦИФРОВОЙ СПИДОМЕТР
            string speedStr = displaySpeed.ToString("0");
            GUIStyle bigSpeedStyle = new GUIStyle(_speedTextStyle);
            bigSpeedStyle.fontSize = Mathf.RoundToInt(66f * scale);
            bigSpeedStyle.alignment = TextAnchor.MiddleLeft;
            bigSpeedStyle.fontStyle = FontStyle.BoldAndItalic;

            // ★ Цифры приподняли вверх
            GUI.Label(new Rect(center.x + 20f * scale, center.y + 10f * scale, 200f * scale, 100f * scale), speedStr, bigSpeedStyle);

            // ★ KM/H опустили еще ниже, чтобы точно не пересекалось
            GUIStyle smallKmh = new GUIStyle(_kmhStyle);
            smallKmh.fontSize = Mathf.RoundToInt(14f * scale);
            smallKmh.alignment = TextAnchor.MiddleLeft;
            smallKmh.normal.textColor = Color.white;
            GUI.Label(new Rect(center.x + 23f * scale, center.y + 80f * scale, 200f * scale, 30f * scale), "KM/H", smallKmh);

            // 3. КРУГ С ПЕРЕДАЧЕЙ
            string gear = "N";
            if (displaySpeed >= 1f) gear = "1";
            if (displaySpeed >= 30f) gear = "2";
            if (displaySpeed >= 60f) gear = "3";
            if (displaySpeed >= 100f) gear = "4";
            if (displaySpeed >= 150f) gear = "5";
            if (displaySpeed >= 220f) gear = "6";

            float gearRadius = 22f * scale;

            DrawFilledCircle(center, gearRadius, new Color(0f, 0.7f, 0.8f), 36);
            DrawCircleOutline(center, gearRadius, 1.5f * scale, 36);

            GUIStyle gearStyle = new GUIStyle(_speedTextStyle);
            gearStyle.fontSize = Mathf.RoundToInt(28f * scale);
            gearStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(center.x - gearRadius, center.y - gearRadius, gearRadius * 2, gearRadius * 2), gear, gearStyle);

            // 4. СТРЕЛКА
            float speedPercent = Mathf.Clamp01(displaySpeed / 300f);
            float arrowAngle = Mathf.Lerp(240f, 0f, speedPercent);
            float arrowRad = arrowAngle * Mathf.Deg2Rad;

            float arrowStartRadius = gearRadius + 2f * scale;
            float arrowEndRadius = radius - 8f * scale;

            float ax1 = center.x + Mathf.Cos(arrowRad) * arrowStartRadius;
            float ay1 = center.y - Mathf.Sin(arrowRad) * arrowStartRadius;
            float ax2 = center.x + Mathf.Cos(arrowRad) * arrowEndRadius;
            float ay2 = center.y - Mathf.Sin(arrowRad) * arrowEndRadius;

            DrawLine(new Vector2(ax1, ay1), new Vector2(ax2, ay2), whiteTex, 3f * scale, Color.white);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(ax2 - 4f * scale, ay2 - 4f * scale, 8f * scale, 8f * scale), Texture2D.whiteTexture);
        }

        private void DrawCircleOutline(Vector2 center, float radius, float thickness, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * Mathf.PI * 2;
                float a2 = (float)(i + 1) / segments * Mathf.PI * 2;
                Vector2 p1 = new Vector2(center.x + Mathf.Cos(a1) * radius, center.y + Mathf.Sin(a1) * radius);
                Vector2 p2 = new Vector2(center.x + Mathf.Cos(a2) * radius, center.y + Mathf.Sin(a2) * radius);
                DrawLine(p1, p2, whiteTex, thickness, Color.white);
            }
        }

        private void DrawFilledCircle(Vector2 center, float radius, Color color, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = (float)i / segments * Mathf.PI * 2;
                float a2 = (float)(i + 1) / segments * Mathf.PI * 2;
                float midA = (a1 + a2) / 2f;
                Vector2 edge = new Vector2(center.x + Mathf.Cos(midA) * radius, center.y + Mathf.Sin(midA) * radius);
                DrawLine(center, edge, whiteTex, (2f * Mathf.PI * radius / segments) * 1.2f, color);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Texture2D tex, float width, Color color)
        {
            Vector2 d = end - start;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;

            GUIUtility.RotateAroundPivot(angle, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y - width / 2, d.magnitude, width), tex);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        // (Остальные методы без изменений)
        private void DrawChineseSpeedometer()
        {
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 1.5f) * 0.85f;
            float width = 360f * scale;
            float height = 360f * scale;

            float x = 50f * scale;
            if (positionMode == 1) x = (Screen.width - width) / 2f;
            else if (positionMode == 2) x = Screen.width - width - 50f * scale;

            float y = Screen.height - height - 100f * scale;
            Vector2 center = new Vector2(x + width / 2f, y + height / 2f);

            if (_speedTextStyle == null) InitChineseStyles(scale);

            string speedStr = displaySpeed.ToString("0");
            GUI.Label(new Rect(x, y + height - 113 * scale, width, 65f * scale), speedStr, _speedTextStyle);
            GUI.Label(new Rect(x, y + height - 50 * scale, width, 40f * scale), "KM/H", _kmhStyle);

            float normalizedSpeed = Mathf.Clamp01(displaySpeed / 300f);
            float angle = Mathf.Lerp(-135f, 135f, normalizedSpeed);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);

            float needleWidth = 8f * scale;
            float needleLength = height * 0.38f;

            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(center.x - needleWidth / 2f, center.y - needleLength, needleWidth, needleLength), Texture2D.whiteTexture);

            float dotSize = 24f * scale;
            GUI.DrawTexture(new Rect(center.x - dotSize / 2f, center.y - dotSize / 2f, dotSize, dotSize), Texture2D.whiteTexture);

            GUI.matrix = oldMatrix;
            GUI.color = Color.white;

            int totalTicks = 15;
            for (int i = 0; i <= totalTicks; i++)
            {
                float t = (float)i / totalTicks;
                float tickAngle = Mathf.Lerp(-135f, 135f, t);
                int speedValue = i * 20;

                bool isLargeText = (i % 2 != 0);
                float tickLength = isLargeText ? 24f * scale : 12f * scale;
                float tickWidth = isLargeText ? 5f * scale : 3f * scale;

                Matrix4x4 tickMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(tickAngle, center);

                float r = width * 0.38f;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(center.x - tickWidth / 2f, center.y - r, tickWidth, tickLength), Texture2D.whiteTexture);

                GUI.matrix = tickMatrix;

                float rad = tickAngle * Mathf.Deg2Rad;
                float textRadius = r + 32f * scale;
                float tx = center.x + Mathf.Sin(rad) * textRadius;
                float ty = center.y - Mathf.Cos(rad) * textRadius;

                _tickTextStyle.fontSize = Mathf.RoundToInt((isLargeText ? 26f : 18f) * scale);
                _tickTextStyle.fontStyle = isLargeText ? FontStyle.Bold : FontStyle.Normal;

                GUI.Label(new Rect(tx - 40f * scale, ty - 15f * scale, 80f * scale, 30f * scale), speedValue.ToString(), _tickTextStyle);
            }
            GUI.color = Color.white;
        }

        private void InitChineseStyles(float scale)
        {
            _speedTextStyle = new GUIStyle();
            _speedTextStyle.fontSize = Mathf.RoundToInt(65f * scale);
            _speedTextStyle.fontStyle = FontStyle.Bold;
            _speedTextStyle.alignment = TextAnchor.MiddleCenter;
            _speedTextStyle.normal.textColor = Color.white;

            _kmhStyle = new GUIStyle(_speedTextStyle);
            _kmhStyle.fontSize = Mathf.RoundToInt(36f * scale);

            _tickTextStyle = new GUIStyle();
            _tickTextStyle.alignment = TextAnchor.MiddleCenter;
            _tickTextStyle.normal.textColor = Color.white;
        }

        private void DrawCustomSpeedometer()
        {
            string speedText = displaySpeed.ToString("0");
            GUIStyle speedStyle = new GUIStyle();
            speedStyle.fontStyle = FontStyle.BoldAndItalic;
            speedStyle.normal.textColor = Color.white;
            GUIStyle unitStyle = new GUIStyle();
            unitStyle.fontSize = 12;
            unitStyle.fontStyle = FontStyle.Bold;
            unitStyle.normal.textColor = Color.gray;

            float screenX = Screen.width / 2f;
            if (positionMode == 0) screenX = 150f;
            if (positionMode == 2) screenX = Screen.width - 150f;

            float width = 184f;
            float height = 138f;
            float posX = screenX - (width / 2f);
            float posY = Screen.height - height - 10f;

            GUI.DrawTexture(new Rect(posX, posY, width, height), gradientBgTex);

            Vector2 center = new Vector2(posX + width / 2f, posY + height - 23f);
            float radius = 57.5f;

            GUIStyle numberStyle = new GUIStyle();
            numberStyle.fontSize = 11;
            numberStyle.fontStyle = FontStyle.Bold;
            numberStyle.alignment = TextAnchor.MiddleCenter;

            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.Lerp(150f, 30f, i / 10f);
                float rad = angle * Mathf.Deg2Rad;
                float x1 = center.x + Mathf.Cos(rad) * (radius - 6f);
                float y1 = center.y - Mathf.Sin(rad) * (radius - 6f);
                float x2 = center.x + Mathf.Cos(rad) * radius;
                float y2 = center.y - Mathf.Sin(rad) * radius;
                Texture2D lineColor = (i >= 8) ? redTex : whiteTex;
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), lineColor, 2f, Color.white);
                float textX = center.x + Mathf.Cos(rad) * (radius + 12f) - 10f;
                float textY = center.y - Mathf.Sin(rad) * (radius + 12f) - 10f;
                numberStyle.normal.textColor = (i >= 8) ? new Color(0.9f, 0.1f, 0.1f) : Color.gray;
                GUI.Label(new Rect(textX, textY, 20, 20), i.ToString(), numberStyle);
            }

            float speedPercent = Mathf.Clamp01(displaySpeed / 300f);
            float arrowAngle = Mathf.Lerp(150f, 30f, speedPercent);
            float arrowRad = arrowAngle * Mathf.Deg2Rad;
            Vector2 arrowEnd = new Vector2(center.x + Mathf.Cos(arrowRad) * (radius - 12f), center.y - Mathf.Sin(arrowRad) * (radius - 12f));
            DrawLine(center, arrowEnd, whiteTex, 2f, Color.white);

            string gear = "N";
            if (displaySpeed >= 1f) gear = "1";
            if (displaySpeed >= 30f) gear = "2";
            if (displaySpeed >= 60f) gear = "3";
            if (displaySpeed >= 100f) gear = "4";
            if (displaySpeed >= 150f) gear = "5";
            if (displaySpeed >= 220f) gear = "6";

            GUIStyle gearStyle = new GUIStyle();
            gearStyle.fontSize = 20;
            gearStyle.fontStyle = FontStyle.Bold;
            gearStyle.normal.textColor = Color.green;
            gearStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(center.x - 10, center.y - 12, 20, 20), gear, gearStyle);

            speedStyle.fontSize = 25;
            speedStyle.alignment = TextAnchor.MiddleRight;
            unitStyle.alignment = TextAnchor.MiddleRight;

            GUI.Label(new Rect(posX - 5, posY + height - 52, width - 5, 25), speedText, speedStyle);
            GUI.Label(new Rect(posX - 5, posY + height - 28, width - 5, 15), "KM/H", unitStyle);
        }

        private void DrawOnlyNumbers()
        {
            string speedText = displaySpeed.ToString("0");
            GUIStyle speedStyle = new GUIStyle();
            speedStyle.fontSize = 60;
            speedStyle.fontStyle = FontStyle.BoldAndItalic;
            speedStyle.normal.textColor = Color.white;
            speedStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle unitStyle = new GUIStyle();
            unitStyle.fontSize = 20;
            unitStyle.fontStyle = FontStyle.Bold;
            unitStyle.normal.textColor = Color.gray;
            unitStyle.alignment = TextAnchor.MiddleCenter;

            float screenX = Screen.width / 2f;
            if (positionMode == 0) screenX = 150f;
            if (positionMode == 2) screenX = Screen.width - 150f;

            float posX = screenX - 100f;
            float posY = Screen.height - 120f;

            GUI.Label(new Rect(posX, posY, 200, 80), speedText, speedStyle);
            GUI.Label(new Rect(posX, posY + 60, 200, 30), "KM/H", unitStyle);
        }

        private Texture2D CreateGradientTex(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pix = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float alpha = Mathf.Lerp(0.7f, 0.0f, (float)y / height);
                for (int x = 0; x < width; x++)
                {
                    float edgeX = Mathf.Min(x, width - 1 - x) / 10f;
                    float edgeY = Mathf.Min(y, height - 1 - y) / 10f;
                    float edgeFade = Mathf.Clamp01(Mathf.Min(edgeX, edgeY));
                    pix[y * width + x] = new Color(0.1f, 0.1f, 0.1f, alpha * edgeFade);
                }
            }
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}