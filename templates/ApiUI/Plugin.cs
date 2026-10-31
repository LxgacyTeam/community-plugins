using System;
using BepInEx;
using BepInEx.Configuration;
using BigCityLegacy.Api;
using BigCityLegacy.UI;
using UnityEngine;

namespace TemplatePlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bigcitylegacy.baseplugin", "1.4.2")]
    [LegacyPluginActivation(RequiresRestart = false)]
    public sealed class Plugin : BaseUnityPlugin, ILegacyPluginActivation
    {
        public const string PluginGuid = "com.example.template.apiui";
        public const string PluginName = "API and UI Plugin Template";
        public const string PluginVersion = "1.0.0";
        private readonly LegacyUIScaleSettings scale = new LegacyUIScaleSettings { BaseScale = 1.1f };
        private ConfigEntry<bool> featureEnabled;
        private LegacyUIWindow window;
        private WindowCursor cursor;
        private bool busy;
        private int exampleCounter;

        public bool IsEnabled => featureEnabled.Value;
        // This example switches immediately. A map/resource plugin should use
        // RequiresRestart=true and report its actual startup state instead.
        public void Enable() { featureEnabled.Value = true; Config.Save(); }
        public void Disable() { featureEnabled.Value = false; Config.Save(); }

        private void Awake()
        {
            featureEnabled = Config.Bind("General", "Enabled", true, "Enable the example counter.");
            LegacyInput.Init();
            cursor = new WindowCursor(transform);
            window = new LegacyUIWindow(PluginName, new Rect(120, 100, 420, 270),
                new LegacyUIWindowOptions { ShowCloseButton = true, InputBlockMode = LegacyUIInputBlockMode.Window });
            window.Visible = false;
        }
        private void Update() { if (Input.GetKeyDown(KeyCode.F8)) window.Visible = !window.Visible; }
        private void LateUpdate() { cursor.SetVisible(window.IsRendered); }
        private void OnGUI()
        {
            using (new LegacyUIGuiScope(-20000, scale))
                window.Draw(area =>
                {
                    GUI.enabled = !busy;
                    LegacyUI.Label(new Rect(area.x, area.y, area.width, 28), "Feature: " + (IsEnabled ? "enabled" : "disabled"));
                    if (LegacyUI.Button(new Rect(area.x, area.y + 34, area.width, 28), "Toggle via BCL API")) ToggleFeature();
                    if (LegacyUI.Button(new Rect(area.x, area.y + 68, area.width, 28), "Confirm and count: " + exampleCounter)) ConfirmCount();
                    if (LegacyUI.Button(new Rect(area.x, area.y + 102, area.width, 28), "Restart game...")) Restart();
                });
        }
        private async void ToggleFeature()
        {
            busy = true;
            try { await LegacyPlugins.SetEnabledAsync(PluginGuid, !IsEnabled); }
            catch (Exception error) { Logger.LogError(error); LegacyAlerts.Small(error.Message, "Activation failed"); }
            finally { busy = false; }
        }
        private async void ConfirmCount()
        {
            busy = true;
            try
            {
                bool yes = await LegacyDialogs.ShowAsync(new LegacyDialogOptions
                {
                    Title = "Counter", Message = "Increment the example counter?",
                    YesBtnText = "Count", NoBtnText = "Cancel", YesBtnType = LegacyDialogButtonType.Green
                });
                if (yes && IsEnabled) { exampleCounter++; LegacyAlerts.Hint("Counter: " + exampleCounter); }
            }
            catch (Exception error) { Logger.LogError(error); }
            finally { busy = false; }
        }
        private async void Restart()
        {
            busy = true;
            try { await LegacyGame.RestartAsync(); }
            catch (Exception error) { Logger.LogError(error); }
            finally { busy = false; }
        }
        private void OnDisable() { if (window != null) window.Close(); if (cursor != null) cursor.SetVisible(false); }
        private void OnDestroy() { if (cursor != null) cursor.Dispose(); }
    }
}
