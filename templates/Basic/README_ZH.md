### [俄文](./README_RU.md) | [English](./README.md) | [> 简体中文 <](./README_ZH.md)

---

# Template.Basic

适用于 MadOut2 PC 9.4 的最小示例，使用 BepInEx 5。仅使用 BaseUnityPlugin 和 ConfigEntry，不依赖 BCL API 或 UI。

1. 在 `Plugin.cs` 中修改 `PluginGuid`（必须唯一）、`PluginName` 和 `PluginVersion`；在 csproj 中更新 `AssemblyName`、`Version`、`Authors` 和 `RootNamespace`。如果更改命名空间且项目包含 `WindowCursor.cs`，请同时更新两个源文件。
2. 安装 .NET SDK 和 .NET Framework 4.7.2 引用程序集 / Developer Pack。Publicizer 0.4.3 需要 NuGet。游戏及依赖 DLL 从本地安装目录读取。
3. 将 `Directory.Build.props.example` 复制为 `Directory.Build.props` 并设置 `GameDir`，或在命令行中传入路径：

```powershell
dotnet build Template.Basic.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

默认情况下，`GameDir` 指向相对于项目的 `../../prod_game`。可分别覆盖 `ManagedDir` 和 `BepInExDir`。显式指定的路径优先。缺少文件时会输出带具体路径的 `BCLTPL001` 警告，然后以 `BCLTPL002` 停止构建。`Private=false` 可防止游戏 DLL 和共享依赖 DLL 被复制到输出目录。

输出文件为 `bin/Release/Template.Basic.dll`。只需将自己的 DLL 复制到 `BepInEx/plugins` 下的独立文件夹。若要在游戏关闭时自动复制，请添加 `-p:CopyToPlugins=true`（默认值为 false）。

本模板面向客户端。开发服务器功能时，请将其初始化与 UI、快捷键处理分开。Linux 使用相同的托管 DLL；如果添加原生资源，必须为目标平台单独构建。
