using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using UnityEngine;

internal sealed class CamParameter
{
    private readonly LegacyLocalizedText group;
    internal readonly LegacyLocalizedText LabelText;
    public string Group => group.Get();
    public string Label => LabelText.Get();
    public readonly float Min, Max;
    private readonly FieldInfo field;

    public CamParameter(string name, LegacyLocalizedText group, LegacyLocalizedText label, float min, float max)
    {
        field = typeof(CamSettings).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new ArgumentException("Unknown camera parameter: " + name);
        this.group = group; LabelText = label; Min = min; Max = max;
    }
    public float Get() => (float)field.GetValue(null);
    public void Set(float value) => field.SetValue(null, Mathf.Clamp(value, Min, Max));
}

internal static class CamParameters
{
    // Order is the v1 preset format; append new parameters only with a schema migration.
    internal static readonly CamParameter[] All =
    {
        new CamParameter("SlotFovY", CameraText.CameraGroup, new LegacyLocalizedText("Field of view (°)", "Поле зрения (°)"), 30f, 90f),
        new CamParameter("SlotPosZ", CameraText.CameraGroup, new LegacyLocalizedText("Distance Z (m)", "Расстояние Z (м)"), -15f, -2f),
        new CamParameter("SlotPosY", CameraText.CameraGroup, new LegacyLocalizedText("Height Y (m)", "Высота Y (м)"), 0.5f, 4f),
        new CamParameter("SpeedPitch", CameraText.EffectsGroup, new LegacyLocalizedText("Speed pitch (°)", "Наклон на скорости (°)"), 0f, 15f),
        new CamParameter("JumpPitch", CameraText.EffectsGroup, new LegacyLocalizedText("Jump pitch (°)", "Наклон в прыжке (°)"), 0f, 25f),
        new CamParameter("JumpPosOffset", CameraText.EffectsGroup, new LegacyLocalizedText("Jump position offset (m)", "Смещение в прыжке (м)"), 0f, 1.5f),
        new CamParameter("JumpMinAirTime", CameraText.EffectsGroup, new LegacyLocalizedText("Minimum airborne time (s)", "Мин. время в воздухе (с)"), 0f, 0.4f),
        new CamParameter("JumpVelDeadzone", CameraText.EffectsGroup, new LegacyLocalizedText("Jump velocity dead zone (m/s)", "Мёртвая зона скорости прыжка (м/с)"), 0f, 4f),
        new CamParameter("GforceRoll", CameraText.EffectsGroup, new LegacyLocalizedText("Cornering roll (°)", "Крен в поворотах (°)"), 0f, 15f),
        new CamParameter("GforceMinSpeed", CameraText.EffectsGroup, new LegacyLocalizedText("Minimum roll speed (km/h)", "Мин. скорость для крена (км/ч)"), 10f, 80f),
        new CamParameter("DriftLagStrength", CameraText.EffectsGroup, new LegacyLocalizedText("Drift lag (°)", "Отставание в заносе (°)"), 0f, 20f),
        new CamParameter("DriftLagCatchUp", CameraText.EffectsGroup, new LegacyLocalizedText("Catch-up time (s)", "Время догоняния (с)"), 0.05f, 1f),
        new CamParameter("InertiaStrength", CameraText.EffectsGroup, new LegacyLocalizedText("Inertia strength", "Сила инерции"), 0f, 0.15f),
        new CamParameter("InertiaBounce", CameraText.EffectsGroup, new LegacyLocalizedText("Inertia bounce", "Отскок инерции"), 0f, 1f),
        new CamParameter("ShakeStrength", CameraText.EffectsGroup, new LegacyLocalizedText("Shake strength", "Сила тряски"), 0f, 3f),
        new CamParameter("ShakeStartKmh", CameraText.EffectsGroup, new LegacyLocalizedText("Shake start speed (km/h)", "Начало тряски (км/ч)"), 20f, 150f),
        new CamParameter("SpeedDistAdd", CameraText.SpeedGroup, new LegacyLocalizedText("Speed distance bonus (m)", "Отдаление на скорости (м)"), 0f, 8f),
        new CamParameter("DistOutTime", CameraText.SpeedGroup, new LegacyLocalizedText("Pull-back time (s)", "Время отдаления (с)"), 0.05f, 2f),
        new CamParameter("DistInTime", CameraText.SpeedGroup, new LegacyLocalizedText("Return time (s)", "Время приближения (с)"), 0.01f, 0.5f)
    };
}

public sealed class CameraPreset
{
    public string Name;
    public float[] Values;
}

public sealed class CameraPresetFile
{
    public int Version = 1;
    public List<CameraPreset> Presets = new List<CameraPreset>();
}

internal sealed class CamPresetStore
{
    private readonly string path;
    private CameraPresetFile data = new CameraPresetFile();
    private bool loadFailed;
    private static readonly XmlSerializer serializer = new XmlSerializer(typeof(CameraPresetFile));
    public CamPresetStore(string path) { this.path = path; }
    public string[] Names => data.Presets.ConvertAll(preset => preset.Name).ToArray();

    public void Load()
    {
        if (!File.Exists(path)) return;
        loadFailed = true;
        CameraPresetFile loaded;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using (var reader = XmlReader.Create(path, settings)) loaded = (CameraPresetFile)serializer.Deserialize(reader);
        if (loaded == null || loaded.Version != 1 || loaded.Presets == null)
            throw new CamLocalizedException(CameraText.UnsupportedFormat);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in loaded.Presets)
        {
            Validate(preset);
            if (!names.Add(preset.Name)) throw new CamLocalizedException(CameraText.DuplicateName);
        }
        data = loaded;
        loadFailed = false;
    }

    private static void Validate(CameraPreset preset)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.Name) || preset.Name.Length > 64 ||
            preset.Values == null || preset.Values.Length != CamParameters.All.Length)
            throw new CamLocalizedException(CameraText.InvalidPreset);
        for (int i = 0; i < preset.Values.Length; i++)
        {
            float value = preset.Values[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < CamParameters.All[i].Min || value > CamParameters.All[i].Max)
                throw new CamLocalizedException(CameraText.InvalidValue, CamParameters.All[i].LabelText);
        }
    }

    public void SavePreset(string name, bool replace)
    {
        name = (name ?? "").Trim();
        var preset = new CameraPreset { Name = name, Values = new float[CamParameters.All.Length] };
        for (int i = 0; i < preset.Values.Length; i++) preset.Values[i] = CamParameters.All[i].Get();
        Validate(preset);
        var updated = new CameraPresetFile { Presets = new List<CameraPreset>(data.Presets) };
        int index = updated.Presets.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && !replace) throw new CamLocalizedException(CameraText.NameExists);
        if (index >= 0) updated.Presets[index] = preset;
        else updated.Presets.Add(preset);
        Write(updated);
    }

    public void Apply(int index)
    {
        CameraPreset preset = data.Presets[index];
        Validate(preset);
        for (int i = 0; i < preset.Values.Length; i++) CamParameters.All[i].Set(preset.Values[i]);
    }

    public void Delete(int index)
    {
        var updated = new CameraPresetFile { Presets = new List<CameraPreset>(data.Presets) };
        updated.Presets.RemoveAt(index);
        Write(updated);
    }

    private void Write(CameraPresetFile updated)
    {
        // Preserve unreadable files until the user has inspected or moved them.
        if (loadFailed) throw new CamLocalizedException(CameraText.DamagedFile);
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var writer = new StreamWriter(temporary)) serializer.Serialize(writer, updated);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            data = updated;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
