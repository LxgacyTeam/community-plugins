### [Русский](./README_RU.md) | [> English <](./README.md) | [简体中文](./README_ZH.md)

---

# WinterPack

### Plugin author: [dvwhrf](https://t.me/dvwhrf)

A winter weather plugin for BigCityLegacy / MadOut2 PC 9.4.

---

## Installation

Extract `releases/WinterPack-1.0.3.zip` into the game root directory.

**Only the Windows version of the game is supported!**

To switch seasons in-game, go to:
Settings → TODO → **Season**: **Winter** / **Summer**. Summer is the default.
Your choice is saved in the player profile.

---

## Building

### Requirements

- MadOut2 PC 9.4 with BigCityLegacy 1.4.0+

- .NET SDK

- Unity 2019.1.8f1

`build.ps1` builds the bundle in `assetbundle/`, validates it and embeds it in the DLL.
You can pass `-GameDir`, `-UnityExe` and `-BclDll`.

To build the DLL using an existing bundle:

```powershell
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

Override the bundle path with `-p:AssetBundlePath=...`.
