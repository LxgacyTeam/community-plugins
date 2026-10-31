### [> Русский <](./README_RU.md) | [English](./README.md)
---

# Template.ApiUI

Минимальный пример для MadOut2 PC 9.4: BepInEx 5 и LegacyUIFramework и BCL 1.4.2+ с API активации/диалогов. Открытие окна — F8; окно использует Window input blocking и отдельный запрос нативного курсора, снимаемый после fade.

1. Измените PluginGuid (он должен быть уникальным), PluginName, PluginVersion в Plugin.cs; AssemblyName, Version, Authors и RootNamespace в csproj. При смене namespace обновите оба исходника, если есть WindowCursor.cs.
2. Установите .NET SDK и reference assemblies .NET Framework 4.7.2 / Developer Pack. NuGet нужен для Publicizer 0.4.3. Игровые DLL и DLL зависимостей берутся из локальной установки.
3. Скопируйте Directory.Build.props.example в Directory.Build.props и задайте GameDir либо передайте путь в командной строке:

```powershell
dotnet build Template.ApiUI.csproj -c Release '-p:GameDir=D:\Games\MadOut2'
```

По умолчанию GameDir указывает на `../../prod_game` относительно проекта. ManagedDir и BepInExDir можно переопределить отдельно. Для UI задаётся LegacyUIFrameworkDll; для BCL — BigCityLegacyDll. Явно заданные пути имеют приоритет. При отсутствии файлов выводятся адресные warnings BCLTPL001, затем сборка останавливается с BCLTPL002. Private=false исключает копирование игровых и общих DLL в результат.

Результат: `bin/Release/Template.ApiUI.dll`. Скопируйте только свою DLL в отдельную папку `BepInEx/plugins`. Для автоматического копирования при закрытой игре добавьте `-p:CopyToPlugins=true` (по умолчанию false).


Пример использует LegacyPlugins.SetEnabledAsync, LegacyDialogs.ShowAsync, LegacyAlerts.Hint и LegacyGame.RestartAsync с подтверждением. Все await асинхронные: не используйте .Result/.Wait() в Unity main thread. ILegacyPluginActivation здесь действительно переключает функцию счётчика без перезапуска, поэтому RequiresRestart=false. Окно остаётся доступным при выключенной функции. Для карты/ресурсов с RequiresRestart=true сохраняйте настройку в Enable/Disable и отдельно возвращайте фактическое состояние текущего процесса; перезапуск из этих методов не вызывается. Кнопка Restart демонстрирует общий helper и действительно перезапускает игру после согласия. Не тестируйте её на dedicated-сервере.

Шаблон ориентирован на клиент. Для серверных функций отделяйте их инициализацию от UI и обработки горячих клавиш. На Linux используются те же управляемые DLL; нативные ресурсы, если добавляете их, собираются отдельно под нужную платформу.
