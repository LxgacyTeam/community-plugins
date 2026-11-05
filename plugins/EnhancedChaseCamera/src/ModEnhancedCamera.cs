using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using BigCityLegacy.UI;

public static class ModEnhancedCameraBootstrap
{
    private static bool _applied;
    public static void Apply()
    {
        if (_applied) return; _applied = true;
        try { new Harmony("com.madout.enhancedcamera").PatchAll(typeof(ModEnhancedCameraBootstrap).Assembly); }
        catch (Exception ex) { Debug.LogError("[ECC] " + ex); }
        var go = new GameObject("ECC_GUI");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<ModEnhancedCameraGUI>();
        Debug.Log("[ECC] Ready. Shift+C to open. Select Enhanced Chase using the game camera cycle.");
    }
}

public static class CamSettings
{
    private const int VER = 9;
    public static bool Enabled => EnhancedCameraMode.IsActive(Cam.me);
    public static float SlotFovY = 70f;
    public static float SlotPosZ = -6.5f;
    public static float SlotPosY = 1.7f;
    public static float SpeedDistAdd = 3f;
    public static float DistOutTime = 0.4f;
    public static float DistInTime = 0.05f;
    public static float SpeedPitch = 5f;
    public static float JumpPitch = 8f;
    public static float JumpPosOffset = 0.6f;
    public static float JumpMinAirTime = 0.12f; // сек — минимальное время в воздухе перед эффектом
    public static float JumpVelDeadzone = 1.2f;  // м/с — игнорируем мелкие вертикальные скорости
    public static float GforceRoll = 4f;
    public static float GforceMinSpeed = 25f;
    public static float ShakeStrength = 1f;
    public static float ShakeStartKmh = 80f;
    public static float DriftLagStrength = 6f;   // макс. градусов отставания по yaw
    public static float DriftLagCatchUp = 0.35f; // сек — скорость "догоняния"
    public static float InertiaStrength = 0.06f;  // метров смещения на 1 м/с² ускорения
    public static float InertiaBounce = 0.55f;  // 0=без отскока, 1=сильно пружинит
    public static float WindowX = 30f, WindowY = 65f;

