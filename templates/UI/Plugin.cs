using BepInEx;
using BigCityLegacy.UI;
using UnityEngine;

namespace TemplatePlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.example.template.ui";
        public const string PluginName = "UI Plugin Template";
        public const string PluginVersion = "1.0.0";
        private readonly LegacyUIScaleSettings scale = new LegacyUIScaleSettings { BaseScale = 1.1f };
        private LegacyUIWindow window;
        private WindowCursor cursor;
        private bool value;

        private void Awake()
        {
            LegacyInput.Init();
            cursor = new WindowCursor(transform);
            window = new LegacyUIWindow(PluginName, new Rect(120, 100, 380, 180),
                new LegacyUIWindowOptions { ShowCloseButton = true, InputBlockMode = LegacyUIInputBlockMode.Window });
            window.Visible = false;
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) window.Visible = !window.Visible;
        }
        private void LateUpdate() { cursor.SetVisible(window.IsRendered); }
        private void OnGUI()
        {
            using (new LegacyUIGuiScope(-20000, scale))
                window.Draw(area =>
                {
                    value = LegacyUI.Toggle(new Rect(area.x, area.y, area.width, 28), value, "Example toggle");
                    LegacyUI.Label(new Rect(area.x, area.y + 38, area.width, 28), "F8: open / close");
                });
        }
        private void OnDisable() { if (window != null) window.Close(); if (cursor != null) cursor.SetVisible(false); }
        private void OnDestroy() { if (cursor != null) cursor.Dispose(); }
    }
}
