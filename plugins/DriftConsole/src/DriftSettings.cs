using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MadOutDrift
{
    internal sealed class DriftSettings
    {
        public const float DefaultPowerMultiplier = 3f;
        public const float DefaultSteerAngle = 75f;
        public const float DefaultSteerSensitivity = 0.30f;
        public const float DefaultReturnSensitivity = 0.50f;
        public const float DefaultSteerAssistStrength = 0.60f;
        public const float DefaultFrontLatGrip = 1f;
        public const float DefaultFrontLongGrip = 1f;
        public const float DefaultRearLatGrip = 0.80f;
        public const float DefaultRearLongGrip = 1f;

        public bool EnableAll = true;
        public bool EnableDriftScore = true;
        public bool EnableSteerAssist = true;
        public float PowerMultiplier = DefaultPowerMultiplier;
        public float SteerAngle = DefaultSteerAngle;
        public float SteerSensitivity = DefaultSteerSensitivity;
        public float ReturnSensitivity = DefaultReturnSensitivity;
        public float SteerAssistStrength = DefaultSteerAssistStrength;
        public float FrontLatGrip = DefaultFrontLatGrip;
        public float FrontLongGrip = DefaultFrontLongGrip;
        public float RearLatGrip = DefaultRearLatGrip;
        public float RearLongGrip = DefaultRearLongGrip;
    }

    internal static class DriftSettingsStore
    {
        public static DriftSettings Load(string path)
        {
            // Always start from defaults; a missing/invalid key must never become zero.
            var settings = new DriftSettings();
            if (!File.Exists(path)) return settings;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                int separator = trimmed.IndexOf('=');
                if (separator < 0) continue;
                string key = trimmed.Substring(0, separator).Trim();
                string text = trimmed.Substring(separator + 1).Trim();
                switch (key)
                {
                    case "EnableAll": settings.EnableAll = ReadBool(text, settings.EnableAll); break;
                    case "EnableDriftScore": settings.EnableDriftScore = ReadBool(text, settings.EnableDriftScore); break;
                    case "EnableSteerAssist": settings.EnableSteerAssist = ReadBool(text, settings.EnableSteerAssist); break;
                    case "PowerMultiplier": settings.PowerMultiplier = ReadFloat(text, settings.PowerMultiplier, 0.1f, 5f); break;
                    case "SteerAngle": settings.SteerAngle = ReadFloat(text, settings.SteerAngle, 10f, 90f); break;
                    case "SteerSensitivity": settings.SteerSensitivity = ReadFloat(text, settings.SteerSensitivity, 0.1f, 5f); break;
                    case "ReturnSensitivity": settings.ReturnSensitivity = ReadFloat(text, settings.ReturnSensitivity, 0.1f, 5f); break;
                    case "SteerAssistStrength": settings.SteerAssistStrength = ReadFloat(text, settings.SteerAssistStrength, 0f, 1f); break;
                    case "FrontLatGrip": settings.FrontLatGrip = ReadFloat(text, settings.FrontLatGrip, 0.1f, 5f); break;
                    case "FrontLongGrip": settings.FrontLongGrip = ReadFloat(text, settings.FrontLongGrip, 0.1f, 5f); break;
                    case "RearLatGrip": settings.RearLatGrip = ReadFloat(text, settings.RearLatGrip, 0.1f, 5f); break;
                    case "RearLongGrip": settings.RearLongGrip = ReadFloat(text, settings.RearLongGrip, 0.1f, 5f); break;
                }
            }
            return settings;
        }

        private static bool ReadBool(string text, bool fallback)
        {
            bool parsed;
            return bool.TryParse(text, out parsed) ? parsed : fallback;
        }

        private static float ReadFloat(string text, float fallback, float min, float max)
        {
            float parsed;
            // Existing files may have been written under either RU or EN culture.
            // No thousands separators: "0,30" is a decimal, never thirty.
            string normalized = text.Replace(',', '.');
            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
                !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max)
                return parsed;
            return fallback;
        }

        public static void Save(string path, DriftSettings settings)
        {
            string text = "# MadOut2 Drift Config (invariant decimal format)\n\n" +
                "EnableAll=" + settings.EnableAll + "\n" +
                "EnableDriftScore=" + settings.EnableDriftScore + "\n" +
                "PowerMultiplier=" + Format(settings.PowerMultiplier) + "\n" +
                "SteerAngle=" + Format(settings.SteerAngle) + "\n" +
                "SteerSensitivity=" + Format(settings.SteerSensitivity) + "\n" +
                "ReturnSensitivity=" + Format(settings.ReturnSensitivity) + "\n" +
                "EnableSteerAssist=" + settings.EnableSteerAssist + "\n" +
                "SteerAssistStrength=" + Format(settings.SteerAssistStrength) + "\n" +
                "FrontLatGrip=" + Format(settings.FrontLatGrip) + "\n" +
                "FrontLongGrip=" + Format(settings.FrontLongGrip) + "\n" +
                "RearLatGrip=" + Format(settings.RearLatGrip) + "\n" +
                "RearLongGrip=" + Format(settings.RearLongGrip) + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
