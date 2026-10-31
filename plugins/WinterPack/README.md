# WinterPack
### Автор плагина: [dvwhrf](https://t.me/dvwhrf)

Плагин на зимнюю погоду для BigCityLegacy / MadOut2 PC 9.4.

---

## Установка

Распакуйте `releases/WinterPack-1.0.3.zip` в корень игры.

**Поддерживается только Windows версия игры!**

Для переключения погоды в игре перейдите:
Настройки → TODO → **Время года**: **Зима** / **Лето**. По умолчанию — лето.
Выбор сохраняется профиле игрока.

---

## Сборка

### Требования:
- MadOut2 PC 9.4 с BigCityLegacy 1.4.0+
- .NET SDK
- Unity 2019.1.8f1 

`build.ps1` собирает bundle в `assetbundle/`, проверяет его и встраивает в DLL.
   Можно передать `-GameDir`, `-UnityExe`, `-BclDll`.

Для сборки DLL с готовым bundle:
```powershell
dotnet build -c Release -p:GameDir="path\to\game\dir" -p:CopyToPlugins=true
```

Путь к bundle можно переопределить через `-p:AssetBundlePath=...`.
