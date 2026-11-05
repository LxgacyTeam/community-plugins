using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BigCityLegacy.UI;
using HarmonyLib;
using UnityEngine;
using static CarPaintSprites;

namespace MadOutDrift
{
    public enum DriftScoreStyle { Flat, Classic }

    [BepInPlugin(PluginGUID, "MadOut Drift Console", "1.2.2")]
    [BepInDependency("com.bigcitylegacy.baseplugin", "1.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGUID = "com.yizhicar.madoutdrift";

        // ================= 核心参数区（默认值与图三一致） =================
        public static bool EnableAll = true;
        public static bool EnableDriftScore = true;
        private static ConfigEntry<DriftScoreStyle> scoreStyleConfig;
        public static DriftScoreStyle ScoreStyle => scoreStyleConfig != null ? scoreStyleConfig.Value : DriftScoreStyle.Classic;

        public static float PowerMultiplier = DriftSettings.DefaultPowerMultiplier;
        public static float SteerAngle = DriftSettings.DefaultSteerAngle;
        public static float SteerSensitivity = DriftSettings.DefaultSteerSensitivity;
        public static float ReturnSensitivity = DriftSettings.DefaultReturnSensitivity;

        public static bool EnableSteerAssist = true;
        public static float SteerAssistStrength = DriftSettings.DefaultSteerAssistStrength;

        public static float FrontLatGrip = DriftSettings.DefaultFrontLatGrip;
        public static float FrontLongGrip = DriftSettings.DefaultFrontLongGrip;
        public static float RearLatGrip = DriftSettings.DefaultRearLatGrip;
        public static float RearLongGrip = DriftSettings.DefaultRearLongGrip;

        public static int LangIndex = 0;

        public static bool showUI = false;
        private LegacyUIWindow window;
        private readonly PluginUiSupport uiSupport = new PluginUiSupport();
        private readonly LegacyUIScaleSettings scale = new LegacyUIScaleSettings();
        private readonly LegacyUIScrollView scroll = new LegacyUIScrollView(new Rect())
            { DrawBackground = false, ShowHorizontalScrollbar = false };
        private readonly LegacyUIDropdown languages = new LegacyUIDropdown(new[] { "中文", "English", "Русский" });
        private readonly LegacyUIDropdown scoreStyles = new LegacyUIDropdown(new[] { "Flat", "Classic" });
        private readonly Dictionary<string, LegacyUICounter> counters = new Dictionary<string, LegacyUICounter>();

        private string ConfigPath;

        public static string L(string cn, string en, string ru)
        {
            if (LangIndex == 1) return en;
            if (LangIndex == 2) return ru;
            return cn;
        }

        private void Awake()
        {
            var harmony = new Harmony(PluginGUID);
            harmony.PatchAll();

            LegacyInput.Init();
            window = new LegacyUIWindow("Drift Console", new Rect(200f, 65f, 520f, 620f),
                new LegacyUIWindowOptions { ShowCloseButton = true, RightHint = "[Alt+D]",
                    InputBlockMode = LegacyUIInputBlockMode.Window, PlayerPrefsKey = "DriftConsole.Window" });
            window.Visible = false;
            window.Closed += () => { showUI = false; languages.IsOpen = scoreStyles.IsOpen = false; };
            LangIndex = Mathf.Clamp(Config.Bind("UI", "Language", 2, "0: 中文, 1: English, 2: Русский").Value, 0, 2);
            languages.SelectedIndex = LangIndex;
            languages.SelectionChanged += index => { LangIndex = index; Config.Bind("UI", "Language", 2).Value = index; };

            scoreStyleConfig = Config.Bind("UI", "ScoreStyle", DriftScoreStyle.Classic, "Drift counter style: Classic (original ring) or Flat (panel).");
            scoreStyles.SelectedIndex = (int)ScoreStyle;
            scoreStyles.SelectionChanged += index => scoreStyleConfig.Value = (DriftScoreStyle)index;

            string configDir = Path.Combine(LegacyHelpers.ModDataPath, "config");
            ConfigPath = Path.Combine(configDir, "Drift config.txt");

            ApplySettings(new DriftSettings());
            LoadConfig();
            if (!File.Exists(ConfigPath)) SaveConfig();

            gameObject.AddComponent<DriftScoreManager>();
        }

        private void Update()
        {
            bool gameplay = LegacyHelpers.IsGameplayRunning;
            if (gameplay && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetKeyDown(KeyCode.D))
                showUI = !showUI;
            window.Visible = showUI;
            if (!gameplay || !showUI) languages.IsOpen = scoreStyles.IsOpen = false;
            uiSupport.SetCursor(this, gameplay && showUI);
        }

        private void OnDisable() { uiSupport.Dispose(); SaveConfig(); }
        private void OnApplicationQuit() { SaveConfig(); }
        private void OnDestroy() { uiSupport.Dispose(); }

        private void OnGUI()
        {
            if (!LegacyHelpers.IsGameplayRunning || !window.IsRendered) return;
            using (new LegacyUIGuiScope(-10000, scale, null, uiSupport.Backdrop))
            {
                languages.HandlePopupInput();
                scoreStyles.HandlePopupInput();
                window.Draw(DrawContent);
                if (window.Visible)
                {
                    languages.DrawPopup();
                    scoreStyles.DrawPopup();
                }
            }
        }

        private void DrawContent(Rect content)
        {
            bool languageWasOpen = languages.IsOpen;
            languages.Draw(new Rect(content.x, content.y, content.width, 28f));
            if (!languageWasOpen && languages.IsOpen) scoreStyles.IsOpen = false;
            LegacyUI.Label(new Rect(content.x, content.y + 36f, 140f, 28f), L("计分板样式", "Counter style", "Стиль счётчика"));
            bool styleWasOpen = scoreStyles.IsOpen;
            scoreStyles.Draw(new Rect(content.x + 148f, content.y + 36f, content.width - 148f, 28f));
            if (!styleWasOpen && scoreStyles.IsOpen) languages.IsOpen = false;
            scroll.ViewRect = new Rect(content.x, content.y + 72f, content.width, Mathf.Max(0f, content.height - 72f));
            scroll.Draw(inner =>
            {
                var ui = new LegacyUILayout(inner, 6f);
                bool before = GUI.changed;
                GUI.changed = false;
                EnableAll = DrawToggle(ui.Row(26f), EnableAll, L("漂移模式", "Drift Mode", "Режим дрифта"));
                LegacyUI.Status(ui.Row(22f), EnableAll ? L("漂移模式已开启", "Drift mode active", "Режим дрифта включён") : L("原厂状态", "Stock mode", "Стоковый режим"));
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && EnableAll;
                LegacyUI.Title(ui.Row(24f), L("车辆动力与转向", "Power & Steering", "Мощность и руль"));
                DrawStepper(ref ui, "PowerMul", L("动力倍率", "Power multiplier", "Множитель мощности"), ref PowerMultiplier, 0.01f, 0.1f, 5f);
                DrawStepper(ref ui, "SteerAng", L("最大转向角度", "Max steer angle", "Макс. угол руля"), ref SteerAngle, 1f, 10f, 90f);
                DrawStepper(ref ui, "SteerSens", L("转向灵敏度", "Steer sensitivity", "Чувствительность руля"), ref SteerSensitivity, 0.05f, 0.1f, 5f);
                DrawStepper(ref ui, "ReturnSens", L("回正灵敏度", "Return sensitivity", "Чувствительность возврата"), ref ReturnSensitivity, 0.05f, 0.1f, 5f);
                LegacyUI.Title(ui.Row(24f), L("转向辅助", "Steering assist", "Помощь руления"));
                EnableSteerAssist = DrawToggle(ui.Row(24f), EnableSteerAssist, L("开启转向辅助", "Enable steering assist", "Включить помощь руления"));
                DrawStepper(ref ui, "AssistStr", L("辅助强度", "Assist strength", "Сила помощи"), ref SteerAssistStrength, 0.05f, 0f, 1f);
                LegacyUI.Title(ui.Row(24f), L("前轮抓地力", "Front wheel grip", "Сцепление передних колёс"));
                DrawStepper(ref ui, "FrontLat", L("横向抓地", "Lateral grip", "Поперечное сцепление"), ref FrontLatGrip, 0.01f, 0.1f, 5f);
                DrawStepper(ref ui, "FrontLong", L("纵向抓地", "Longitudinal grip", "Продольное сцепление"), ref FrontLongGrip, 0.01f, 0.1f, 5f);
                LegacyUI.Title(ui.Row(24f), L("后轮抓地力", "Rear wheel grip", "Сцепление задних колёс"));
                DrawStepper(ref ui, "RearLat", L("横向抓地", "Lateral grip", "Поперечное сцепление"), ref RearLatGrip, 0.01f, 0.1f, 5f);
                DrawStepper(ref ui, "RearLong", L("纵向抓地", "Longitudinal grip", "Продольное сцепление"), ref RearLongGrip, 0.01f, 0.1f, 5f);
                EnableDriftScore = DrawToggle(ui.Row(26f), EnableDriftScore, L("开启计分板", "Enable drift scoring", "Показывать очки дрифта"));
                GUI.enabled = enabled;
                if (LegacyUI.Button(ui.Row(30f), L("保存设置", "Save settings", "Сохранить настройки"))) SaveConfig();
                if (LegacyUI.Button(ui.Row(30f), L("恢复默认数值", "Reset defaults", "Сбросить настройки")))
                {
                    ApplySettings(new DriftSettings());
                    counters.Clear(); GUI.changed = true;
                }
                bool changed = GUI.changed;
                GUI.changed = before || changed;
                if (changed) SaveConfig();
            });
        }

        private static bool DrawToggle(Rect rect, bool value, string label)
        {
            bool next = LegacyUI.Toggle(rect, value, label);
            if (next != value) GUI.changed = true;
            return next;
        }

        private void DrawStepper(ref LegacyUILayout ui, string id, string label, ref float value, float step, float min, float max)
        {
            LegacyUI.Label(ui.Row(20f), label);
            LegacyUICounter counter;
            if (!counters.TryGetValue(id, out counter))
            { counter = new LegacyUICounter(value, step, min, max); counters.Add(id, counter); }
            counter.Value = value;
            float next = counter.Draw(ui.Row(28f));
            if (!Mathf.Approximately(value, next)) { value = next; GUI.changed = true; }
        }

        private static DriftSettings CaptureSettings()
        {
            return new DriftSettings
            {
                EnableAll = EnableAll, EnableDriftScore = EnableDriftScore, EnableSteerAssist = EnableSteerAssist,
                PowerMultiplier = PowerMultiplier, SteerAngle = SteerAngle,
                SteerSensitivity = SteerSensitivity, ReturnSensitivity = ReturnSensitivity,
                SteerAssistStrength = SteerAssistStrength, FrontLatGrip = FrontLatGrip,
                FrontLongGrip = FrontLongGrip, RearLatGrip = RearLatGrip, RearLongGrip = RearLongGrip
            };
        }

        private static void ApplySettings(DriftSettings settings)
        {
            EnableAll = settings.EnableAll;
            EnableDriftScore = settings.EnableDriftScore;
            EnableSteerAssist = settings.EnableSteerAssist;
            PowerMultiplier = settings.PowerMultiplier;
            SteerAngle = settings.SteerAngle;
            SteerSensitivity = settings.SteerSensitivity;
            ReturnSensitivity = settings.ReturnSensitivity;
            SteerAssistStrength = settings.SteerAssistStrength;
            FrontLatGrip = settings.FrontLatGrip;
            FrontLongGrip = settings.FrontLongGrip;
            RearLatGrip = settings.RearLatGrip;
            RearLongGrip = settings.RearLongGrip;
        }

        private void LoadConfig()
        {
            try { ApplySettings(DriftSettingsStore.Load(ConfigPath)); }
            catch (Exception e) { Logger.LogError("Failed to load drift settings; using defaults: " + e.Message); }
        }

        public void SaveConfig()
        {
            if (string.IsNullOrEmpty(ConfigPath)) return;
            try { DriftSettingsStore.Save(ConfigPath, CaptureSettings()); }
            catch (Exception e) { Logger.LogError("Failed to save drift settings: " + e.Message); }
        }

    }

    // ==========================================
    // CarX 同款漂移计分系统
    // ==========================================
    public class DriftScoreManager : MonoBehaviour
    {
        private float currentScore = 0f;
        private float multiplier = 1.0f;
        private float comboProgress = 0f;
        private bool isDrifting = false;
        private bool wasDrifting = false;
        private bool hasFailedThisDrift = false;

        private float endTimer = 0f;
        private float fadeAlpha = 1f;
        private bool isFadingOut = false;

        private class RewardText
        {
            public string text;
            public float birthTime;
            public bool isFailed;
            public bool isDying;
            public float deathTime;
        }
        private List<RewardText> activeRewards = new List<RewardText>();

        private float lastBigAngleTime = 0f;
        private float lastExtremeAngleTime = 0f;
        private float lastContinuousTime = 0f;
        private float lastPerfectTime = 0f;
        private float lastLongDriftTime = 0f;
        private float lastThrottleTime = 0f;

        private LegacyUITheme scoreTheme, rewardTheme;
        private readonly LegacyUIScaleSettings classicScale = new LegacyUIScaleSettings
            { Mode = LegacyUIScaleMode.Manual, ManualScale = 1f };
        private Material classicRingMaterial;
        private GUIStyle scoreStyle;
        private GUIStyle multStyle;
        private GUIStyle shadowStyle;
        private GUIStyle rewardStyle;



        private static readonly AccessTools.FieldRef<CarPh, Vector3> bodyLinearWRef = AccessTools.FieldRefAccess<CarPh, Vector3>("body_linearW");
        private static readonly AccessTools.FieldRef<CarPh, Quaternion> bodyRotInvRef = AccessTools.FieldRefAccess<CarPh, Quaternion>("body_rot_inv");
        private static readonly AccessTools.FieldRef<CarPh, float> valUpRef = AccessTools.FieldRefAccess<CarPh, float>("val_up");

        private void EnsureClassicStyles()
        {
            if (scoreStyle != null) return;
            scoreStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            multStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            shadowStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            shadowStyle.normal.textColor = Color.black;
            rewardStyle = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void OnDestroy()
        {
            if (classicRingMaterial) Destroy(classicRingMaterial);
        }

        private void Update()
        {
            if (!Plugin.EnableAll || !Plugin.EnableDriftScore)
            {
                currentScore = 0f;
                comboProgress = 0f;
                multiplier = 1.0f;
                isDrifting = false;
                isFadingOut = false;
                fadeAlpha = 1f;
                activeRewards.Clear();
                return;
            }

            CarPh car = GetPlayerCar();
            if (car == null) { isDrifting = false; return; }

            Vector3 localVel = bodyRotInvRef(car) * bodyLinearWRef(car);
            float latSpeed = localVel.x;
            float fwdSpeed = localVel.z;
            float speedKmh = localVel.magnitude * 3.6f;
            float slipAngle = Mathf.Abs(Mathf.Atan2(latSpeed, Mathf.Abs(fwdSpeed)) * Mathf.Rad2Deg);

            float throttle = valUpRef(car);

            bool currentlyDrifting = speedKmh > 30f && slipAngle > 15f && slipAngle < 90f;

            if (currentlyDrifting)
            {
                if (!wasDrifting)
                {
                    hasFailedThisDrift = false;
                    lastContinuousTime = Time.time;
                    lastBigAngleTime = Time.time;
                    lastExtremeAngleTime = Time.time;
                    lastPerfectTime = Time.time;
                    lastLongDriftTime = Time.time;
                    lastThrottleTime = Time.time;
                }

                isDrifting = true;
                wasDrifting = true;
                isFadingOut = false;
                endTimer = 2.0f;
                fadeAlpha = 1f;

                float basePoints = speedKmh * slipAngle * Time.deltaTime * 0.15f;
                currentScore += basePoints * multiplier;

                if (multiplier < 5.0f)
                {
                    float currentSpeed = 0.2f + (multiplier - 1.0f) * 0.08f;
                    comboProgress += Time.deltaTime * currentSpeed;
                    if (comboProgress >= 1f)
                    {
                        comboProgress = 0f;
                        multiplier += 0.2f;
                        if (multiplier > 5.0f) multiplier = 5.0f;
                    }
                }
                else
                {
                    comboProgress = Mathf.PingPong(Time.time * 2f, 1f);
                }

                if (throttle > 0.8f)
                {
                    if (Time.time - lastThrottleTime > 3.0f)
                    {
                        TriggerReward(Plugin.L("全油门漂移", "Full Throttle", "Полный газ"), 200f, false);
                        lastThrottleTime = Time.time;
                    }
                }

                if (slipAngle > 40f)
                {
                    if (Time.time - lastBigAngleTime > 2.5f)
                    {
                        TriggerReward(Plugin.L("大角度", "Big Angle", "Большой угол"), 150f, false);
                        lastBigAngleTime = Time.time;
                    }
                }

                if (slipAngle > 60f)
                {
                    if (Time.time - lastExtremeAngleTime > 3.5f)
                    {
                        TriggerReward(Plugin.L("极限角度", "Extreme Angle", "Экстрим угол"), 250f, false);
                        lastExtremeAngleTime = Time.time;
                    }
                }

                if (slipAngle >= 25f && slipAngle <= 35f)
                {
                    if (Time.time - lastPerfectTime > 4.0f)
                    {
                        TriggerReward(Plugin.L("完美控制", "Perfect Control", "Идеальный контроль"), 100f, false);
                        lastPerfectTime = Time.time;
                    }
                }

                if (Time.time - lastContinuousTime > 3.0f)
                {
                    TriggerReward(Plugin.L("连续漂移", "Continuous Drift", "Непрерывный дрифт"), 50f, false);
                    lastContinuousTime = Time.time;
                }

                if (Time.time - lastLongDriftTime > 10.0f)
                {
                    TriggerReward(Plugin.L("长距离漂移", "Long Drift", "Долгий дрифт"), 500f, false);
                    lastLongDriftTime = Time.time;
                }
            }
            else
            {
                if (isDrifting)
                {
                    wasDrifting = false;
                    endTimer -= Time.deltaTime;

                    if (endTimer <= 0f && !hasFailedThisDrift)
                    {
                        isFadingOut = true;
                        TriggerReward(Plugin.L("漂移失败", "Drift Failed", "Дрифт провален"), 0f, true);
                        hasFailedThisDrift = true;
                    }
                }
            }

            if (isFadingOut)
            {
                fadeAlpha -= Time.deltaTime * 1.5f;
                if (fadeAlpha <= 0f)
                {
                    currentScore = 0f;
                    comboProgress = 0f;
                    multiplier = 1.0f;
                    isDrifting = false;
                    isFadingOut = false;
                    fadeAlpha = 1f;
                }
            }

            activeRewards.RemoveAll(r => r.isDying && Time.time - r.deathTime > 0.4f);
        }

        private void TriggerReward(string text, float points, bool isFailed)
        {
            if (!isFailed) currentScore += points * multiplier;
            string finalText = text + (isFailed ? "" : " +" + Mathf.RoundToInt(points * multiplier));

            foreach (var r in activeRewards)
            {
                if (!r.isDying)
                {
                    r.isDying = true;
                    r.deathTime = Time.time;
                }
            }

            activeRewards.Add(new RewardText
            {
                text = finalText,
                birthTime = Time.time,
                isFailed = isFailed,
                isDying = false
            });
        }

        private void OnGUI()
        {
            if (!LegacyHelpers.IsGameplayRunning || !Plugin.EnableAll || !Plugin.EnableDriftScore ||
                (currentScore < 1f && activeRewards.Count == 0)) return;
            if (Plugin.ScoreStyle == DriftScoreStyle.Classic)
            {
                using (new LegacyUIGuiScope(-9999, classicScale)) DrawClassicCounter();
                return;
            }
            DrawFlatCounter();
        }

        private void DrawFlatCounter()
        {
            using (new LegacyUIGuiScope(-9999))
            {
                if (scoreTheme == null)
                {
                    scoreTheme = new LegacyUITheme();
                    scoreTheme.Styles.Title.fontSize = 32;
                    scoreTheme.Styles.Title.alignment = TextAnchor.MiddleCenter;
                    rewardTheme = new LegacyUITheme();
                    rewardTheme.Styles.Title.alignment = TextAnchor.MiddleCenter;
                }
                float width = Screen.width / LegacyUI.Scale;
                float height = Screen.height / LegacyUI.Scale;
                float x = (width - 360f) / 2f, y = height * 0.12f;
                Color before = GUI.color;
                try
                {
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(fadeAlpha));
                    LegacyUI.Panel(new Rect(x, y, 360f, 102f), 0.75f);
                    using (LegacyUI.WithTheme(scoreTheme))
                        LegacyUI.Title(new Rect(x + 12f, y + 8f, 336f, 44f), Mathf.FloorToInt(currentScore) + "   ×" + multiplier.ToString("F1"));
                    LegacyUI.ProgressBar(new Rect(x + 12f, y + 65f, 336f, 20f), comboProgress);
                    foreach (var reward in activeRewards)
                    {
                        float age = Time.time - reward.birthTime;
                        if (!reward.isDying && age > 1f) { reward.isDying = true; reward.deathTime = Time.time; }
                        float deathAge = reward.isDying ? Time.time - reward.deathTime : 0f;
                        float alpha = reward.isDying ? Mathf.Clamp01(1f - deathAge / 0.4f) : 1f;
                        if (alpha <= 0f) continue;
                        GUI.color = new Color(1f, reward.isFailed ? 0.25f : 1f, reward.isFailed ? 0.25f : 1f, alpha);
                        rewardTheme.Styles.Title.fontSize = Mathf.RoundToInt(24f * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(age / 0.15f)));
                        using (LegacyUI.WithTheme(rewardTheme))
                            LegacyUI.Title(new Rect(x - 60f, y + 110f + deathAge * 120f, 480f, 52f), reward.text);
                    }
                }
                finally { GUI.color = before; }
            }
        }

        private void DrawClassicCounter()
        {
            EnsureClassicStyles();

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 1.5f);

            float ringRadius = 45f * scale;
            int fontSize = Mathf.RoundToInt(ringRadius * 1.4f);

            scoreStyle.fontSize = fontSize;
            shadowStyle.fontSize = fontSize;
            multStyle.fontSize = Mathf.RoundToInt(ringRadius * 0.5f);

            float gap = 20f * scale;
            float scoreWidth = 280f * scale;
            float totalW = scoreWidth + gap + ringRadius * 2;

            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.12f;
            float startX = cx - totalW / 2f;

            float scoreX = startX;
            float ringX = startX + scoreWidth + gap + ringRadius;
            float ringY = cy + ringRadius;

            Color baseColor = isDrifting ? Color.Lerp(Color.yellow, Color.red, Mathf.Clamp01((multiplier - 1f) / 4f)) : Color.white;
            Color finalColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(fadeAlpha));

