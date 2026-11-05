### [Русский](./README_RU.md) | [> English <](./README.md) | [简体中文](./README_ZH.md)

---

# Template.ApiUI

A minimal example for MadOut2 PC 9.4 using BepInEx 5, LegacyUIFramework and BCL 1.4.2+ with the activation/dialog API. Press F8 to open the window. It uses Window input blocking and a separate native cursor request, released after the closing fade.

1. Change `PluginGuid` (it must be unique), `PluginName` and `PluginVersion` in `Plugin.cs`; update `AssemblyName`, `Version`, `Authors` and `RootNamespace` in the csproj. If you change the namespace, update both source files when `WindowCursor.cs` is present.
2. Install the .NET SDK and .NET Framework 4.7.2 reference assemblies / Developer Pack. NuGet is required for Publicizer 0.4.3. Game and dependency DLLs are read from your local installation.
3. Copy `Directory.Build.props.example` to `Directory.Build.props` and set `GameDir`, or pass the path on the command line:

```powershell
dotnet build Template.ApiUI.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

By default, `GameDir` points to `../../prod_game` relative to the project. `ManagedDir` and `BepInExDir` can be overridden separately. Use `LegacyUIFrameworkDll` for the UI library and `BigCityLegacyDll` for BCL. Explicit paths take priority. Missing files produce `BCLTPL001` warnings with the affected paths, then stop the build with `BCLTPL002`. `Private=false` prevents game and shared dependency DLLs from being copied to the output.

Output: `bin/Release/Template.ApiUI.dll`. Copy only your own DLL into a separate folder under `BepInEx/plugins`. To copy automatically while the game is closed, add `-p:CopyToPlugins=true` (default: false).

The example uses `LegacyPlugins.SetEnabledAsync`, `LegacyDialogs.ShowAsync`, `LegacyAlerts.Hint` and `LegacyGame.RestartAsync` with confirmation. All calls use asynchronous `await`: do not use `.Result`/`.Wait()` on Unity's main thread. `ILegacyPluginActivation` here actually switches the counter feature without restarting, so `RequiresRestart=false`. The window stays available when the feature is disabled. For maps/resources with `RequiresRestart=true`, save the setting in `Enable`/`Disable` and report the actual state of the current process separately; do not restart from these methods. The Restart button demonstrates the shared helper and really restarts the game after approval. Do not test it on a dedicated server.

The template targets the client. For server features, keep their initialization separate from UI and hotkey handling. Linux uses the same managed DLLs; any native resources you add must be built separately for the target platform.
