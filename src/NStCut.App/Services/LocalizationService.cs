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
    public string MenuNesting => this["MenuNesting"];
    public string MenuLanguage => this["MenuLanguage"];
    public string MenuTheme => this["MenuTheme"];
    public string LabelSearchPath => this["LabelSearchPath"];
    public string LabelOutputPath => this["LabelOutputPath"];
    public string LabelSheetMargin => this["LabelSheetMargin"];
    public string LabelPartGap => this["LabelPartGap"];
    public string LabelZoom => this["LabelZoom"];
    public string LabelMaxSearchPasses => this["LabelMaxSearchPasses"];
    public string LabelNoImprovementStopCount => this["LabelNoImprovementStopCount"];
    public string LabelAcceptableMaxUnplaced => this["LabelAcceptableMaxUnplaced"];
    public string LabelAcceptableMaxSheets => this["LabelAcceptableMaxSheets"];
    public string ButtonBrowse => this["ButtonBrowse"];
    public string ButtonFindFiles => this["ButtonFindFiles"];
    public string ButtonNest => this["ButtonNest"];
    public string ButtonExportDxf => this["ButtonExportDxf"];
    public string ButtonRenestCurrentSheet => this["ButtonRenestCurrentSheet"];
    public string MenuExportCombinedDxf => this["MenuExportCombinedDxf"];
    public string HintSheetMargin => this["HintSheetMargin"];
    public string HintPartGap => this["HintPartGap"];
    public string HintMaxSearchPasses => this["HintMaxSearchPasses"];
    public string HintNoImprovementStopCount => this["HintNoImprovementStopCount"];
    public string HintAcceptableMaxUnplaced => this["HintAcceptableMaxUnplaced"];
    public string HintAcceptableMaxSheets => this["HintAcceptableMaxSheets"];
    public string SheetLabel => this["SheetLabel"];
    public string MaterialLabel => this["MaterialLabel"];
    public string NoGrain => this["NoGrain"];
    public string GrainHorizontal => this["GrainHorizontal"];
    public string GrainVertical => this["GrainVertical"];
    public string ColumnDirection => this["ColumnDirection"];
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
    public string ColumnSeq => this["ColumnSeq"];
    public string ColumnEnabled => this["ColumnEnabled"];
    public string ColumnNumber => this["ColumnNumber"];
    public string ColumnTitle => this["ColumnTitle"];
    public string ColumnQty => this["ColumnQty"];
    public string ColumnPartName => this["ColumnPartName"];
    public string ColumnDxfFile => this["ColumnDxfFile"];
    public string ColumnDimensions => this["ColumnDimensions"];
    public string ColumnMaterial => this["ColumnMaterial"];
    public string ColumnGrain => this["ColumnGrain"];
    public string ColumnRotate => this["ColumnRotate"];
    public string ColumnStatus => this["ColumnStatus"];
    public string ButtonMaterialsLibrary => this["ButtonMaterialsLibrary"];
    public string ButtonAddToProject => this["ButtonAddToProject"];
    public string ButtonRemoveFromProject => this["ButtonRemoveFromProject"];
    public string ButtonDeleteRow => this["ButtonDeleteRow"];
    public string ButtonCopyMaterial => this["ButtonCopyMaterial"];
    public string TooltipCopyMaterial => this["TooltipCopyMaterial"];
    public string ButtonSelectFileManually => this["ButtonSelectFileManually"];
    public string ButtonOpenDxfFolder => this["ButtonOpenDxfFolder"];
    public string ButtonOpenReport => this["ButtonOpenReport"];
    public string ButtonClear => this["ButtonClear"];
    public string ContextMenuFindFile => this["ContextMenuFindFile"];
    public string ContextMenuSelectManually => this["ContextMenuSelectManually"];
    public string ContextMenuResolveAmbiguous => this["ContextMenuResolveAmbiguous"];
    public string ContextMenuCopyDxfPath => this["ContextMenuCopyDxfPath"];
    public string ContextMenuDeleteRow => this["ContextMenuDeleteRow"];
    public string TooltipActiveMaterial => this["TooltipActiveMaterial"];
    public string SectionSourceDestination => this["SectionSourceDestination"];
    public string SectionDetails => this["SectionDetails"];
    public string ErrorTitleNesting => this["ErrorTitleNesting"];
    public string ErrorTitleExport => this["ErrorTitleExport"];
    public string ErrorTitleSave => this["ErrorTitleSave"];
    public string ErrorTitleOpen => this["ErrorTitleOpen"];
    public string ErrorTitleImport => this["ErrorTitleImport"];
    public string ErrorTitleFindFiles => this["ErrorTitleFindFiles"];
    public string ErrorTitleUnexpected => this["ErrorTitleUnexpected"];
    public string ClearRowsTitle => this["ClearRowsTitle"];
    public string ClearRowsConfirmation => this["ClearRowsConfirmation"];
    public string CandidateSelectTitle => this["CandidateSelectTitle"];
    public string CandidateSelectPrompt => this["CandidateSelectPrompt"];
    public string ButtonOk => this["ButtonOk"];
    public string ButtonCancel => this["ButtonCancel"];
    public string SheetPreviewTitle => this["SheetPreviewTitle"];
    public string SheetPreviewTabFormat => this["SheetPreviewTabFormat"];
    public string SheetPreviewFormat => this["SheetPreviewFormat"];
    public string StatusOversizedFormat => this["StatusOversizedFormat"];
    public string StatusOversizedGrainBlockedFormat => this["StatusOversizedGrainBlockedFormat"];

    private LocalizationService()
    {
        _resources["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StCut",
            ["MenuFile"] = "File",
            ["MenuOpenProject"] = "Open project",
            ["MenuSaveProject"] = "Save project",
            ["MenuNesting"] = "Nesting",
            ["MenuLanguage"] = "Language",
            ["MenuTheme"] = "Theme",
            ["LabelSearchPath"] = "DXF Search Folder",
            ["LabelOutputPath"] = "Output Folder",
            ["LabelSheetMargin"] = "Sheet edge margin, mm",
            ["LabelPartGap"] = "Gap between parts, mm",
            ["LabelZoom"] = "Zoom",
            ["LabelMaxSearchPasses"] = "Max search passes",
            ["LabelNoImprovementStopCount"] = "Stop after no improvement",
            ["LabelAcceptableMaxUnplaced"] = "Acceptable unplaced",
            ["LabelAcceptableMaxSheets"] = "Acceptable sheets",
            ["ButtonBrowse"] = "Browse",
            ["ButtonFindFiles"] = "Find Files",
            ["ButtonNest"] = "Nest",
            ["ButtonExportDxf"] = "Export DXF",
            ["ButtonRenestCurrentSheet"] = "Renest sheet",
            ["MenuExportCombinedDxf"] = "Combined DXF",
            ["HintSheetMargin"] = "Margin between part edges and the sheet border.\nRecommended: 5–10 mm for plasma/oxy cutting, 0–2 mm for layout-only DXF.",
            ["HintPartGap"] = "Minimum distance between placed parts.\nRecommended: material thickness + 1–2 mm, or kerf if cutting.",
            ["HintMaxSearchPasses"] = "Maximum number of layout attempts.\nMore passes may find a better result but take longer.\nRecommended: 30–100.",
            ["HintNoImprovementStopCount"] = "Stop early when the best result has not improved for this many passes.\n0 = never stop early.\nRecommended: 10–30.",
            ["HintAcceptableMaxUnplaced"] = "If the best layout places all but this many parts, stop searching immediately.\n-1 to disable. Useful for quick 'good enough' results.",
            ["HintAcceptableMaxSheets"] = "If the best layout uses no more than this many sheets, stop searching immediately.\n-1 to disable.",
            ["SheetLabel"] = "Sheet",
            ["MaterialLabel"] = "Material",
            ["NoGrain"] = "No grain",
            ["GrainHorizontal"] = "Horizontal",
            ["GrainVertical"] = "Vertical",
            ["ColumnDirection"] = "Direction",
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
            ["ColumnSeq"] = "#",
            ["ColumnEnabled"] = "On",
            ["ColumnNumber"] = "Number",
            ["ColumnTitle"] = "Title",
            ["ColumnQty"] = "Qty",
            ["ColumnPartName"] = "Part Name",
            ["ColumnDxfFile"] = "DXF File",
            ["ColumnDimensions"] = "Dimensions",
            ["ColumnMaterial"] = "Material",
            ["ColumnGrain"] = "Texture",
            ["ColumnRotate"] = "Rotate",
            ["ColumnStatus"] = "Status",
            ["ButtonMaterialsLibrary"] = "Library",
            ["ButtonAddToProject"] = "Add to project >",
            ["ButtonRemoveFromProject"] = "< Remove from project",
            ["ButtonDeleteRow"] = "Delete Row",
            ["ButtonCopyMaterial"] = "Copy",
            ["TooltipCopyMaterial"] = "Copy selected material",
            ["ButtonSelectFileManually"] = "Select File Manually",
            ["ButtonOpenDxfFolder"] = "Open DXF",
            ["ButtonOpenReport"] = "Open Report",
            ["ButtonClear"] = "Clear",
            ["ContextMenuFindFile"] = "Find file",
            ["ContextMenuSelectManually"] = "Select DXF manually",
            ["ContextMenuResolveAmbiguous"] = "Resolve ambiguous",
            ["ContextMenuCopyDxfPath"] = "Copy DXF path",
            ["ContextMenuDeleteRow"] = "Delete row",
            ["TooltipActiveMaterial"] = "Click to make this material the active paste target",
            ["SectionSourceDestination"] = "Source / destination",
            ["SectionDetails"] = "Parts",
            ["ErrorTitleNesting"] = "Nesting error",
            ["ErrorTitleExport"] = "Export error",
            ["ErrorTitleSave"] = "Save error",
            ["ErrorTitleOpen"] = "Open error",
            ["ErrorTitleImport"] = "Import error",
            ["ErrorTitleFindFiles"] = "Find files error",
            ["ErrorTitleUnexpected"] = "Unexpected error",
            ["ClearRowsTitle"] = "Clear parts list",
            ["ClearRowsConfirmation"] = "Clear all parts from the table? This action can be undone.",
            ["CandidateSelectTitle"] = "Select DXF file",
            ["CandidateSelectPrompt"] = "Several files match. Select one:",
            ["ButtonOk"] = "OK",
            ["ButtonCancel"] = "Cancel",
            ["SheetPreviewTitle"] = "Sheet Preview",
            ["SheetPreviewTabFormat"] = "Sheet {0}",
            ["SheetPreviewFormat"] = "{0}  {1}x{2} mm  utilization {3:F1}%",
            ["StatusOversizedFormat"] = "Oversized: part {0:F1} × {1:F1} mm does not fit in usable sheet {2:F1} × {3:F1} mm.",
            ["StatusOversizedGrainBlockedFormat"] = "Oversized: part {0:F1} × {1:F1} mm does not fit in usable sheet {2:F1} × {3:F1} mm (rotation blocked by grain)."
        };

        _resources["ua"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WindowTitle"] = "N.StCut",
            ["MenuFile"] = "Файл",
            ["MenuOpenProject"] = "Відкрити проект",
            ["MenuSaveProject"] = "Зберегти проект",
            ["MenuNesting"] = "Розкрій",
            ["MenuLanguage"] = "Мова",
            ["MenuTheme"] = "Тема",
            ["LabelSearchPath"] = "Папка пошуку DXF",
            ["LabelOutputPath"] = "Папка збереження",
            ["LabelSheetMargin"] = "Відступ від краю листа, мм",
            ["LabelPartGap"] = "Відстань між деталями, мм",
            ["LabelZoom"] = "Масштаб",
            ["LabelMaxSearchPasses"] = "Макс. кількість проходів",
            ["LabelNoImprovementStopCount"] = "Зупинка без покращень",
            ["LabelAcceptableMaxUnplaced"] = "Допуст. нерозміщ.",
            ["LabelAcceptableMaxSheets"] = "Допуст. листів",
            ["ButtonBrowse"] = "Огляд",
            ["ButtonFindFiles"] = "Знайти файли",
            ["ButtonNest"] = "Розкрій",
            ["ButtonExportDxf"] = "Експорт DXF",
            ["ButtonRenestCurrentSheet"] = "Переразкроїти лист",
            ["MenuExportCombinedDxf"] = "Загальний DXF",
            ["HintSheetMargin"] = "Відступ від краю листа до деталі.\nРекомендація: 5–10 мм для плазми/газу, 0–2 мм лише для розкладки DXF.",
            ["HintPartGap"] = "Мінімальна відстань між деталями.\nРекомендація: товщина матеріалу + 1–2 мм, або ширина різу.",
            ["HintMaxSearchPasses"] = "Максимальна кількість спроб розкладу.\nБільше — кращий результат, але довше.\nРекомендація: 30–100.",
            ["HintNoImprovementStopCount"] = "Зупинитися, якщо результат не покращився за вказану кількість проходів.\n0 — не зупинятися раніше.\nРекомендація: 10–30.",
            ["HintAcceptableMaxUnplaced"] = "Зупинитися, якщо залишилось не більше вказаної кількості нерозміщених деталей.\n-1 — вимкнено. Корисно для швидкого 'достатньо хорошого' результату.",
            ["HintAcceptableMaxSheets"] = "Зупинитися, якщо використано не більше вказаної кількості листів.\n-1 — вимкнено.",
            ["SheetLabel"] = "Лист",
            ["MaterialLabel"] = "Матеріал",
            ["NoGrain"] = "Без текстури",
            ["GrainHorizontal"] = "Горизонтальна",
            ["GrainVertical"] = "Вертикальна",
            ["ColumnDirection"] = "Напрямок",
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
            ["ColumnSeq"] = "#",
            ["ColumnEnabled"] = "Вкл",
            ["ColumnNumber"] = "Номер",
            ["ColumnTitle"] = "Назва",
            ["ColumnQty"] = "К-ть",
            ["ColumnPartName"] = "Деталь",
            ["ColumnDxfFile"] = "DXF файл",
            ["ColumnDimensions"] = "Габарит",
            ["ColumnMaterial"] = "Матеріал",
            ["ColumnGrain"] = "Текстура",
            ["ColumnRotate"] = "Поворот",
            ["ColumnStatus"] = "Статус",
            ["ButtonMaterialsLibrary"] = "Бібліотека",
            ["ButtonAddToProject"] = "Додати в проект >",
            ["ButtonRemoveFromProject"] = "< Прибрати з проекту",
            ["ButtonDeleteRow"] = "Видалити рядок",
            ["ButtonCopyMaterial"] = "Копіювати",
            ["TooltipCopyMaterial"] = "Копіювати вибраний матеріал",
            ["ButtonSelectFileManually"] = "Вибір файлу вручну",
            ["ButtonOpenDxfFolder"] = "Відкрити DXF",
            ["ButtonOpenReport"] = "Відкрити звіт",
            ["ButtonClear"] = "Очистити",
            ["ContextMenuFindFile"] = "Знайти файл",
            ["ContextMenuSelectManually"] = "Вибрати DXF вручну",
            ["ContextMenuResolveAmbiguous"] = "Вирішити неоднозначність",
            ["ContextMenuCopyDxfPath"] = "Копіювати шлях DXF",
            ["ContextMenuDeleteRow"] = "Видалити рядок",
            ["TooltipActiveMaterial"] = "Клікніть, щоб зробити цей матеріал активним для вставки",
            ["SectionSourceDestination"] = "Джерело / призначення",
            ["SectionDetails"] = "Деталі",
            ["ErrorTitleNesting"] = "Помилка розкрою",
            ["ErrorTitleExport"] = "Помилка експорту",
            ["ErrorTitleSave"] = "Помилка збереження",
            ["ErrorTitleOpen"] = "Помилка відкриття",
            ["ErrorTitleImport"] = "Помилка імпорту",
            ["ErrorTitleFindFiles"] = "Помилка пошуку файлів",
            ["ErrorTitleUnexpected"] = "Неочікувана помилка",
            ["ClearRowsTitle"] = "Очистити список деталей",
            ["ClearRowsConfirmation"] = "Очистити всі деталі з таблиці? Дію можна скасувати командою Undo.",
            ["CandidateSelectTitle"] = "Вибір DXF файлу",
            ["CandidateSelectPrompt"] = "Знайдено кілька файлів. Виберіть один:",
            ["ButtonOk"] = "OK",
            ["ButtonCancel"] = "Скасувати",
            ["SheetPreviewTitle"] = "Попередній перегляд листа",
            ["SheetPreviewTabFormat"] = "Лист {0}",
            ["SheetPreviewFormat"] = "{0}  {1}x{2} мм  заповнюваність {3:F1}%",
            ["StatusOversizedFormat"] = "Перевищує розмір: деталь {0:F1} × {1:F1} мм не вміщується в робочу зону листа {2:F1} × {3:F1} мм.",
            ["StatusOversizedGrainBlockedFormat"] = "Перевищує розмір: деталь {0:F1} × {1:F1} мм не вміщується в робочу зону листа {2:F1} × {3:F1} мм (поворот заблокований текстурою)."
        };
    }
}