            scoreStyle.normal.textColor = finalColor;
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeAlpha));

            string scoreStr = Mathf.FloorToInt(currentScore).ToString();
            if (currentScore > 0)
            {
                GUI.Label(new Rect(scoreX + 2, cy + 2, scoreWidth, ringRadius * 2), scoreStr, shadowStyle);
                GUI.Label(new Rect(scoreX, cy, scoreWidth, ringRadius * 2), scoreStr, scoreStyle);
            }

            if (fadeAlpha > 0)
            {
                DrawDriftRing(ringX, ringY, ringRadius, comboProgress, multiplier, fadeAlpha);
                multStyle.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(fadeAlpha));
                GUI.Label(new Rect(ringX - ringRadius, ringY - ringRadius * 0.35f, ringRadius * 2, ringRadius * 0.7f), "x" + multiplier.ToString("F1"), multStyle);
            }

            foreach (var r in activeRewards)
            {
                float age = Time.time - r.birthTime;
                float popScale = Mathf.Lerp(2.0f, 1.0f, Mathf.Clamp01(age / 0.15f));
                float alpha = 1f;
                float dropOffset = 0f;

                if (r.isDying)
                {
                    float deathAge = Time.time - r.deathTime;
                    alpha = Mathf.Clamp01(1f - deathAge / 0.4f);
                    dropOffset = deathAge * 120f;
                }
                else if (age > 1.0f)
                {
                    r.isDying = true;
                    r.deathTime = Time.time;
                }

                if (alpha <= 0) continue;

                rewardStyle.fontSize = Mathf.RoundToInt(32f * scale * popScale);
                Color textColor = r.isFailed ? Color.red : Color.white;
                textColor.a = alpha;
                rewardStyle.normal.textColor = textColor;

                float textY = ringY + ringRadius + 20f + dropOffset;

                GUI.Label(new Rect(cx - 200, textY + 2, 400, 50), r.text, new GUIStyle(rewardStyle) { normal = { textColor = new Color(0f, 0f, 0f, alpha) } });
                GUI.Label(new Rect(cx - 200, textY, 400, 50), r.text, rewardStyle);
            }
        }

        private void DrawDriftRing(float cx, float cy, float radius, float progress, float mult, float alpha)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!classicRingMaterial)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (!shader) return;
                classicRingMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                classicRingMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                classicRingMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                classicRingMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                classicRingMaterial.SetInt("_ZWrite", 0);
                classicRingMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            if (!classicRingMaterial.SetPass(0)) return;
            int segments = 60;
            float whiteThick = 2f;
            float yellowThick = 16f;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

            float outerR = radius;
            float innerR = radius - whiteThick - yellowThick - whiteThick;

            GL.Begin(GL.QUADS);
            GL.Color(new Color(1f, 1f, 1f, alpha));
            DrawArc(cx, cy, outerR - whiteThick, outerR, 0, 360, segments);
            GL.End();

            GL.Begin(GL.QUADS);
            GL.Color(new Color(1f, 1f, 1f, alpha));
            DrawArc(cx, cy, innerR, innerR + whiteThick, 0, 360, segments);
            GL.End();

            if (progress > 0.01f)
            {
                GL.Begin(GL.QUADS);
                Color progColor = Color.Lerp(Color.yellow, new Color(1f, 0.3f, 0f), Mathf.Clamp01((mult - 1f) / 4f));
                progColor.a = alpha;
                GL.Color(progColor);

                float barInner = innerR + whiteThick;
                float barOuter = outerR - whiteThick;

                float startAngle = 0f;
                float endAngle = progress * 360f;
                DrawArc(cx, cy, barInner, barOuter, startAngle, endAngle, segments);
                GL.End();
            }

            GL.PopMatrix();
        }

        private void DrawArc(float cx, float cy, float inner, float outer, float startDeg, float endDeg, int segments)
        {
            float startRad = (90 - startDeg) * Mathf.Deg2Rad;
            float endRad = (90 - endDeg) * Mathf.Deg2Rad;
            float step = (startRad - endRad) / segments;

            for (int i = 0; i < segments; i++)
            {
                float a1 = startRad - i * step;
                float a2 = startRad - (i + 1) * step;

                float cos1 = Mathf.Cos(a1);
                float sin1 = Mathf.Sin(a1);
                float cos2 = Mathf.Cos(a2);
                float sin2 = Mathf.Sin(a2);

                GL.Vertex3(cx + cos1 * outer, cy + sin1 * outer, 0);
                GL.Vertex3(cx + cos2 * outer, cy + sin2 * outer, 0);
                GL.Vertex3(cx + cos2 * inner, cy + sin2 * inner, 0);
                GL.Vertex3(cx + cos1 * inner, cy + sin1 * inner, 0);
            }
        }

        private CarPh GetPlayerCar()
        {
            try
            {
                var prop = typeof(PlayerControl).GetProperty("curPlyaer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object player = prop != null ? prop.GetValue(null) : null;
                if (player == null) return null;
                var comp = player as Component;
                if (comp == null) return null;
                var parentField = player.GetType().GetField("parent_control", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (parentField == null) return null;
                var parentControl = parentField.GetValue(player) as Component;
                if (parentControl == null) return null;
                return parentControl.GetComponentInChildren<CarPh>();
            }
            catch { return null; }
        }
    }

    // ==========================================
    // 补丁 1：动力系统
    // ==========================================
    [HarmonyPatch(typeof(CarPhWheel), "GetEngineForce")]
    public class Patch_GetEngineForce
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result)
        {
            if (!Plugin.EnableAll) return true;
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(ref float __result)
        {
            if (!Plugin.EnableAll) return;
            if (__result > 0f) __result *= Plugin.PowerMultiplier;
        }
    }

    // ==========================================
    // 补丁 2：四轮独立抓地力
    // ==========================================
    [HarmonyPatch(typeof(CarPhWheel), "CalculateFriction")]
    public class Patch_CalculateFriction
    {
        private static readonly AccessTools.FieldRef<CarPhWheel, bool> isBackRef = AccessTools.FieldRefAccess<CarPhWheel, bool>("IsBack");

        [HarmonyPrefix]
        public static bool Prefix(ref Vector3 __result)
        {
            if (!Plugin.EnableAll) return true;
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(CarPhWheel __instance, ref Vector3 __result)
        {
            if (!Plugin.EnableAll) return;
            if (isBackRef(__instance))
            {
                __result.x *= Plugin.RearLatGrip;
                __result.z *= Plugin.RearLongGrip;
            }
            else
            {
                __result.x *= Plugin.FrontLatGrip;
                __result.z *= Plugin.FrontLongGrip;
            }
        }
    }

    // ==========================================
    // 补丁 3：带强制反打辅助的终极转向
    // ==========================================
    [HarmonyPatch(typeof(CarPh), "UpdateAng")]
    public class Patch_UpdateAng
    {
        private static readonly FieldInfo userInputField = AccessTools.Field(typeof(CarPh), "user_input");
        private static readonly FieldInfo vLeftField = AccessTools.Field(userInputField.FieldType, "v_left");
        private static readonly AccessTools.FieldRef<CarPh, float> angRef = AccessTools.FieldRefAccess<CarPh, float>("ang");
        private static readonly AccessTools.FieldRef<CarPh, float> angMaxRef = AccessTools.FieldRefAccess<CarPh, float>("ang_max");
        private static readonly AccessTools.FieldRef<CarPh, Vector3> bodyLinearWRef = AccessTools.FieldRefAccess<CarPh, Vector3>("body_linearW");
        private static readonly AccessTools.FieldRef<CarPh, Quaternion> bodyRotInvRef = AccessTools.FieldRefAccess<CarPh, Quaternion>("body_rot_inv");

        [HarmonyPrefix]
        public static bool Prefix(CarPh __instance)
        {
            if (!Plugin.EnableAll) return true;

            object userInput = userInputField.GetValue(__instance);
            if (userInput == null) return true;

            float vLeft = (float)vLeftField.GetValue(userInput);
            float ang_max = Plugin.SteerAngle;
            angMaxRef(__instance) = ang_max;

            float targetAng = vLeft * ang_max;

            if (Plugin.EnableSteerAssist && Plugin.SteerAssistStrength > 0f)
            {
                Vector3 localVel = bodyRotInvRef(__instance) * bodyLinearWRef(__instance);
                float latSpeed = localVel.x;
                float fwdSpeed = localVel.z;

                if (Mathf.Abs(fwdSpeed) > 3f && Mathf.Abs(latSpeed) > 1f)
                {
                    float slipAngle = Mathf.Atan2(latSpeed, Mathf.Abs(fwdSpeed)) * Mathf.Rad2Deg;
                    float idealCounterSteer = -slipAngle;
                    idealCounterSteer = Mathf.Clamp(idealCounterSteer, -ang_max, ang_max);

                    targetAng = Mathf.Lerp(targetAng, idealCounterSteer, Plugin.SteerAssistStrength);
                }
            }

            float currentAng = angRef(__instance);
            float baseSpeed = 500f;
            float currentSpeed = baseSpeed;

            if (Mathf.Abs(vLeft) < 0.01f)
                currentSpeed *= Plugin.ReturnSensitivity;
            else
                currentSpeed *= Plugin.SteerSensitivity;

            currentAng = Mathf.MoveTowards(currentAng, targetAng, currentSpeed * Time.fixedDeltaTime);

            currentAng = Mathf.Clamp(currentAng, -ang_max, ang_max);
            if (Mathf.Abs(currentAng) < 0.01f) currentAng = 0f;

            angRef(__instance) = currentAng;

            return false;
        }
    }
}
