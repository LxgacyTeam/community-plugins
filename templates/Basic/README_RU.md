### [> Русский <](./README_RU.md) | [English](./README.md)
---

# Template.Basic

Минимальный пример для MadOut2 PC 9.4: BepInEx 5. Только BaseUnityPlugin и ConfigEntry; зависимостей от BCL API и UI нет.

1. Измените PluginGuid (он должен быть уникальным), PluginName, PluginVersion в Plugin.cs; AssemblyName, Version, Authors и RootNamespace в csproj. При смене namespace обновите оба исходника, если есть WindowCursor.cs.
2. Установите .NET SDK и reference assemblies .NET Framework 4.7.2 / Developer Pack. NuGet нужен для Publicizer 0.4.3. Игровые DLL и DLL зависимостей берутся из локальной установки.
3. Скопируйте Directory.Build.props.example в Directory.Build.props и задайте GameDir либо передайте путь в командной строке:

```powershell
dotnet build Template.Basic.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

По умолчанию GameDir указывает на `../../prod_game` относительно проекта. ManagedDir и BepInExDir можно переопределить отдельно. Явно заданные пути имеют приоритет. При отсутствии файлов выводятся адресные warnings BCLTPL001, затем сборка останавливается с BCLTPL002. Private=false исключает копирование игровых и общих DLL в результат.

Результат: `bin/Release/Template.Basic.dll`. Скопируйте только свою DLL в отдельную папку `BepInEx/plugins`. Для автоматического копирования при закрытой игре добавьте `-p:CopyToPlugins=true` (по умолчанию false).




Шаблон ориентирован на клиент. Для серверных функций отделяйте их инициализацию от UI и обработки горячих клавиш. На Linux используются те же управляемые DLL; нативные ресурсы, если добавляете их, собираются отдельно под нужную платформу.
