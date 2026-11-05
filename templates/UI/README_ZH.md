### [俄文](./README_RU.md) | [English](./README.md) | [> 简体中文 <](./README_ZH.md)

---

# Template.UI

适用于 MadOut2 PC 9.4 的最小示例，使用 BepInEx 5 和 LegacyUIFramework。按 F8 打开窗口。窗口使用 Window 输入阻止模式，并单独请求游戏原生光标，在关闭淡出结束后释放请求。

1. 在 `Plugin.cs` 中修改 `PluginGuid`（必须唯一）、`PluginName` 和 `PluginVersion`；在 csproj 中更新 `AssemblyName`、`Version`、`Authors` 和 `RootNamespace`。如果更改命名空间且项目包含 `WindowCursor.cs`，请同时更新两个源文件。
2. 安装 .NET SDK 和 .NET Framework 4.7.2 引用程序集 / Developer Pack。Publicizer 0.4.3 需要 NuGet。游戏及依赖 DLL 从本地安装目录读取。
3. 将 `Directory.Build.props.example` 复制为 `Directory.Build.props` 并设置 `GameDir`，或在命令行中传入路径：

```powershell
dotnet build Template.UI.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

默认情况下，`GameDir` 指向相对于项目的 `../../prod_game`。可分别覆盖 `ManagedDir` 和 `BepInExDir`。通过 `LegacyUIFrameworkDll` 指定 UI 库。显式指定的路径优先。缺少文件时会输出带具体路径的 `BCLTPL001` 警告，然后以 `BCLTPL002` 停止构建。`Private=false` 可防止游戏 DLL 和共享依赖 DLL 被复制到输出目录。

输出文件为 `bin/Release/Template.UI.dll`。只需将自己的 DLL 复制到 `BepInEx/plugins` 下的独立文件夹。若要在游戏关闭时自动复制，请添加 `-p:CopyToPlugins=true`（默认值为 false）。

`LegacyUIFramework.dll` 应单独安装为共享库；本示例不依赖 `BigCityLegacy.dll`。不要在每个插件旁重复分发框架 DLL。缩放使用插件自己的 `LegacyUIScaleSettings` 实例，不会更改其他窗口的全局设置。

本模板面向客户端。开发服务器功能时，请将其初始化与 UI、快捷键处理分开。Linux 使用相同的托管 DLL；如果添加原生资源，必须为目标平台单独构建。
