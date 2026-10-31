### [Русский](./README_RU.md) | [> English <](./README.md)
---

# Template.Basic

A minimal example for MadOut2 PC 9.4 using BepInEx 5. Only BaseUnityPlugin and ConfigEntry are used; there are no BCL API or UI dependencies.

1. Change `PluginGuid` (it must be unique), `PluginName` and `PluginVersion` in `Plugin.cs`; update `AssemblyName`, `Version`, `Authors` and `RootNamespace` in the csproj. If you change the namespace, update both source files when `WindowCursor.cs` is present.
2. Install the .NET SDK and .NET Framework 4.7.2 reference assemblies / Developer Pack. NuGet is required for Publicizer 0.4.3. Game and dependency DLLs are read from your local installation.
3. Copy `Directory.Build.props.example` to `Directory.Build.props` and set `GameDir`, or pass the path on the command line:

```powershell
dotnet build Template.Basic.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

By default, `GameDir` points to `../../prod_game` relative to the project. `ManagedDir` and `BepInExDir` can be overridden separately. Explicit paths take priority. Missing files produce `BCLTPL001` warnings with the affected paths, then stop the build with `BCLTPL002`. `Private=false` prevents game and shared dependency DLLs from being copied to the output.

Output: `bin/Release/Template.Basic.dll`. Copy only your own DLL into a separate folder under `BepInEx/plugins`. To copy automatically while the game is closed, add `-p:CopyToPlugins=true` (default: false).

The template targets the client. For server features, keep their initialization separate from UI and hotkey handling. Linux uses the same managed DLLs; any native resources you add must be built separately for the target platform.