    public static void Load()
    {
        if (PlayerPrefs.GetInt("ECC_ver", 0) != VER) { ResetDefaults(); return; }
        SlotFovY = PlayerPrefs.GetFloat("ECC_fovy", 70f);
        SlotPosZ = PlayerPrefs.GetFloat("ECC_pz", -6.5f);
        SlotPosY = PlayerPrefs.GetFloat("ECC_py", 1.7f);
        SpeedDistAdd = PlayerPrefs.GetFloat("ECC_sda", 3f);
        DistOutTime = PlayerPrefs.GetFloat("ECC_dot", 0.4f);
        DistInTime = PlayerPrefs.GetFloat("ECC_dit", 0.05f);
        SpeedPitch = PlayerPrefs.GetFloat("ECC_spit", 5f);
        JumpPitch = PlayerPrefs.GetFloat("ECC_jpit", 8f);
        JumpPosOffset = PlayerPrefs.GetFloat("ECC_jpos", 0.6f);
        JumpMinAirTime = PlayerPrefs.GetFloat("ECC_jmat", 0.12f);
        JumpVelDeadzone = PlayerPrefs.GetFloat("ECC_jvdz", 1.2f);
        GforceRoll = PlayerPrefs.GetFloat("ECC_roll", 4f);
        GforceMinSpeed = PlayerPrefs.GetFloat("ECC_grms", 25f);
        ShakeStrength = PlayerPrefs.GetFloat("ECC_shk", 1f);
        ShakeStartKmh = PlayerPrefs.GetFloat("ECC_shks", 80f);
        DriftLagStrength = PlayerPrefs.GetFloat("ECC_dls", 6f);
        DriftLagCatchUp = PlayerPrefs.GetFloat("ECC_dlc", 0.35f);
        InertiaStrength = PlayerPrefs.GetFloat("ECC_ist", 0.06f);
        InertiaBounce = PlayerPrefs.GetFloat("ECC_ibo", 0.55f);
        WindowX = PlayerPrefs.GetFloat("ECC_wx", 30f);
        WindowY = PlayerPrefs.GetFloat("ECC_wy", 60f);
    }
    public static void Save()
    {
        PlayerPrefs.SetInt("ECC_ver", VER);
        PlayerPrefs.SetFloat("ECC_fovy", SlotFovY);
        PlayerPrefs.SetFloat("ECC_pz", SlotPosZ);
        PlayerPrefs.SetFloat("ECC_py", SlotPosY);
        PlayerPrefs.SetFloat("ECC_sda", SpeedDistAdd);
        PlayerPrefs.SetFloat("ECC_dot", DistOutTime);
        PlayerPrefs.SetFloat("ECC_dit", DistInTime);
        PlayerPrefs.SetFloat("ECC_spit", SpeedPitch);
        PlayerPrefs.SetFloat("ECC_jpit", JumpPitch);
        PlayerPrefs.SetFloat("ECC_jpos", JumpPosOffset);
        PlayerPrefs.SetFloat("ECC_jmat", JumpMinAirTime);
        PlayerPrefs.SetFloat("ECC_jvdz", JumpVelDeadzone);
        PlayerPrefs.SetFloat("ECC_roll", GforceRoll);
        PlayerPrefs.SetFloat("ECC_grms", GforceMinSpeed);
        PlayerPrefs.SetFloat("ECC_shk", ShakeStrength);
        PlayerPrefs.SetFloat("ECC_shks", ShakeStartKmh);
        PlayerPrefs.SetFloat("ECC_dls", DriftLagStrength);
        PlayerPrefs.SetFloat("ECC_dlc", DriftLagCatchUp);
        PlayerPrefs.SetFloat("ECC_ist", InertiaStrength);
        PlayerPrefs.SetFloat("ECC_ibo", InertiaBounce);
        PlayerPrefs.SetFloat("ECC_wx", WindowX);
        PlayerPrefs.SetFloat("ECC_wy", WindowY);
        PlayerPrefs.Save();
    }
    public static void ResetDefaults()
    {
        SlotFovY = 54f; SlotPosZ = -6.5f; SlotPosY = 1.7f;
        SpeedDistAdd = 3f; DistOutTime = 0.4f; DistInTime = 0.05f;
        SpeedPitch = 5f; JumpPitch = 8f; JumpPosOffset = 0.6f;
        JumpMinAirTime = 0.12f; JumpVelDeadzone = 1.2f;
        GforceRoll = 4f; GforceMinSpeed = 25f;
        ShakeStrength = 1f; ShakeStartKmh = 80f;
        DriftLagStrength = 6f; DriftLagCatchUp = 0.35f;
        InertiaStrength = 0.06f; InertiaBounce = 0.55f; Save();
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  CarPhAccess — читает реальные физические данные машины:
//  speed (м/с), is_fly (в воздухе?), body.velocity (Rigidbody).
//  Путь: control -> ToCar() -> CarControl.ph_car -> CarPh
// ═══════════════════════════════════════════════════════════════════════════
internal static class CarPhAccess
{
    private static bool _cached;
    private static MethodInfo _miToCar;
    private static FieldInfo _fiPhCar;
    private static FieldInfo _fiSpeed;
    private static FieldInfo _fiIsFly;
    private static FieldInfo _fiBody;

    public struct Data
    {
        public bool valid;
        public float speedMs;
        public bool isFly;
        public float vertVel; // м/с, знак: + вверх
    }

    public static Data Read(object control)
    {
        var result = new Data();
        if (control == null) return result;
        try
        {
            if (!_cached)
            {
                _cached = true;
                var bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var tControl = control.GetType();
                // Ищем ToCar по всей иерархии (control обычно Control или PlayerControl)
                var t = tControl;
                while (t != null && _miToCar == null)
                {
                    _miToCar = t.GetMethod("ToCar", bf, null, Type.EmptyTypes, null);
                    t = t.BaseType;
                }
                var tCarControl = AccessTools.TypeByName("CarControl");
                _fiPhCar = tCarControl?.GetField("ph_car", bf);
                var tCarPh = AccessTools.TypeByName("CarPh");
                _fiSpeed = tCarPh?.GetField("speed", bf);
                _fiIsFly = tCarPh?.GetField("is_fly", bf);
                _fiBody = tCarPh?.GetField("body", bf);
            }
            if (_miToCar == null) return result;
            var car = _miToCar.Invoke(control, null);
            if (car == null) return result; // не в машине (пешком)

            var phCar = _fiPhCar?.GetValue(car);
            if (phCar == null) return result;

            result.valid = true;
            if (_fiSpeed != null) result.speedMs = (float)_fiSpeed.GetValue(phCar);
            if (_fiIsFly != null) result.isFly = (bool)_fiIsFly.GetValue(phCar);
            if (_fiBody != null)
            {
                var rb = _fiBody.GetValue(phCar) as Rigidbody;
                if (rb != null) result.vertVel = rb.velocity.y;
            }
        }
        catch { }
        return result;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  Runtime — все сглаженные значения
// ═══════════════════════════════════════════════════════════════════════════
public static class CamRuntime
{
    public static float SpeedKmh = 0f;
    public static float SmoothedDist = 0f;
    public static float SmoothedPitch = 0f;
    public static float JumpPitch = 0f;
    public static float JumpPosY = 0f;
    public static float SmoothedRoll = 0f;
    public static float DriftLag = 0f;
    public static float EccTime = 0f;
    public static bool InCar = false;

    private static float _pitchVel, _jumpPitchVel, _jumpPosVel, _rollVel, _wasFlyTime, _driftLagVel;
    private static float _airborneTime = 0f;
    private static float _lastCarYaw = float.NaN;
    public static float InertiaOffset = 0f; // текущее смещение пружины (метры)
    private static float _inertiaVel = 0f; // скорость пружины
    private static float _lastSpeedMs = float.NaN;

    public static void Reset()
    {
        SpeedKmh = SmoothedDist = SmoothedPitch = JumpPitch = JumpPosY = SmoothedRoll = DriftLag = EccTime = InertiaOffset = 0f;
        _pitchVel = _jumpPitchVel = _jumpPosVel = _rollVel = _wasFlyTime = _driftLagVel = _airborneTime = _inertiaVel = 0f;
        _lastCarYaw = _lastSpeedMs = float.NaN;
        InCar = false;
    }

    // ── Дистанция — вызывается в Apply PREFIX (до update()/UpWorldPos) ────
    public static void UpdateDist(float speedKmh, float dt)
    {
        if (dt <= 0f) return;
        SpeedKmh = speedKmh;
        float ratio = Mathf.Clamp01(SpeedKmh / 180f);
        float target = ratio * CamSettings.SpeedDistAdd;
        float speed = target > SmoothedDist
            ? CamSettings.SpeedDistAdd / Mathf.Max(0.001f, CamSettings.DistOutTime)
            : CamSettings.SpeedDistAdd / Mathf.Max(0.001f, CamSettings.DistInTime);
        SmoothedDist = Mathf.MoveTowards(SmoothedDist, target, speed * dt);
    }

    // ── Эффекты ротации/тряски — вызывается в Apply POSTFIX ───────────────
    public static void UpdateEffects(Vector3 speedVecWorld, Quaternion carRot,
        bool isFly, float vertVelMs, float dt)
    {
        if (dt <= 0f) return;
        EccTime += dt;
        float ratio = Mathf.Clamp01(SpeedKmh / 180f);

        // Speed pitch
        float targetPitch = ratio * CamSettings.SpeedPitch;
        SmoothedPitch = Mathf.SmoothDamp(SmoothedPitch, targetPitch, ref _pitchVel, 0.18f);

        // ── Jump — на основе is_fly + реальной вертикальной скорости ──────
        // Фильтруем мелкие кочки/бордюры: нужно и минимальное время
        // непрерывно в воздухе, и скорость выше dead-zone.
        float jumpPitchTarget = 0f, jumpPosTarget = 0f;
        if (isFly)
        {
            _airborneTime += dt;
            // Плавный ramp-in после порога MinAirTime (не резкий щелчок при пересечении)
            float airRamp = Mathf.Clamp01((_airborneTime - CamSettings.JumpMinAirTime) / 0.08f);
            // Dead-zone по скорости — гасим шум мелких кочек
            float v = vertVelMs;
            float dz = CamSettings.JumpVelDeadzone;
            float vFiltered = Mathf.Abs(v) > dz ? v - Mathf.Sign(v) * dz : 0f;

            jumpPitchTarget = Mathf.Clamp(-vFiltered * 1.5f, -CamSettings.JumpPitch, CamSettings.JumpPitch) * airRamp;
            jumpPosTarget = Mathf.Clamp(-vFiltered * 0.08f, -CamSettings.JumpPosOffset, CamSettings.JumpPosOffset) * airRamp;
            _wasFlyTime = EccTime;
        }
        else
        {
            _airborneTime = 0f;
            if (EccTime - _wasFlyTime < 0.35f)
            {
                float t = 1f - Mathf.Clamp01((EccTime - _wasFlyTime) / 0.35f);
                jumpPosTarget = -CamSettings.JumpPosOffset * 0.5f * t;
                jumpPitchTarget = CamSettings.JumpPitch * 0.4f * t;
            }
        }
        JumpPitch = Mathf.SmoothDamp(JumpPitch, jumpPitchTarget, ref _jumpPitchVel, 0.16f);
        JumpPosY = Mathf.SmoothDamp(JumpPosY, jumpPosTarget, ref _jumpPosVel, 0.16f);

        // ── G-force roll — ЛОКАЛЬНАЯ (относительно машины) боковая скорость
        float rollTarget = 0f;
        if (SpeedKmh >= CamSettings.GforceMinSpeed)
        {
            // Проекция мирового вектора смещения на правую ось машины
            Vector3 localRight = carRot * Vector3.right;
            float lateral = Vector3.Dot(speedVecWorld, localRight) / dt;
            rollTarget = Mathf.Clamp(lateral * CamSettings.GforceRoll * 0.1f,
                -CamSettings.GforceRoll, CamSettings.GforceRoll);
        }
        float rollSmooth = Mathf.Abs(rollTarget) > Mathf.Abs(SmoothedRoll) ? 0.15f : 0.07f;
        SmoothedRoll = Mathf.SmoothDamp(SmoothedRoll, rollTarget, ref _rollVel, rollSmooth);

        // ── Drift lag — камера "отстаёт" от резкого поворота машины ──────
        // (NFS-style: зад машины уже ушёл в сторону, камера чуть позади)
        float carYaw = carRot.eulerAngles.y;
        if (!float.IsNaN(_lastCarYaw))
        {
            float yawRate = Mathf.DeltaAngle(_lastCarYaw, carYaw) / dt; // °/сек
            float lagTarget = Mathf.Clamp(-yawRate * 0.12f,
                -CamSettings.DriftLagStrength, CamSettings.DriftLagStrength);
            DriftLag = Mathf.SmoothDamp(DriftLag, lagTarget, ref _driftLagVel,
                Mathf.Max(0.02f, CamSettings.DriftLagCatchUp));
        }
        _lastCarYaw = carYaw;

        // ── Inertia — пружина камеры на резких ускорениях/торможениях ────
        // Торможение (accel<0) → камера по инерции "проседает" вперёд,
        // к машине, затем отскакивает назад (overshoot).
        // Резкий старт (accel>0) → камера чуть отстаёт назад, потом
        // догоняет.
        float speedMs = SpeedKmh / 3.6f;
        if (!float.IsNaN(_lastSpeedMs))
        {
            float accel = Mathf.Clamp((speedMs - _lastSpeedMs) / dt, -25f, 25f); // м/с²
            float inertiaTarget = Mathf.Clamp(-accel * CamSettings.InertiaStrength, -1.5f, 1.5f);

            // Полу-неявный Эйлер интегратор пружины (даёт настоящий отскок)
            const float stiffness = 60f;
            float criticalDamping = 2f * Mathf.Sqrt(stiffness);
            float damping = criticalDamping * (1f - Mathf.Clamp01(CamSettings.InertiaBounce) * 0.85f);

            float springForce = (inertiaTarget - InertiaOffset) * stiffness;
            float dampingForce = -_inertiaVel * damping;
            _inertiaVel += (springForce + dampingForce) * dt;
            InertiaOffset += _inertiaVel * dt;
        }
        _lastSpeedMs = speedMs;
    }

    public static Vector3 GetShake()
    {
        if (CamSettings.ShakeStrength < 0.01f) return Vector3.zero;
        float t = Mathf.Clamp01((SpeedKmh - CamSettings.ShakeStartKmh) / 60f);
        if (t < 0.001f) return Vector3.zero;
        float sx = t * Mathf.Sin(2f * Mathf.PI * 17f * EccTime) * 0.005f;
        float sy = t * Mathf.Sin(2f * Mathf.PI * 11f * EccTime) * 0.009f;
        return new Vector3(sx, sy, 0f) * CamSettings.ShakeStrength;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  CF — рефлексия по Cam. Слот кэшируется с ПОВТОРНОЙ попыткой,
//  пока не найдём валидный cur_slot (мог быть null на первом кадре).
// ═══════════════════════════════════════════════════════════════════════════
internal static class CF
{
    public static FieldInfo fiCurSlot, fiNodePos, fiSlotFovy, fiLastSetTr, fiTr, fiTrPos, fiTrRot, fiSpeedVec;
    private static bool _baseCached, _slotCached;

    public static void TryCache(object cam)
    {
        var bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (!_baseCached)
        {
            var t = cam.GetType();
            fiCurSlot = t.GetField("cur_slot", bf);
            fiNodePos = t.GetField("cam_pos_node_pos", bf);
            fiLastSetTr = t.GetField("last_set_tr", bf);
            fiSpeedVec = t.GetField("speed_vec", bf);
            var lst = fiLastSetTr?.GetValue(cam);
            if (lst != null)
            {
                fiTr = lst.GetType().GetField("tr", bf);
                var tr = fiTr?.GetValue(lst);
                if (tr != null)
                {
                    fiTrPos = tr.GetType().GetField("pos", bf);
                    fiTrRot = tr.GetType().GetField("rot", bf);
                    _baseCached = true; // базовые поля точно на месте
                }
            }
        }
        if (!_slotCached)
        {
            var slot = fiCurSlot?.GetValue(cam);
            if (slot != null)
            {
                fiSlotFovy = slot.GetType().GetField("fovy", bf);
                _slotCached = true; // ретраим пока слот не появится
            }
        }
    }

    public static bool GetTrPosRot(object cam, out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero; rot = Quaternion.identity;
        var lst = fiLastSetTr?.GetValue(cam); if (lst == null) return false;
        var tr = fiTr?.GetValue(lst); if (tr == null) return false;
        if (fiTrPos != null) pos = (Vector3)fiTrPos.GetValue(tr);
        if (fiTrRot != null) rot = (Quaternion)fiTrRot.GetValue(tr);
        return true;
    }
    public static void SetTrPosRot(object cam, Vector3 pos, Quaternion rot)
    {
        var lst = fiLastSetTr?.GetValue(cam); if (lst == null) return;
        var tr = fiTr?.GetValue(lst); if (tr == null) return;
        fiTrPos?.SetValue(tr, pos);
        fiTrRot?.SetValue(tr, rot);
        fiTr?.SetValue(lst, tr);
    }
    public static Vector3 GetSpeedVec(object cam)
    {
        var v = fiSpeedVec?.GetValue(cam);
        return v != null ? (Vector3)v : Vector3.zero;
    }
}

// ─── GUI ──────────────────────────────────────────────────────────────────
public class ModEnhancedCameraGUI : MonoBehaviour
{
    private LegacyUIWindow window;
    private readonly PluginUiSupport uiSupport = new PluginUiSupport();
    private readonly LegacyUIScaleSettings scale = new LegacyUIScaleSettings();
    private readonly LegacyUIScrollView scroll = new LegacyUIScrollView(new Rect())
        { DrawBackground = false, ShowHorizontalScrollbar = false };
    private readonly LegacyUIDropdown presets = new LegacyUIDropdown(new string[0]);
    private CamPresetStore store;
    private int tab;
    private string presetName = "";
    private Func<string> status = () => string.Empty;

    private void Awake()
    {
        CamSettings.Load();
        LegacyInput.Init();
        window = new LegacyUIWindow(CameraText.WindowTitle.Get(), new Rect(CamSettings.WindowX, CamSettings.WindowY, 520f, 600f),
            new LegacyUIWindowOptions { ShowCloseButton = true, RightHint = "[Shift+C]",
                PlayerPrefsKey = "EnhancedChaseCamera.Window", InputBlockMode = LegacyUIInputBlockMode.Window });
        window.Visible = false;
        window.Closed += () => presets.IsOpen = false;
        store = new CamPresetStore(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "EnhancedChaseCamera.presets.xml"));
        try { store.Load(); RefreshPresets(); }
        catch (Exception ex) { status = () => CameraText.PresetReadFailed.Get() + ": " + ex.Message; Debug.LogError("[ECC] " + status()); }
    }

    private void RefreshPresets()
    {
        presets.Items = store.Names;
        presets.SelectedIndex = Mathf.Clamp(presets.SelectedIndex, 0, Mathf.Max(0, store.Names.Length - 1));
    }

    private void Update()
    {
        bool gameplay = LegacyHelpers.IsGameplayRunning;
        if (gameplay && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.C))
            window.Visible = !window.Visible;
        if (!gameplay || !window.Visible) presets.IsOpen = false;
        uiSupport.SetCursor(this, gameplay && window.Visible);
    }
    private void OnDisable() { uiSupport.Dispose(); }
    private void OnDestroy() { uiSupport.Dispose(); }

    private void OnGUI()
    {
        if (!LegacyHelpers.IsGameplayRunning || !window.IsRendered) return;
        using (new LegacyUIGuiScope(-10000, scale, null, uiSupport.Backdrop))
        {
            window.Title = CameraText.WindowTitle.Get();
            if (tab == 1) presets.HandlePopupInput();
            window.Draw(DrawContent);
            if (tab == 1 && window.Visible) presets.DrawPopup();
        }
    }

    private void DrawContent(Rect content)
    {
        var ui = new LegacyUILayout(content);
        Rect tabs = ui.Row(28f);
        string[] titles = { CameraText.SettingsTab.Get(), CameraText.PresetsTab.Get(), CameraText.InfoTab.Get() };
        for (int i = 0; i < titles.Length; i++)
            if (LegacyUI.TabButton(new Rect(tabs.x + i * tabs.width / 3f, tabs.y, tabs.width / 3f - 4f, tabs.height), titles[i], tab == i))
            { if (tab != i) { tab = i; presets.IsOpen = false; scroll.ResetScroll(); } }
        LegacyUI.Status(ui.Row(22f), CamSettings.Enabled ? CameraText.ModeActive.Get() : CameraText.SelectMode.Get());
        Rect footer = new Rect(content.x, content.yMax - 30f, content.width, 30f);
        scroll.ViewRect = new Rect(content.x, ui.Y, content.width, Mathf.Max(0f, footer.y - ui.Y - 8f));
        // Dropdown anchors must stay outside scroll clips, like their popups.
        if (tab == 1)
        {
            var body = new LegacyUILayout(scroll.ViewRect);
            DrawPresets(ref body);
        }
        else scroll.Draw(inner =>
        {
            var body = new LegacyUILayout(inner);
            if (tab == 0)
            {
                string group = null;
                foreach (CamParameter parameter in CamParameters.All)
                {
                    if (group != parameter.Group) { group = parameter.Group; LegacyUI.Title(body.Row(24f), group); }
                    float value = parameter.Get();
                    LegacyUI.Label(body.Row(20f), parameter.Label + ": " + value.ToString("0.##"));
                    float next = LegacyUI.HorizontalSlider(body.Row(20f), value, parameter.Min, parameter.Max);
                    if (!Mathf.Approximately(next, value)) parameter.Set(next);
                }
            }
            else
            {
                LegacyUI.Title(body.Row(24f), CameraText.WindowTitle.Get() + " 2.3.1");
                LegacyUI.Label(body.Row(24f), CameraText.MenuHelp.Get());
                LegacyUI.Label(body.Row(24f), CameraText.CycleHelp.Get());
                LegacyUI.Label(body.Row(24f), CameraText.ModeOrderHelp.Get());
                LegacyUI.Label(body.Row(24f), CameraText.NextModeHelp.Get());
                LegacyUI.Label(body.Row(24f), CameraText.PresetsHelp.Get());
                LegacyUI.Label(body.Row(24f), string.Format(CameraText.LiveSpeed.Get(), CamRuntime.SpeedKmh.ToString("0")));
            }
        });
        
        if (LegacyUI.GreenButton(new Rect(footer.x, footer.y, 140f, 30f), CameraText.Save.Get())) CamSettings.Save();
        if (LegacyUI.Button(new Rect(footer.x + 148f, footer.y, 140f, 30f), CameraText.Reset.Get())) CamSettings.ResetDefaults();
    }

    private void DrawPresets(ref LegacyUILayout ui)
    {
        LegacyUI.Label(ui.Row(20f), CameraText.PresetName.Get());
        presetName = LegacyUI.TextField(ui.Row(28f), presetName);
        if (LegacyUI.Button(ui.Row(28f), CameraText.SavePreset.Get()))
            PresetAction(() => { store.SavePreset(presetName, false); RefreshPresets(); presets.SelectedIndex = Array.IndexOf(store.Names, presetName.Trim()); }, CameraText.PresetSaved);
        ui.Space(8f);
        Rect selector = ui.Row(28f);
        if (store.Names.Length > 0) presets.Draw(selector);
        else LegacyUI.HintBox(selector, CameraText.NoPresets.Get());
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && store.Names.Length > 0;
        if (LegacyUI.Button(ui.Row(28f), CameraText.LoadPreset.Get()))
            PresetAction(() => { store.Apply(presets.SelectedIndex); CamSettings.Save(); CamRuntime.Reset(); }, CameraText.PresetLoaded);
        if (LegacyUI.Button(ui.Row(28f), CameraText.OverwritePreset.Get()))
            PresetAction(() => store.SavePreset(store.Names[presets.SelectedIndex], true), CameraText.PresetUpdated);
        if (LegacyUI.Button(ui.Row(28f), CameraText.DeletePreset.Get()))
            PresetAction(() => { store.Delete(presets.SelectedIndex); RefreshPresets(); }, CameraText.PresetDeleted);
        GUI.enabled = enabled;
        LegacyUI.Label(ui.Row(22f), status());
    }

    private void PresetAction(Action action, LegacyLocalizedText message)
    {
        try { action(); status = message.Get; }
        catch (Exception ex) { status = () => CameraText.PresetActionFailed.Get() + ": " + ex.Message; Debug.LogWarning("[ECC] Presets: " + ex.Message); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  PATCH — Apply Prefix
//  Выполняется ДО update()/UpWorldPos(). Ставим cam_pos_node_pos и fovy
//  здесь чтобы изменения применились В ЭТОМ ЖЕ кадре.
//  control — object (безопасно даже если Control недоступен извне сборки)
// ═══════════════════════════════════════════════════════════════════════════
[HarmonyPatch]
public static class Patch_Apply_Prefix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("Cam"); if (t == null) return null;
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (m.Name == "Apply") return m;
        return null;
    }

    // Кэш данных этого кадра, чтобы Postfix не читал CarPh повторно
    [ThreadStatic] internal static CarPhAccess.Data FrameData;

    static void Prefix(object __instance, object control)
    {
        FrameData = default(CarPhAccess.Data);
        CamRuntime.InCar = false;
        if (!LegacyHelpers.IsGameplayRunning || !EnhancedCameraMode.IsActive(__instance as Cam)) return;
        try
        {
            CF.TryCache(__instance);

            var data = CarPhAccess.Read(control);
            FrameData = data;
            CamRuntime.InCar = data.valid;

            if (!data.valid)
            {
                // Не в машине — не трогаем позицию/fov
                return;
            }

            float speedKmh = data.speedMs * 3.6f;
            float dt = UnityEngine.Time.deltaTime;
            CamRuntime.UpdateDist(speedKmh, dt);

            var cam = (Cam)__instance;
            cam.cur_slot.pos = new Vector3(0f, CamSettings.SlotPosY, CamSettings.SlotPosZ);
            var curSlot = CF.fiCurSlot?.GetValue(__instance);
            if (curSlot != null)
                CF.fiSlotFovy?.SetValue(curSlot, CamSettings.SlotFovY);

            // ВАЖНО: здесь ставим только СТАТИЧНУЮ базу (SlotPosZ/SlotPosY).
            // Динамический offset от скорости (SmoothedDist) применяется
            // отдельно в Postfix — путь через cam_pos_node_pos ненадёжен,
            // так как update()/FromObject может перезаписать это значение
            // до того как UpWorldPos успеет его прочитать.
            CF.fiNodePos?.SetValue(__instance, new Vector3(0f, CamSettings.SlotPosY, CamSettings.SlotPosZ));
        }
        catch (Exception ex) { Debug.LogError("[ECC] Apply Prefix: " + ex.Message); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  PATCH — Apply Postfix
//  speed_vec уже посчитан внутри update(). Модифицируем last_set_tr.tr
//  напрямую — это гарантированно последний штрих перед ApplyToRealCam.
// ═══════════════════════════════════════════════════════════════════════════
[HarmonyPatch]
public static class Patch_Apply_Postfix
{
    static MethodBase TargetMethod()
    {
        var t = AccessTools.TypeByName("Cam"); if (t == null) return null;
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            if (m.Name == "Apply") return m;
        return null;
    }

    static void Postfix(object __instance, Quaternion b_rot)
    {
        if (!LegacyHelpers.IsGameplayRunning || !EnhancedCameraMode.IsActive(__instance as Cam)) return;
        if (!CamRuntime.InCar) return; // не в машине — эффекты не нужны
        try
        {
            CF.TryCache(__instance);
            if (CF.fiLastSetTr == null || CF.fiTr == null) return;

            Vector3 pos; Quaternion rot;
            if (!CF.GetTrPosRot(__instance, out pos, out rot)) return;

            var data = Patch_Apply_Prefix.FrameData;
            float dt = UnityEngine.Time.deltaTime;
            Vector3 speedVecWorld = CF.GetSpeedVec(__instance);

            CamRuntime.UpdateEffects(speedVecWorld, b_rot, data.isFly, data.vertVel, dt);

            // FromObject can change the selected slot during the original Apply.
            if (!EnhancedCameraMode.IsActive(__instance as Cam)) return;
            bool changed = false;

            float totalPitch = CamRuntime.SmoothedPitch + CamRuntime.JumpPitch;
            if (Mathf.Abs(totalPitch) > 0.01f)
            {
                rot = Quaternion.AngleAxis(totalPitch, rot * Vector3.right) * rot;
                changed = true;
            }
            if (Mathf.Abs(CamRuntime.JumpPosY) > 0.001f)
            {
                pos += Vector3.up * CamRuntime.JumpPosY;
                changed = true;
            }
            if (Mathf.Abs(CamRuntime.SmoothedRoll) > 0.01f)
            {
                rot = Quaternion.AngleAxis(CamRuntime.SmoothedRoll, rot * Vector3.forward) * rot;
                changed = true;
            }
            if (Mathf.Abs(CamRuntime.DriftLag) > 0.01f)
            {
                rot = Quaternion.AngleAxis(CamRuntime.DriftLag, Vector3.up) * rot;
                changed = true;
            }
            // Динамический pull-back дистанции — надёжный путь (см. коммент в Prefix)
            if (Mathf.Abs(CamRuntime.SmoothedDist) > 0.001f)
            {
                pos += rot * new Vector3(0f, 0f, -CamRuntime.SmoothedDist);
                changed = true;
            }
            // Инерция — пружинный отскок на резком тормозе/старте
            if (Mathf.Abs(CamRuntime.InertiaOffset) > 0.001f)
            {
                pos += rot * new Vector3(0f, 0f, CamRuntime.InertiaOffset);
                changed = true;
            }
            Vector3 shake = CamRuntime.GetShake();
            if (shake.sqrMagnitude > 0.000001f)
            {
                pos += rot * shake;
                changed = true;
            }

            if (changed) CF.SetTrPosRot(__instance, pos, rot);
        }
        catch (Exception ex) { Debug.LogError("[ECC] Apply Postfix: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(FirstScene), "Start")]
public static class Patch_FirstScene
{
    static void Postfix() => ModEnhancedCameraBootstrap.Apply();
}
