using System.ComponentModel;
using System.Globalization;

namespace NStCut.App.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _culture = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Culture
    {
        get => _culture;
        set
        {
            if (_culture == value) return;
            _culture = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    public string this[string key]
    {
        get
        {
            if (_resources.TryGetValue(_culture, out var dict) && dict.TryGetValue(key, out var value))
                return value;
            if (_resources.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var fb))
                return fb;
            return key;
        }
    }

    public string WindowTitle => this["WindowTitle"];
    public string MenuFile => this["MenuFile"];
    public string MenuOpenProject => this["MenuOpenProject"];
    public string MenuSaveProject => this["MenuSaveProject"];
    public string MenuLanguage => this["MenuLanguage"];
    public string LabelSearchPath => this["LabelSearchPath"];
    public string LabelOutputPath => this["LabelOutputPath"];
    public string ButtonBrowse => this["ButtonBrowse"];
    public string ButtonFindFiles => this["ButtonFindFiles"];
    public string ButtonNest => this["ButtonNest"];
    public string ButtonExportDxf => this["ButtonExportDxf"];
    public string ButtonExportReport => this["ButtonExportReport"];
    public string ButtonAddRow => this["ButtonAddRow"];
    public string ButtonMaterials => this["ButtonMaterials"];
    public string ButtonUndo => this["ButtonUndo"];
    public string ButtonPaste => this["ButtonPaste"];
    public string ButtonImportExcel => this["ButtonImportExcel"];
    public string StatTotal => this["StatTotal"];
    public string StatFound => this["StatFound"];
    public string StatNotFound => this["StatNotFound"];
    public string StatAmbiguous => this["StatAmbiguous"];
    public string ColumnQty => this["ColumnQty"];
    public string ColumnPartName => this["ColumnPartName"];
    public string ColumnDxfFile => this["ColumnDxfFile"];
    public string ColumnMaterial => this["ColumnMaterial"];
    public string ColumnGrain => this["ColumnGrain"];
    public string ColumnRotate => this["ColumnRotate"];
    public string ColumnStatus => this["ColumnStatus"];

    private LocalizationService()
    {
        _resources["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StCut",
            ["MenuFile"] = "File",
            ["MenuOpenProject"] = "Open project",
            ["MenuSaveProject"] = "Save project",
            ["MenuLanguage"] = "Language",
            ["LabelSearchPath"] = "DXF Search Folder",
            ["LabelOutputPath"] = "Output Folder",
            ["ButtonBrowse"] = "Browse",
            ["ButtonFindFiles"] = "Find Files",
            ["ButtonNest"] = "Nest",
            ["ButtonExportDxf"] = "Export DXF",
            ["ButtonExportReport"] = "Export Report",
            ["ButtonAddRow"] = "Add Row",
            ["ButtonMaterials"] = "Materials",
            ["ButtonUndo"] = "Undo",
            ["ButtonPaste"] = "Paste Excel",
            ["ButtonImportExcel"] = "Import Excel",
            ["StatTotal"] = "Total:",
            ["StatFound"] = "Found:",
            ["StatNotFound"] = "Not Found:",
            ["StatAmbiguous"] = "Ambiguous:",
            ["ColumnQty"] = "Qty",
            ["ColumnPartName"] = "Part Name",
            ["ColumnDxfFile"] = "DXF File",
            ["ColumnMaterial"] = "Material",
            ["ColumnGrain"] = "Grain",
            ["ColumnRotate"] = "Rotate",
            ["ColumnStatus"] = "Status"
        };

        _resources["ua"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StCut",
            ["MenuFile"] = "Файл",
            ["MenuOpenProject"] = "Відкрити проект",
            ["MenuSaveProject"] = "Зберегти проект",
            ["MenuLanguage"] = "Мова",
            ["LabelSearchPath"] = "Папка пошуку DXF",
            ["LabelOutputPath"] = "Папка збереження",
            ["ButtonBrowse"] = "Огляд",
            ["ButtonFindFiles"] = "Знайти файли",
            ["ButtonNest"] = "Розкрій",
            ["ButtonExportDxf"] = "Експорт DXF",
            ["ButtonExportReport"] = "Експорт звіту",
            ["ButtonAddRow"] = "Додати рядок",
            ["ButtonMaterials"] = "Матеріали",
            ["ButtonUndo"] = "Скасувати",
            ["ButtonPaste"] = "Вставити з Excel",
            ["ButtonImportExcel"] = "Імпорт Excel",
            ["StatTotal"] = "Всього:",
            ["StatFound"] = "Знайдено:",
            ["StatNotFound"] = "Не знайдено:",
            ["StatAmbiguous"] = "Неоднозначно:",
            ["ColumnQty"] = "К-ть",
            ["ColumnPartName"] = "Деталь",
            ["ColumnDxfFile"] = "DXF файл",
            ["ColumnMaterial"] = "Матеріал",
            ["ColumnGrain"] = "Текстура",
            ["ColumnRotate"] = "Поворот",
            ["ColumnStatus"] = "Статус"
        };
    }
}
