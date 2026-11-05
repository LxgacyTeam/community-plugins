using System;

// Keep bilingual text until draw time so game language changes also update
// already-created parameters, status messages and preset validation errors.
internal static class CameraText
{
    internal static readonly LegacyLocalizedText WindowTitle = new LegacyLocalizedText("Enhanced Chase Camera", "Enhanced Chase Camera");
    internal static readonly LegacyLocalizedText SettingsTab = new LegacyLocalizedText("Settings", "Настройки");
    internal static readonly LegacyLocalizedText PresetsTab = new LegacyLocalizedText("Presets", "Пресеты");
    internal static readonly LegacyLocalizedText InfoTab = new LegacyLocalizedText("Info", "Информация");
    internal static readonly LegacyLocalizedText CameraGroup = new LegacyLocalizedText("Camera", "Камера");
    internal static readonly LegacyLocalizedText EffectsGroup = new LegacyLocalizedText("Effects", "Эффекты");
    internal static readonly LegacyLocalizedText SpeedGroup = new LegacyLocalizedText("Speed", "Скорость");
    internal static readonly LegacyLocalizedText ModeActive = new LegacyLocalizedText("Enhanced Chase mode is active", "Режим Enhanced Chase активен");
    internal static readonly LegacyLocalizedText SelectMode = new LegacyLocalizedText("Select Enhanced Chase using the camera cycle", "Выберите Enhanced Chase штатной сменой камеры");
    internal static readonly LegacyLocalizedText MenuHelp = new LegacyLocalizedText("Shift+C — open or close settings.", "Shift+C — открыть или закрыть настройки.");
    internal static readonly LegacyLocalizedText CycleHelp = new LegacyLocalizedText("In a vehicle, use the game's camera switch button.", "В автомобиле переключайте камеру штатной кнопкой.");
    internal static readonly LegacyLocalizedText ModeOrderHelp = new LegacyLocalizedText("Enhanced Chase follows the first-person view.", "Enhanced Chase идёт после вида от первого лица.");
    internal static readonly LegacyLocalizedText NextModeHelp = new LegacyLocalizedText("Switch again to return to the standard view.", "Следующее переключение возвращает обычный вид.");
    internal static readonly LegacyLocalizedText PresetsHelp = new LegacyLocalizedText("Presets save all camera and effect settings.", "Пресеты сохраняют все параметры камеры и эффектов.");
    internal static readonly LegacyLocalizedText LiveSpeed = new LegacyLocalizedText("Speed: {0} km/h", "Скорость: {0} км/ч");
    internal static readonly LegacyLocalizedText Reset = new LegacyLocalizedText("Reset settings", "Сброс настроек");
    internal static readonly LegacyLocalizedText Save = new LegacyLocalizedText("Save", "Сохранить");
    internal static readonly LegacyLocalizedText Close = new LegacyLocalizedText("Close", "Закрыть");
    internal static readonly LegacyLocalizedText PresetName = new LegacyLocalizedText("New preset name", "Имя нового пресета");
    internal static readonly LegacyLocalizedText SavePreset = new LegacyLocalizedText("Save new preset", "Сохранить новый пресет");
    internal static readonly LegacyLocalizedText LoadPreset = new LegacyLocalizedText("Load selected", "Загрузить выбранный");
    internal static readonly LegacyLocalizedText OverwritePreset = new LegacyLocalizedText("Overwrite selected", "Перезаписать выбранный");
    internal static readonly LegacyLocalizedText DeletePreset = new LegacyLocalizedText("Delete selected", "Удалить выбранный");
    internal static readonly LegacyLocalizedText NoPresets = new LegacyLocalizedText("No saved presets", "Нет сохранённых пресетов");
    internal static readonly LegacyLocalizedText PresetSaved = new LegacyLocalizedText("Preset saved", "Пресет сохранён");
    internal static readonly LegacyLocalizedText PresetLoaded = new LegacyLocalizedText("Preset loaded", "Пресет загружен");
    internal static readonly LegacyLocalizedText PresetUpdated = new LegacyLocalizedText("Preset updated", "Пресет обновлён");
    internal static readonly LegacyLocalizedText PresetDeleted = new LegacyLocalizedText("Preset deleted", "Пресет удалён");
    internal static readonly LegacyLocalizedText PresetReadFailed = new LegacyLocalizedText("Could not read presets", "Не удалось прочитать пресеты");
    internal static readonly LegacyLocalizedText PresetActionFailed = new LegacyLocalizedText("Preset operation failed", "Ошибка работы с пресетами");
    internal static readonly LegacyLocalizedText UnsupportedFormat = new LegacyLocalizedText("Unsupported preset format.", "Неподдерживаемый формат пресетов.");
    internal static readonly LegacyLocalizedText DuplicateName = new LegacyLocalizedText("Duplicate preset name.", "Повторяющееся имя пресета.");
    internal static readonly LegacyLocalizedText InvalidPreset = new LegacyLocalizedText("Invalid camera preset.", "Некорректный пресет камеры.");
    internal static readonly LegacyLocalizedText InvalidValue = new LegacyLocalizedText("Invalid value: ", "Некорректное значение: ");
    internal static readonly LegacyLocalizedText NameExists = new LegacyLocalizedText("This name already exists. Use overwrite.", "Такое имя уже есть. Используйте перезапись.");
    internal static readonly LegacyLocalizedText DamagedFile = new LegacyLocalizedText("Preset file is damaged. Back it up and move it out of BepInEx/config.", "Файл пресетов повреждён. Сохраните его копию и уберите из BepInEx/config.");
}

internal sealed class CamLocalizedException : Exception
{
    private readonly LegacyLocalizedText text;
    private readonly LegacyLocalizedText? detail;

    internal CamLocalizedException(LegacyLocalizedText text, LegacyLocalizedText? detail = null)
        : base(text.English)
    { this.text = text; this.detail = detail; }

    public override string Message => text.Get() + (detail.HasValue ? detail.Value.Get() : string.Empty);
}
