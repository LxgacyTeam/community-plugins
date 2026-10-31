using BepInEx;
using BepInEx.Configuration;

namespace TemplatePlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.example.template.basic";
        public const string PluginName = "Basic Plugin Template";
        public const string PluginVersion = "1.0.0";
        private ConfigEntry<bool> featureEnabled;

        private void Awake()
        {
            featureEnabled = Config.Bind("General", "Enabled", true, "Enable the example feature.");
            Logger.LogInfo(PluginName + " loaded; Enabled=" + featureEnabled.Value);
            // Initialize your feature here. No BCL API or UI dependency.
        }
    }
}
