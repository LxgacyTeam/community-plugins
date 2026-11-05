using BepInEx;

[BepInPlugin("com.madout.enhancedcamera", "Enhanced Chase Camera", "2.3.1")]
[BepInDependency("com.bigcitylegacy.baseplugin", "1.4.0")]
public class EnhancedCameraPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        ModEnhancedCameraBootstrap.Apply();
        Logger.LogInfo("Enhanced Chase Camera loaded!");
    }
}