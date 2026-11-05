### [俄文](./README_RU.md) | [English](./README.md) | [> 简体中文 <](./README_ZH.md)

---

# WinterPack

### 插件作者：[dvwhrf](https://t.me/dvwhrf)

适用于 BigCityLegacy / MadOut2 PC 9.4 的冬季天气插件。

---

## 安装

将 `releases/WinterPack-1.0.3.zip` 解压到游戏根目录。

**仅支持 Windows 版游戏！**

在游戏中切换季节，请前往：
设置 → TODO → **Season（季节）**：**Winter（冬季）** / **Summer（夏季）**。默认是夏季。
选择会保存在玩家档案中。

---

## 构建

### 环境要求

- MadOut2 PC 9.4 和 BigCityLegacy 1.4.0+

- .NET SDK

- Unity 2019.1.8f1

`build.ps1` 会在 `assetbundle/` 中构建资源包、验证资源包并将其嵌入 DLL。
可传入 `-GameDir`、`-UnityExe` 和 `-BclDll` 参数。

使用已有资源包构建 DLL：

```powershell
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

可通过 `-p:AssetBundlePath=...` 指定其他资源包路径。
