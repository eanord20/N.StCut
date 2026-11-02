using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ExcelDataReader;
using Microsoft.Win32;
using NStCut.App.Commands;
using NStCut.App.Converters;
using NStCut.App.Models;
using NStCut.App.Services;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;
using NStCut.Core.Services;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace NStCut.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly IFileResolveService _fileResolveService;
    private readonly IProjectFileService _projectService;
    private readonly IAppSettingsService _appSettingsService;

    private readonly INestingEngine _nestingEngine;
    private readonly IDxfGeometryService _dxfGeometry;
    private readonly IDxfExportService _dxfExport;
    private readonly IReportService _reportService;
    private readonly IExportFileNameService _exportFileNameService;
    private readonly IMaterialsLibraryService _materialsLibraryService;

    private string _searchPath = string.Empty;
    private string _outputPath = string.Empty;
    private string _projectName = "N.StCut Project";
    private string _statusText = string.Empty;
    private string _qualitySummary = string.Empty;
    private string? _oversizedStatusText;
    private double _progressValue;
    private bool _isBusy;
    private PartRow? _selectedRow;
    private readonly INestingResultManager _nestingResultManager;
    private CancellationTokenSource? _currentCts;
    private string? _activePasteMaterialId;
    private double _sheetMarginMm = DefaultNestingOptions.Value.SheetMarginMm;
    private double _partGapMm = DefaultNestingOptions.Value.PartGapMm;
    private int _maxSearchPasses = DefaultNestingOptions.Value.MaxSearchPasses;
    private int _noImprovementStopCount = DefaultNestingOptions.Value.NoImprovementStopCount;
    private int _acceptableMaxUnplaced = DefaultNestingOptions.Value.AcceptableMaxUnplaced;
    private int _acceptableMaxSheets = DefaultNestingOptions.Value.AcceptableMaxSheets;
    private string? _lastNestingLogPath;
    private readonly string _sessionLogPath;
    private bool _suspendRecompute;
    private bool _loadingSettings;
    private bool _exportCombinedDxf;
    private bool _allowMarginRelax;
    private AppTheme _currentTheme = AppTheme.Light;

    private sealed class Snapshot(List<PartRow> rows, List<MaterialRow> materials)
    {
        public List<PartRow> Rows { get; } = rows;
        public List<MaterialRow> Materials { get; } = materials;
    }

    private readonly LinkedList<Snapshot> _undoStack = new();
    // Полное клонирование всего списка деталей на каждый снэпшот сохранено намеренно (не переходим
    // на дельта-модель — см. docs/Refactoring_Plan.md, Conflict Resolution, п.1: корректность важнее
    // производительности согласно приоритетам AGENTS.md). Единственная безопасная оптимизация без
    // изменения модели — снизить глубину истории (было 50), чтобы ограничить пиковый объём памяти,
    // удерживаемой историей Undo (docs/Audit_Performance.md, issue P3, Memory Analysis).
    private const int MaxUndoDepth = 20;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(INestingEngine nestingEngine, IDxfGeometryService dxfGeometry,
        IDxfExportService dxfExport, IReportService reportService,
        IFileResolveService? fileResolveService = null,
        IProjectFileService? projectService = null,
        IAppSettingsService? appSettingsService = null,
        IExportFileNameService? exportFileNameService = null,
        IMaterialsLibraryService? materialsLibraryService = null,
        INestingResultManager? nestingResultManager = null)
    {
        _nestingEngine = nestingEngine;
        _dxfGeometry = dxfGeometry;
        _dxfExport = dxfExport;
        _reportService = reportService;
        // Опциональные параметры с DI по интерфейсу — позволяют подставлять тестовые реализации
        // (см. docs/Audit_Architecture.md, SOLID Assessment, D), при этом единственная существующая
        // точка создания (App.xaml.cs) не требует изменений.
        _fileResolveService = fileResolveService ?? new FileResolveService();
        _projectService = projectService ?? new ProjectFileService();
        _appSettingsService = appSettingsService ?? new AppSettingsService();
        _exportFileNameService = exportFileNameService ?? new ExportFileNameService();
        _materialsLibraryService = materialsLibraryService ?? new MaterialsLibraryService();
        _nestingResultManager = nestingResultManager ?? new NestingResultManager();
        _nestingResultManager.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(INestingResultManager.HasResult))
            {
                ((RelayCommand?)ExportDxfCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand?)ExportReportCommand)?.RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();
            }
            else if (e.PropertyName == nameof(INestingResultManager.HasLastReportPath))
            {
                ((RelayCommand?)OpenReportCommand)?.RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        };
        _sessionLogPath = BuildSessionLogPath();

        RowsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Rows);
        // Группируем с конвертацией MaterialId -> имя материала сразу здесь (простой IValueConverter,
        // без MultiBinding в XAML) — group.Name становится готовым для отображения именем материала.
        RowsView.GroupDescriptions!.Add(new PropertyGroupDescription(nameof(PartRow.MaterialId), new MaterialIdToNameConverter(Materials)));
        RowsView.CustomSort = new RowSectionComparer(this);
        // Живая пересортировка/перегруппировка вместо принудительного RowsView.Refresh() на каждое
        // единичное изменение строки (см. docs/Code_Audit_UI.md, issue C2): Refresh() приводит к полному
        // Reset — WPF пересоздаёт все контейнеры групп/строк заново. IsLiveSorting/IsLiveGrouping
        // заставляют CollectionView реагировать на PropertyChanged точечно, без полного пересоздания.
        RowsView.IsLiveSorting = true;
        RowsView.LiveSortingProperties.Add(nameof(PartRow.RowNumberInSection));
        RowsView.IsLiveGrouping = true;
        RowsView.LiveGroupingProperties.Add(nameof(PartRow.MaterialId));

        Rows.CollectionChanged += OnRowsCollectionChanged;
        Materials.CollectionChanged += OnMaterialsCollectionChanged;

        BrowseSearchPathCommand = new RelayCommand(_ => BrowseSearchPath());
        BrowseOutputPathCommand = new RelayCommand(_ => BrowseOutputPath());
        AddRowCommand = new RelayCommand(_ => AddRow());
        DeleteSelectedRowsCommand = new RelayCommand(_ => DeleteSelectedRows(), _ => SelectedRow != null);
        DeleteRowCommand = new RelayCommand(row => DeleteRow(row as PartRow), row => row is PartRow);
        ClearAllCommand = new RelayCommand(_ => ClearAll(), _ => Rows.Count > 0 && !IsBusy);
        FindFilesCommand = new RelayCommand(_ => FindFiles(), _ => !string.IsNullOrWhiteSpace(SearchPath));
        PasteFromClipboardCommand = new RelayCommand(_ => PasteFromClipboard());
        ImportExcelFileCommand = new RelayCommand(_ => ImportExcelFile());
        OpenMaterialsCommand = new RelayCommand(_ => OpenMaterialsWindow());
        OpenReportSettingsCommand = new RelayCommand(_ => OpenReportSettingsWindow());
        NestCommand = new RelayCommand(_ => Nest(), _ => CanNest());
        ExportDxfCommand = new RelayCommand(_ => ExportDxf(), _ => _nestingResultManager.HasResult && !IsBusy);
        ExportReportCommand = new RelayCommand(_ => ExportReport(), _ => _nestingResultManager.HasResult && !IsBusy);
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => Directory.Exists(OutputPath));
        OpenReportCommand = new RelayCommand(_ => OpenReport(), _ => _nestingResultManager.HasLastReportPath);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsBusy);
        UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => Rows.Count > 0);
        OpenProjectCommand = new RelayCommand(_ => OpenProject());
        SelectFileManuallyCommand = new RelayCommand(row => SelectFileManually(row as PartRow), row => row is PartRow);
        SelectAmbiguousFileCommand = new RelayCommand(row => SelectAmbiguousFile(row as PartRow), row => row is PartRow p && p.Status == PartStatus.Ambiguous);
        CopyDxfPathCommand = new RelayCommand(row => CopyDxfPath(row as PartRow), row => row is PartRow p && !string.IsNullOrWhiteSpace(p.DxfPath));
        OpenDxfFileCommand = new RelayCommand(row => OpenDxfFile(row as PartRow), row => row is PartRow p && p.Status == PartStatus.Found && !string.IsNullOrWhiteSpace(p.DxfPath) && File.Exists(p.DxfPath));
        ResolveStatusCommand = new RelayCommand(row => ResolveStatus(row as PartRow));
        IncreaseQuantityCommand = new RelayCommand(row => (row as PartRow)?.IncreaseQuantity());
        DecreaseQuantityCommand = new RelayCommand(row => (row as PartRow)?.DecreaseQuantity());
        SetActiveMaterialCommand = new RelayCommand(param => SetActiveMaterial(param as string));
        DeleteMaterialCommand = new RelayCommand(param => DeleteMaterial(param as string), param => param is string id && Materials.Any(m => m.Id == id));
        AddMaterialCommand = new RelayCommand(param => AddMaterial(param));
        SetLanguageCommand = new RelayCommand(lang => SetLanguage(lang));
        SetThemeCommand = new RelayCommand(theme => SetTheme(theme));
        AboutCommand = new RelayCommand(_ => ShowAbout());

        LoadAppSettings();
        RecomputeCanNest();
    }

    /// <summary>
    /// Ширины столбцов таблицы деталей (MainWindow.PartsGrid) по имени колонки — читаются/пишутся
    /// напрямую окном (MainWindow.xaml.cs) при загрузке/закрытии, ViewModel только хранит и
    /// передаёт их в AppSettingsService, не зная о самом DataGrid (см. AppSettings.PartsGridColumnWidths).
    /// </summary>
    public Dictionary<string, double> PartsGridColumnWidths { get; private set; } = new();

    /// <summary>Ширины столбцов таблицы библиотеки материалов (MaterialsWindow), сохраняются в AppSettings.</summary>
    public Dictionary<string, double> MaterialsGridColumnWidths { get; private set; } = new();

    /// <summary>Восстанавливает папки поиска/сохранения, параметры раскроя, язык и тему из прошлого запуска.</summary>
    private void LoadAppSettings()
    {
        _loadingSettings = true;
        try
        {
            var settings = _appSettingsService.Load();
            if (!string.IsNullOrWhiteSpace(settings.SearchPath)) SearchPath = settings.SearchPath;
            if (!string.IsNullOrWhiteSpace(settings.OutputPath)) OutputPath = settings.OutputPath;
            if (double.IsFinite(settings.SheetMarginMm) && settings.SheetMarginMm >= 0)
                SheetMarginMm = settings.SheetMarginMm;
            if (double.IsFinite(settings.PartGapMm) && settings.PartGapMm >= 0)
                PartGapMm = settings.PartGapMm;
            // MaxSearchPasses / NoImprovementStopCount / AcceptableMax* are hidden from the UI
            // and always use the quality-oriented defaults from AppConstants.
            if (!string.IsNullOrWhiteSpace(settings.Culture)) _localization.Culture = settings.Culture;
            _currentTheme = ThemeService.TryParse(settings.Theme, out var theme) ? theme : AppTheme.Light;
            ThemeService.Apply(_currentTheme);
            ExportCombinedDxf = settings.ExportCombinedDxf;
            AllowMarginRelax = settings.AllowMarginRelax;
            Report = settings.Report ?? new ReportSettings();
            OnPropertyChanged(nameof(ReportFileName));
            PartsGridColumnWidths = settings.PartsGridColumnWidths ?? new();
            MaterialsGridColumnWidths = settings.MaterialsGridColumnWidths ?? new();
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    /// <summary>Сохраняет текущие пути/параметры раскроя/язык/тему — вызывается при закрытии главного окна.</summary>
    public void SaveAppSettings()
    {
        _appSettingsService.Save(new AppSettings
        {
            SearchPath = SearchPath,
            OutputPath = OutputPath,
            SheetMarginMm = SheetMarginMm,
            PartGapMm = PartGapMm,
            MaxSearchPasses = MaxSearchPasses,
            NoImprovementStopCount = NoImprovementStopCount,
            AcceptableMaxUnplaced = AcceptableMaxUnplaced,
            AcceptableMaxSheets = AcceptableMaxSheets,
            Culture = _localization.Culture,
            Theme = _currentTheme.ToString(),
            ExportCombinedDxf = ExportCombinedDxf,
            AllowMarginRelax = AllowMarginRelax,
            Report = Report,
            PartsGridColumnWidths = PartsGridColumnWidths,
            MaterialsGridColumnWidths = MaterialsGridColumnWidths
        });
    }

    public ObservableCollection<PartRow> Rows { get; } = new();
    public ObservableCollection<MaterialRow> Materials { get; } = new();

    /// <summary>Представление Rows, сгруппированное по материалу (разделы) и отсортированное по порядку материалов в проекте.</summary>
    public ListCollectionView RowsView { get; }

    public string SearchPath
    {
        get => _searchPath;
        set
        {
            _searchPath = value ?? string.Empty;
            OnPropertyChanged();
            if (!_loadingSettings) SaveAppSettings();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value ?? string.Empty;
            OnPropertyChanged();
            if (!_loadingSettings) SaveAppSettings();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set { _projectName = value; OnPropertyChanged(); }
    }

    /// <summary>Отступ от края листа, мм.</summary>
    public double SheetMarginMm
    {
        get => _sheetMarginMm;
        set
        {
            _sheetMarginMm = double.IsFinite(value) ? Math.Max(0, value) : 0;
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                RecomputeOversizedForAll();
                UpdateStatusTextForSelection();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Расстояние (зазор) между деталями, мм.</summary>
    public double PartGapMm
    {
        get => _partGapMm;
        set
        {
            _partGapMm = double.IsFinite(value) ? Math.Max(0, value) : 0;
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                RecomputeOversizedForAll();
                UpdateStatusTextForSelection();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Максимальное количество проходов много-проходного движка раскроя.</summary>
    public int MaxSearchPasses
    {
        get => _maxSearchPasses;
        set
        {
            _maxSearchPasses = Math.Max(1, value);
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Количество проходов без улучшения, после которого поиск останавливается (0 — не останавливать).</summary>
    public int NoImprovementStopCount
    {
        get => _noImprovementStopCount;
        set
        {
            _noImprovementStopCount = Math.Max(0, value);
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Допустимое количество неразмещённых деталей, при достижении которого поиск останавливается (-1 — не использовать).</summary>
    public int AcceptableMaxUnplaced
    {
        get => _acceptableMaxUnplaced;
        set
        {
            _acceptableMaxUnplaced = value;
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Допустимое количество листов, при достижении которого поиск останавливается (-1 — не использовать).</summary>
    public int AcceptableMaxSheets
    {
        get => _acceptableMaxSheets;
        set
        {
            _acceptableMaxSheets = value;
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                SaveAppSettings();
            }
        }
    }

    /// <summary>Путь к файлу лога последнего раскроя.</summary>
    public string? LastNestingLogPath
    {
        get => _lastNestingLogPath;
        set { _lastNestingLogPath = value; OnPropertyChanged(); }
    }

    /// <summary>true — команда экспорта DXF создаёт один общий файл; false — отдельный файл на каждый лист.</summary>
    public bool ExportCombinedDxf
    {
        get => _exportCombinedDxf;
        set
        {
            _exportCombinedDxf = value;
            OnPropertyChanged();
            if (!_loadingSettings) SaveAppSettings();
        }
    }

    /// <summary>Настройки генерации отчёта; редактируются в окне настроек отчёта.</summary>
    public ReportSettings Report { get; private set; } = new();

    /// <summary>Имя файла отчёта на главном экране; пустое — имя по умолчанию (дата/время).</summary>
    public string ReportFileName
    {
        get => Report.FileName;
        set
        {
            Report = Report with { FileName = value ?? string.Empty };
            OnPropertyChanged();
            if (!_loadingSettings) SaveAppSettings();
        }
    }

    /// <summary>Разрешить детали, не влезающей с отступом, размещаться у края листа.</summary>
    public bool AllowMarginRelax
    {
        get => _allowMarginRelax;
        set
        {
            _allowMarginRelax = value;
            OnPropertyChanged();
            if (!_loadingSettings)
            {
                InvalidateNestingResult();
                RecomputeOversizedForAll();
                SaveAppSettings();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string QualitySummary
    {
        get => _qualitySummary;
        private set { _qualitySummary = value; OnPropertyChanged(); }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); RecomputeCanNest(); CommandManager.InvalidateRequerySuggested(); }
    }

    public PartRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            _selectedRow = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
            UpdateStatusTextForSelection();
        }
    }

    /// <summary>
    /// Материал, "активный" для вставки (клик по чипу материала или по заголовку раздела в таблице).
    /// Новые строки из Paste/Import Excel автоматически получают этот MaterialId.
    /// </summary>
    public string? ActivePasteMaterialId
    {
        get => _activePasteMaterialId;
        set { _activePasteMaterialId = value; OnPropertyChanged(); }
    }

    public int TotalCount => Rows.Count;
    public int FoundCount => Rows.Count(r => r.Status == PartStatus.Found);
    public int NotFoundCount => Rows.Count(r => r.Status == PartStatus.NotFound);
    public int AmbiguousCount => Rows.Count(r => r.Status == PartStatus.Ambiguous);

    public ICommand BrowseSearchPathCommand { get; }
    public ICommand BrowseOutputPathCommand { get; }
    public ICommand AddRowCommand { get; }
    public ICommand DeleteSelectedRowsCommand { get; }
    public ICommand DeleteRowCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand FindFilesCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand ImportExcelFileCommand { get; }
    public ICommand OpenMaterialsCommand { get; }
    public ICommand OpenReportSettingsCommand { get; }
    public ICommand NestCommand { get; }
    public ICommand ExportDxfCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SelectFileManuallyCommand { get; }
    public ICommand SelectAmbiguousFileCommand { get; }
    public ICommand CopyDxfPathCommand { get; }
    public ICommand ResolveStatusCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand SetActiveMaterialCommand { get; }
    public ICommand DeleteMaterialCommand { get; }
    public ICommand AddMaterialCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand SetThemeCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand OpenDxfFileCommand { get; }

    public IEnumerable<SheetMaterial> AvailableLibraryMaterials
        => _materialsLibraryService.Load()
            .Where(m => Materials.All(pm => pm.Id != m.Id))
            .OrderBy(m => m.Name);

    /// <summary>Кеш позиции материала в проектном списке — избавляет от линейного поиска на каждое
    /// сравнение при сортировке таблицы (docs/Audit_Performance.md, issue P5). Сбрасывается при любом
    /// изменении состава/порядка Materials (см. подписку на Materials.CollectionChanged в конструкторе).</summary>
    private Dictionary<string, int>? _materialOrderCache;

    /// <summary>Позиция материала в проектном списке — определяет порядок разделов в таблице.</summary>
    public int GetMaterialOrder(string materialId)
    {
        if (_materialOrderCache == null)
        {
            _materialOrderCache = new Dictionary<string, int>();
            for (int i = 0; i < Materials.Count; i++)
                _materialOrderCache[Materials[i].Id] = i;
        }

        return _materialOrderCache.TryGetValue(materialId, out var index) ? index : int.MaxValue;
    }

    /// <summary>
    /// Делает материал "активным" для вставки. Принимает либо Id материала (клик по чипу),
    /// либо его отображаемое имя (клик по заголовку раздела в таблице, где Name группы уже сконвертирован в имя).
    /// </summary>
    private void SetActiveMaterial(string? idOrName)
    {
        if (idOrName == null)
        {
            ActivePasteMaterialId = null;
            return;
        }

        var match = Materials.FirstOrDefault(m => m.Id == idOrName) ?? Materials.FirstOrDefault(m => m.Name == idOrName);
        ActivePasteMaterialId = match?.Id ?? idOrName;
    }

    private void DeleteMaterial(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
            return;

        var material = Materials.FirstOrDefault(m => m.Id == materialId);
        if (material == null)
            return;

        var result = MessageBox.Show(
            string.Format(_localization.ConfirmDeleteMaterialMessage, material.Name),
            _localization.ConfirmDeleteMaterialTitle,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK)
            return;

        PushUndoSnapshot();
        Materials.Remove(material);

        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (Rows[i].MaterialId == materialId)
                Rows.RemoveAt(i);
        }

        if (ActivePasteMaterialId == materialId)
            ActivePasteMaterialId = Materials.FirstOrDefault()?.Id;

        OnPropertyChanged(nameof(AvailableLibraryMaterials));
    }

    private void AddMaterial(object? parameter)
    {
        if (parameter is not SheetMaterial sheetMaterial)
            return;

        if (Materials.Any(m => m.Id == sheetMaterial.Id))
            return;

        PushUndoSnapshot();
        Materials.Add(MaterialRow.FromMaterial(sheetMaterial));
        ActivePasteMaterialId ??= sheetMaterial.Id;
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
    }

    /// <summary>Переносит строку в раздел другого материала (используется при Drag&amp;Drop строки между разделами).</summary>
    public void MoveRowToMaterial(PartRow row, string materialId)
    {
        if (row.MaterialId == materialId) return;
        PushUndoSnapshot();
        row.MaterialId = materialId;
        RecomputeSectionNumbers();
        OnRowsChanged();
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (PartRow row in e.OldItems)
                row.PropertyChanged -= OnRowPropertyChanged;

        if (e.NewItems != null)
            foreach (PartRow row in e.NewItems)
                row.PropertyChanged += OnRowPropertyChanged;

        if (e.Action == NotifyCollectionChangedAction.Reset)
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
                row.PropertyChanged += OnRowPropertyChanged;
            }

        if (_suspendRecompute) return;

        if (e.NewItems != null)
            foreach (PartRow row in e.NewItems)
                RecomputeOversized(row);

        RecomputeSectionNumbers();
        RecomputeDuplicateNumbers();
        OnRowsChanged();
    }

    private void OnMaterialsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (MaterialRow material in e.OldItems)
                material.PropertyChanged -= OnMaterialPropertyChanged;

        if (e.NewItems != null)
            foreach (MaterialRow material in e.NewItems)
                material.PropertyChanged += OnMaterialPropertyChanged;

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var material in Materials)
                material.PropertyChanged -= OnMaterialPropertyChanged;
            foreach (var material in Materials)
                material.PropertyChanged += OnMaterialPropertyChanged;
        }

        _materialOrderCache = null; // состав/порядок материалов изменился — кеш порядка невалиден (RF-020)
        InvalidateNestingResult();
        RecomputeSectionNumbers();
        RecomputeCanNest();
        RecomputeOversizedForAll();
        RowsView.Refresh();
        CommandManager.InvalidateRequerySuggested();
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
    }

    private void OnMaterialPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateNestingResult();
        RecomputeOversizedForAll();
    }

    /// <summary>
    /// Добавляет несколько строк одним пересчётом разделов/дублей вместо одного пересчёта на каждую строку
    /// (иначе Paste/Import большого списка вызывают RowsView.Refresh() десятки раз подряд, что визуально
    /// "ломает" таблицу — заголовки/содержимое на время рассинхронизируются, пока не произойдёт следующий рендер).
    /// </summary>
    private void AddRowsBatch(IEnumerable<PartRow> rows)
    {
        _suspendRecompute = true;
        try
        {
            foreach (var row in rows)
                Rows.Add(row);
        }
        finally
        {
            _suspendRecompute = false;
        }

        RecomputeSectionNumbers();
        RecomputeDuplicateNumbers();
        OnRowsChanged();
    }

    /// <summary>
    /// Реагирует на изменения свойств строки, сделанные напрямую через биндинги ячеек таблицы
    /// (ComboBox материала, textbox номера) — раньше пересчёт разделов/дублей срабатывал только
    /// при Drag&amp;Drop или структурных изменениях Rows, из-за чего таблица не обновлялась "на лету".
    /// </summary>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var row = sender as PartRow;
        if (e.PropertyName is nameof(PartRow.IsEnabled) or nameof(PartRow.Quantity) or
            nameof(PartRow.Number) or nameof(PartRow.Title) or nameof(PartRow.MaterialId) or
            nameof(PartRow.DxfPath) or nameof(PartRow.Status) or nameof(PartRow.IsManuallyResolved) or
            nameof(PartRow.GrainMode) or nameof(PartRow.CanRotate) or nameof(PartRow.Bounds))
            InvalidateNestingResult();

        switch (e.PropertyName)
        {
            case nameof(PartRow.MaterialId):
                RecomputeSectionNumbers();
                RecomputeCanNest();
                if (row != null)
                    RecomputeOversized(row);
                break;
            case nameof(PartRow.GrainMode):
            case nameof(PartRow.CanRotate):
            case nameof(PartRow.Bounds):
                if (row != null)
                    RecomputeOversized(row);
                break;
            case nameof(PartRow.Number):
                RecomputeDuplicateNumbers();
                break;
        }

        if (row == _selectedRow)
            UpdateStatusTextForSelection();
    }

    private void OnRowsChanged()
    {
        InvalidateNestingResult();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FoundCount));
        OnPropertyChanged(nameof(NotFoundCount));
        OnPropertyChanged(nameof(AmbiguousCount));
        RecomputeCanNest();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Кеш результата условия для NestCommand — раньше CanNest() делал O(n)-проход по Rows на каждый
    /// CommandManager.RequerySuggested (который WPF поднимает на почти любое UI-событие, не только
    /// связанное с таблицей). Пересчитывается явно там, где действительно меняются входные данные
    /// (docs/Audit_Performance.md, issue P6).
    /// </summary>
    private bool _canNestCache;
    private int _inputVersion;

    private void RecomputeCanNest()
    {
        _canNestCache = !IsBusy && Rows.Count > 0 && Materials.Count > 0 &&
                        Rows.Any(r => !string.IsNullOrWhiteSpace(r.MaterialId));
    }

    private void InvalidateNestingResult()
    {
        // Любое изменение входных данных повышает версию снимка: результат,
        // вычисленный по старому снимку, не должен публиковаться (race fix).
        _inputVersion++;
        if (!_nestingResultManager.HasResult)
            return;

        _nestingResultManager.Clear();
    }

    private void RecomputeOversized(PartRow row)
    {
        var material = Materials.FirstOrDefault(m => m.Id == row.MaterialId)?.ToMaterial();
        if (material is null ||
            row.Bounds.Width <= 0 || row.Bounds.Height <= 0)
        {
            row.OversizedEvaluation = null;
            return;
        }

        var options = CreateNestingOptions();
        row.OversizedEvaluation = SheetFitEvaluator.Evaluate(row.ToPart(), material, options);
    }

    private void RecomputeOversizedForAll()
    {
        foreach (var row in Rows)
            RecomputeOversized(row);
    }

    private string? GetOversizedExplanation(PartRow row)
    {
        var evaluation = row.OversizedEvaluation;
        if (evaluation is null || !evaluation.IsOversized)
            return null;

        var key = evaluation.GrainBlocksRotation
            ? "StatusOversizedGrainBlockedFormat"
            : "StatusOversizedFormat";

        return string.Format(_localization[key],
            evaluation.PartWidth,
            evaluation.PartHeight,
            evaluation.UsableSheetWidth,
            evaluation.UsableSheetHeight);
    }

    private void UpdateStatusTextForSelection()
    {
        var explanation = _selectedRow is null ? null : GetOversizedExplanation(_selectedRow);
        if (!string.IsNullOrWhiteSpace(explanation))
        {
            _oversizedStatusText = explanation;
            StatusText = explanation;
            return;
        }

        if (_oversizedStatusText != null && StatusText == _oversizedStatusText)
            StatusText = string.Empty;
        _oversizedStatusText = null;
    }

    private void UpdateQualitySummary(NestingResult result)
    {
        var sheets = result.Sheets;
        var totalCut = sheets.Sum(s => s.Parts.Sum(p => p.Source is null
            ? 2.0 * (p.Bounds.Width + p.Bounds.Height)
            : p.Source.TruePerimeter));
        var residual = sheets.Sum(s => s.Pockets.Count > 0
            ? s.Pockets.Sum(p => p.Area)
            : s.FreeRectangles.Sum(r => r.Area));
        var pockets = sheets.Sum(s => s.Pockets.Count);
        var score = NestingQualityMetrics.Compute(result);

        QualitySummary = $"""
            Utilization: {score.TrueUtilizationPercent:F2}%
            Cut length: {totalCut:F0} mm
            Residual area: {residual:F0} mm²
            Pockets / holes: {pockets}
            Unplaced: {result.Unplaced.Count}
            """;
    }

    private void RecomputeSectionNumbers()
    {
        var counters = new Dictionary<string, int>();
        foreach (var row in Rows.OrderBy(r => GetMaterialOrder(r.MaterialId)))
        {
            var key = row.MaterialId ?? string.Empty;
            counters.TryGetValue(key, out var count);
            count++;
            counters[key] = count;
            row.RowNumberInSection = count;
        }
        // RowsView.Refresh() больше не требуется здесь: IsLiveSorting/IsLiveGrouping (см. конструктор)
        // сами пересортируют/перегруппируют строки при изменении RowNumberInSection/MaterialId,
        // без полного Reset контейнеров группы (issue C2, docs/Code_Audit_UI.md).
    }

    private void RecomputeDuplicateNumbers()
    {
        var duplicateNumbers = Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Number))
            .GroupBy(r => r.Number.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Rows)
            row.IsDuplicateNumber = !string.IsNullOrWhiteSpace(row.Number) && duplicateNumbers.Contains(row.Number.Trim());
    }

    private void PushUndoSnapshot()
    {
        while (_undoStack.Count >= MaxUndoDepth)
            _undoStack.RemoveFirst();
        _undoStack.AddLast(new Snapshot(Rows.Select(r => r.Clone()).ToList(), Materials.ToList()));
        ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var snapshot = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        _suspendRecompute = true;
        try
        {
            Materials.Clear();
            foreach (var material in snapshot.Materials)
                Materials.Add(material);

            Rows.Clear();
        }
        finally
        {
            _suspendRecompute = false;
        }
        AddRowsBatch(snapshot.Rows);
        ActivePasteMaterialId = Materials.FirstOrDefault()?.Id;
        OnPropertyChanged(nameof(AvailableLibraryMaterials));
        ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
    }

    private void BrowseSearchPath()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _localization["LabelSearchPath"],
            SelectedPath = FolderBrowserDialogHelper.GetInitialFolder(SearchPath)
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SearchPath = dlg.SelectedPath;
    }

    private void BrowseOutputPath()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _localization["LabelOutputPath"],
            SelectedPath = FolderBrowserDialogHelper.GetInitialFolder(OutputPath)
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            OutputPath = dlg.SelectedPath;
    }

    private void AddRow()
    {
        PushUndoSnapshot();
        var row = new PartRow { RowNumber = Rows.Count + 1 };
        Rows.Add(row);
        SelectedRow = row;
    }

    private void DeleteSelectedRows()
    {
        DeleteRow(SelectedRow);
    }

    private void DeleteRow(PartRow? row)
    {
        if (row == null || !Rows.Contains(row)) return;
        PushUndoSnapshot();
        Rows.Remove(row);
        if (SelectedRow == row)
            SelectedRow = Rows.FirstOrDefault();
    }

    private void ClearAll()
    {
        if (Rows.Count == 0) return;

        var result = MessageBox.Show(
            _localization.ClearRowsConfirmation,
            _localization.ClearRowsTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        PushUndoSnapshot();
        Rows.Clear();
        SelectedRow = null;
        _nestingResultManager.Clear();
    }

    private void SelectFileManually(PartRow? row)
    {
        if (row == null) return;
        var dlg = new OpenFileDialog { Filter = "DXF files (*.dxf)|*.dxf" };
        if (dlg.ShowDialog() != true) return;
        PushUndoSnapshot();
        AssignDxfToRow(row, dlg.FileName);
    }

    private void ApplyDxfToRowCore(PartRow row, string path)
    {
        row.DxfPath = path;
        UpdateRowBounds(row, out var contourRead);
        row.IsManuallyResolved = contourRead;
        row.Status = contourRead ? PartStatus.ManuallySelected : PartStatus.NotFound;
        RecomputeOversized(row);
    }

    public void AssignDxfToRow(PartRow row, string path)
    {
        ApplyDxfToRowCore(row, path);
        OnRowsChanged();
    }

    public void AssignDroppedDxfToRow(PartRow row, string path)
    {
        if (!string.IsNullOrWhiteSpace(row.DxfPath) || !DroppedDxfMapper.IsDxfFile(path)) return;
        PushUndoSnapshot();
        AssignDxfToRow(row, path);
    }

    public void AddDroppedDxfFiles(IReadOnlyList<string> filePaths)
    {
        var dxfPaths = DroppedDxfMapper.FilterDxfFiles(filePaths);
        if (dxfPaths.Count == 0) return;

        var newRows = new List<PartRow>();
        foreach (var path in dxfPaths)
        {
            var (number, title, quantity) = DroppedDxfMapper.Map(path);
            var row = new PartRow
            {
                RowNumber = Rows.Count + newRows.Count + 1,
                Quantity = quantity,
                Number = number,
                Title = title,
                MaterialId = ActivePasteMaterialId ?? string.Empty
            };
            ApplyDxfToRowCore(row, path);
            newRows.Add(row);
        }

        PushUndoSnapshot();
        AddRowsBatch(newRows);
    }

    private void CopyDxfPath(PartRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.DxfPath)) return;
        Clipboard.SetText(row.DxfPath);
    }

    private void OpenDxfFile(PartRow? row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.DxfPath) || !File.Exists(row.DxfPath))
            return;
        Process.Start(new ProcessStartInfo(row.DxfPath) { UseShellExecute = true });
    }

    private void SelectAmbiguousFile(PartRow? row)
    {
        if (row == null || row.Status != PartStatus.Ambiguous) return;
        var candidates = _fileResolveService.GetCandidates(row.DisplayName, SearchPath, OutputPath);
        var window = new NStCut.App.Views.CandidateSelectWindow(candidates)
        {
            Owner = Application.Current.MainWindow
        };
        if (window.ShowDialog() != true) return;
        if (string.IsNullOrWhiteSpace(window.SelectedPath)) return;
        PushUndoSnapshot();
        AssignDxfToRow(row, window.SelectedPath);
    }

    /// <summary>
    /// Обрабатывает клик по ячейке статуса: Ambiguous — открывает список найденных кандидатов,
    /// NotFound/остальные — открывает диалог выбора файла напрямую (без промежуточных кликов по меню).
    /// </summary>
    private void ResolveStatus(PartRow? row)
    {
        if (row == null) return;
        if (row.Status == PartStatus.Ambiguous)
            SelectAmbiguousFile(row);
        else
            SelectFileManually(row);
    }

    private string FormatGeometryDiagnostics(DxfContour contour) =>
        FormatGeometryDiagnostics(contour.Quality, contour.Diagnostics.Select(d => d.Code));

    private string FormatGeometryDiagnostics(
        DxfGeometryQuality quality,
        IEnumerable<DxfGeometryDiagnosticCode> diagnostics)
    {
        var codes = diagnostics.Distinct().ToList();
        if (codes.Count == 0)
            return FormatQualitySummary(quality);
        return string.Join(Environment.NewLine, codes.Select(MapDiagnosticCode));
    }

    private string MapDiagnosticCode(DxfGeometryDiagnosticCode code) => code switch
    {
        DxfGeometryDiagnosticCode.UnsupportedEntity => _localization["GeometryDiagnosticUnsupportedEntity"],
        DxfGeometryDiagnosticCode.ClosedStateUnavailable => _localization["GeometryDiagnosticClosedStateUnavailable"],
        DxfGeometryDiagnosticCode.BulgeUnavailable => _localization["GeometryDiagnosticBulgeUnavailable"],
        DxfGeometryDiagnosticCode.InvalidClosedLoop => _localization["GeometryDiagnosticInvalidClosedLoop"],
        DxfGeometryDiagnosticCode.NoClosedContour => _localization["GeometryDiagnosticNoClosedContour"],
        DxfGeometryDiagnosticCode.NoUsableBounds => _localization["GeometryDiagnosticNoUsableBounds"],
        DxfGeometryDiagnosticCode.AdditionalContoursIgnored => _localization["GeometryDiagnosticAdditionalContoursIgnored"],
        DxfGeometryDiagnosticCode.DuplicateContourIgnored => _localization["GeometryDiagnosticDuplicateContourIgnored"],
        DxfGeometryDiagnosticCode.SelfIntersectingContour => _localization["GeometryDiagnosticSelfIntersecting"],
        _ => code.ToString()
    };

    private string FormatQualitySummary(DxfGeometryQuality quality) => quality switch
    {
        DxfGeometryQuality.Partial => _localization["GeometryQualityPartial"],
        DxfGeometryQuality.BoundsOnly => _localization["GeometryQualityBoundsOnly"],
        DxfGeometryQuality.Invalid => _localization["GeometryQualityInvalid"],
        _ => string.Empty
    };

    private bool UpdateRowBounds(PartRow row, out bool contourRead)
    {
        contourRead = false;
        if (string.IsNullOrWhiteSpace(row.DxfPath) || !File.Exists(row.DxfPath))
        {
            row.Bounds = default;
            row.GeometryQuality = DxfGeometryQuality.Invalid;
            row.GeometryDiagnostics = new List<DxfGeometryDiagnosticCode>();
            row.GeometryDiagnosticText = _localization["GeometryQualityInvalid"];
            return false;
        }
        try
        {
            var contour = _dxfGeometry.ReadContour(row.DxfPath);
            contourRead = true;
            row.GeometryQuality = contour.Quality;
            row.GeometryDiagnostics = contour.Diagnostics.Select(d => d.Code).ToList();
            row.GeometryDiagnosticText = FormatGeometryDiagnostics(contour);
            var hasValidBounds = contour.HasUsableBounds && contour.Quality != DxfGeometryQuality.Invalid;
            row.Bounds = hasValidBounds ? contour.Bounds : default;
            return hasValidBounds;
        }
        catch (Exception)
        {
            row.Bounds = default;
            row.GeometryQuality = DxfGeometryQuality.Invalid;
            row.GeometryDiagnostics = new List<DxfGeometryDiagnosticCode>();
            row.GeometryDiagnosticText = _localization["GeometryQualityInvalid"];
            return false;
        }
    }

    private void FindFiles()
    {
        try
        {
            // Один проход по диску на весь список деталей вместо повторного сканирования папки
            // на каждую строку (docs/Audit_Performance.md, issue P1).
            var allDxfFiles = _fileResolveService.EnumerateDxfFiles(SearchPath, OutputPath);
            var ambiguousDetails = new List<string>();
            foreach (var row in Rows.Where(r => !r.IsManuallyResolved))
            {
                var result = _fileResolveService.Resolve(row.DisplayName, allDxfFiles);
                row.Status = result.Status;
                row.DxfPath = result.Path;
                UpdateRowBounds(row, out _);
                RecomputeOversized(row);
                if (result.Status == PartStatus.Ambiguous)
                    ambiguousDetails.Add($"{row.DisplayName} ({result.Candidates.Count})");
            }
            OnRowsChanged();
            StatusText = ambiguousDetails.Count == 0
                ? $"Found: {FoundCount}, not found: {NotFoundCount}"
                : $"Ambiguous: {string.Join(", ", ambiguousDetails)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleFindFiles, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        PushUndoSnapshot();
        var text = Clipboard.GetText();
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var newRows = new List<PartRow>();
        foreach (var line in lines)
        {
            var cells = line.Split('\t');
            var (number, title, qty) = PastedRowParser.Parse(cells);
            if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(title)) continue;
            newRows.Add(new PartRow
            {
                RowNumber = Rows.Count + newRows.Count + 1,
                Quantity = qty,
                Number = number,
                Title = title,
                MaterialId = ActivePasteMaterialId ?? string.Empty
            });
        }
        AddRowsBatch(newRows);
    }

    private void ImportExcelFile()
    {
        var dlg = new OpenFileDialog { Filter = "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            PushUndoSnapshot();
            var newRows = new List<PartRow>();
            using (var stream = File.Open(dlg.FileName, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                // Read first sheet, skip header row
                bool first = true;
                while (reader.Read())
                {
                    if (first) { first = false; continue; }
                    var cells = new[]
                    {
                        reader.GetValue(0)?.ToString() ?? string.Empty,
                        reader.FieldCount > 1 ? reader.GetValue(1)?.ToString() ?? string.Empty : string.Empty,
                        reader.FieldCount > 2 ? reader.GetValue(2)?.ToString() ?? string.Empty : string.Empty
                    };
                    var (number, title, qty) = PastedRowParser.Parse(cells);
                    if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(title)) continue;
                    newRows.Add(new PartRow
                    {
                        RowNumber = Rows.Count + newRows.Count + 1,
                        Quantity = qty,
                        Number = number,
                        Title = title,
                        MaterialId = ActivePasteMaterialId ?? string.Empty
                    });
                }
            }
            AddRowsBatch(newRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleImport, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenMaterialsWindow()
    {
        var vm = new MaterialsViewModel(Materials, columnWidths: MaterialsGridColumnWidths);
        var window = new NStCut.App.Views.MaterialsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            Materials.Clear();
            foreach (var m in vm.ProjectMaterials)
                Materials.Add(m);

            if (ActivePasteMaterialId == null || Materials.All(m => m.Id != ActivePasteMaterialId))
                ActivePasteMaterialId = Materials.FirstOrDefault()?.Id;
        }
    }

    internal ReportLabels BuildReportLabels() => new(
        _localization.ColumnDirection,
        _localization.NoGrain,
        _localization.GrainHorizontal,
        _localization.GrainVertical,
        _localization.ReportGenerated,
        _localization.ReportMaterials,
        _localization.ReportMaterial,
        _localization.ReportSize,
        _localization.ReportSheets,
        _localization.ReportAvgUtilization,
        _localization.ReportParts,
        _localization.ReportPart,
        _localization.ReportQty,
        _localization.ReportPerimeter,
        _localization.ReportUtilization,
        _localization.ReportWasteArea,
        _localization.ReportNumber,
        _localization.ReportSizeMm,
        _localization.ReportAreaMm2,
        _localization.ReportAreaM2,
        _localization.ReportSheet);

    private void OpenReportSettingsWindow()
    {
        var vm = new ReportSettingsViewModel(Report, ExportCombinedDxf);
        var window = new NStCut.App.Views.ReportSettingsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            Report = vm.Settings;
            ExportCombinedDxf = vm.ExportCombinedDxf;
            OnPropertyChanged(nameof(ReportFileName));
            SaveAppSettings();
        }
    }

    private bool CanNest() => _canNestCache;

    // async void — стандартный, принятый в WPF паттерн для обработчиков команд (RelayCommand не
    // поддерживает Func<Task>). Чтение геометрии и сам раскрой выполняются в Task.Run вне UI-потока,
    // чтобы интерфейс не "замирал" на время обработки большого числа деталей
    // (см. docs/Audit_Performance.md, issue P2). После await продолжение выполняется на UI-потоке
    // (SynchronizationContext WPF), поэтому обращения к StatusText/ProgressValue/созданию окна ниже безопасны.
    internal sealed record NestingInput(int RowIndex, Part Part);
    internal sealed record RowNestingUpdate(
        int RowIndex,
        NStCut.Core.Geometry.Rect Bounds,
        double Area,
        double Perimeter,
        PartStatus Status,
        DxfGeometryQuality GeometryQuality,
        IReadOnlyList<DxfGeometryDiagnosticCode> GeometryDiagnostics);
    internal sealed record NestingWorkResult(NestingResult Result, IReadOnlyList<RowNestingUpdate> Updates);

    internal NestingWorkResult PerformNesting(
        NestingOptions options,
        IReadOnlyDictionary<string, SheetMaterial> materialDict,
        IReadOnlyList<NestingInput> inputs,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var validParts = new List<Part>();
        var invalidParts = new List<Part>();
        var oversizedParts = new List<Part>();
        var updates = new List<RowNestingUpdate>();

        foreach (var input in inputs)
        {
            var part = input.Part;
            if (string.IsNullOrWhiteSpace(part.DxfPath) || !File.Exists(part.DxfPath))
            {
                AddInvalidPart(input, invalidParts, updates, null);
                continue;
            }

            try
            {
                var contour = _dxfGeometry.ReadContour(part.DxfPath);
                part.Bounds = contour.Bounds;
                part.Contour = contour.Polygon;
                part.Holes = contour.Holes;
                part.Area = contour.TrueArea;
                part.Perimeter = contour.TruePerimeter;

                if (contour.Quality == DxfGeometryQuality.Invalid || !contour.HasUsableBounds)
                {
                    AddInvalidPart(input, invalidParts, updates, contour);
                    continue;
                }

                if (materialDict.TryGetValue(part.MaterialId, out var material))
                {
                    var fit = SheetFitEvaluator.Evaluate(part, material, options);
                    if (fit.IsOversized)
                    {
                        updates.Add(new RowNestingUpdate(
                            input.RowIndex,
                            contour.Bounds,
                            part.Area,
                            part.Perimeter,
                            part.Status,
                            contour.Quality,
                            contour.Diagnostics.Select(d => d.Code).ToList()));
                        for (var copyIndex = 0; copyIndex < Math.Max(0, part.Quantity); copyIndex++)
                        {
                            var copy = part.Clone();
                            copy.Quantity = 1;
                            oversizedParts.Add(copy);
                        }
                        continue;
                    }
                }

                updates.Add(new RowNestingUpdate(
                    input.RowIndex,
                    contour.Bounds,
                    part.Area,
                    part.Perimeter,
                    part.Status,
                    contour.Quality,
                    contour.Diagnostics.Select(d => d.Code).ToList()));
                validParts.Add(part);
            }
            catch (Exception)
            {
                AddInvalidPart(input, invalidParts, updates, null);
            }
        }

        var materials = validParts.Select(p => p.MaterialId).Distinct()
            .Where(materialDict.ContainsKey)
            .Select(id => materialDict[id])
            .ToList();

        var result = _nestingEngine.Nest(validParts, materials, options, progress, cancellationToken);
        result.Unplaced.AddRange(invalidParts);
        result.Unplaced.AddRange(oversizedParts);
        return new NestingWorkResult(result, updates);
    }

    private static void AddInvalidPart(
        NestingInput input,
        ICollection<Part> invalidParts,
        ICollection<RowNestingUpdate> updates,
        DxfContour? contour)
    {
        var diagnostics = contour?.Diagnostics.Select(d => d.Code).ToList()
            ?? new List<DxfGeometryDiagnosticCode>();
        var status = contour != null ? input.Part.Status : PartStatus.NotFound;
        updates.Add(new RowNestingUpdate(
            input.RowIndex,
            default,
            0.0,
            0.0,
            status,
            DxfGeometryQuality.Invalid,
            diagnostics));
        for (var copyIndex = 0; copyIndex < Math.Max(0, input.Part.Quantity); copyIndex++)
        {
            var copy = input.Part.Clone();
            copy.Quantity = 1;
            copy.Bounds = default;
            copy.Area = 0;
            copy.Perimeter = 0;
            copy.Contour = null;
            copy.Status = status;
            invalidParts.Add(copy);
        }
    }

    internal async Task<NestingResult> NestAsync(IProgress<double>? progress = null)
    {
        using var cts = new CancellationTokenSource();
        _currentCts = cts;
        IsBusy = true;
        try
        {
            var options = CreateNestingOptions();
            options.LogFilePath = BuildNestingLogPath();
            progress ??= new Progress<double>(v => ProgressValue = v);

            RecomputeOversizedForAll();

            // Переснимаем входы и пересчитываем, если они менялись во время расчёта —
            // иначе опубликованный результат не соответствовал бы текущему заданию.
            const int maxAttempts = 3;
            NestingWorkResult? work = null;
            List<PartRow>? rowReferences = null;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var version = _inputVersion;
                var materialDict = Materials
                    .GroupBy(m => m.Id)
                    .ToDictionary(g => g.Key, g => g.First().ToMaterial());
                var rows = Rows.Where(r => r.IsEnabled).ToList();
                var inputs = rows.Select((row, index) => new NestingInput(index, row.ToPart())).ToList();

                var candidate = await Task.Run(
                    () => PerformNesting(options, materialDict, inputs, progress, cts.Token),
                    cts.Token);
                if (version == _inputVersion || attempt == maxAttempts - 1)
                {
                    work = candidate;
                    rowReferences = rows;
                    break;
                }
            }

            foreach (var update in work!.Updates)
            {
                var row = rowReferences![update.RowIndex];
                row.Bounds = update.Bounds;
                row.Area = update.Area;
                row.Perimeter = update.Perimeter;
                row.Status = update.Status;
                row.GeometryQuality = update.GeometryQuality;
                row.GeometryDiagnostics = update.GeometryDiagnostics;
                row.GeometryDiagnosticText = FormatGeometryDiagnostics(
                    update.GeometryQuality,
                    update.GeometryDiagnostics);
            }

            RecomputeOversizedForAll();
            UpdateStatusTextForSelection();

            var result = work.Result;
            _nestingResultManager.Set(result);
            UpdateQualitySummary(result);
            var existingStatus = $"Sheets: {result.Sheets.Count}, unplaced: {result.Unplaced.Count}" +
                                 _localization["StatusHeuristicSuffix"];
            var simplifiedCount = work.Updates
                .Where(update => update.GeometryQuality is DxfGeometryQuality.Partial or DxfGeometryQuality.BoundsOnly)
                .Select(update => update.RowIndex)
                .Distinct()
                .Count();
            StatusText = simplifiedCount > 0
                ? $"{existingStatus} {string.Format(_localization["GeometrySimplifiedCountFormat"], simplifiedCount)}"
                : existingStatus;
            return result;
        }
        finally
        {
            _currentCts = null;
            IsBusy = false;
        }
    }

    private async void Nest()
    {
        try
        {
            var result = await NestAsync();
            var previewOptions = CreateNestingOptions();
            previewOptions.LogFilePath = null;
            var parts = Rows.Where(r => r.IsEnabled).Select(r => r.ToPart()).ToList();
            var materials = Materials.Select(m => m.ToMaterial()).ToList();
            var preview = new NStCut.App.Views.SheetPreviewWindow
            {
                DataContext = new SheetPreviewViewModel(
                    result, _nestingEngine, previewOptions,
                    _dxfExport, _reportService, _exportFileNameService,
                    OutputPath, ProjectName, parts, materials,
                    Report, ExportCombinedDxf),
                Owner = Application.Current.MainWindow
            };
            preview.Show();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleNesting, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // в Task.Run, чтобы не блокировать UI-поток при экспорте большого числа листов/деталей
    // (docs/Audit_Performance.md, issue P2).
    private async void ExportDxf()
    {
        if (_nestingResultManager.Current == null) return;
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusText = _localization["StatusOutputFolderRequired"];
            return;
        }
        var outputPath = OutputPath;
        var partGapMm = PartGapMm;
        var sheetMarginMm = SheetMarginMm;
        var exportCombined = ExportCombinedDxf;
        var sheets = _nestingResultManager.Current.Sheets.ToList();
        using var cts = new CancellationTokenSource();
        _currentCts = cts;
        IsBusy = true;
        var exportedCount = 0;
        string? combinedPath = null;
        try
        {
            Directory.CreateDirectory(outputPath);
            var localization = LocalizationService.Instance;
            var labels = new DxfExportLabels(localization.SheetLabel, localization.NoGrain,
                localization.GrainHorizontal, localization.GrainVertical, localization.MaterialLabel);
            await Task.Run(() =>
            {
                if (exportCombined)
                {
                    combinedPath = _exportFileNameService.GetCombinedDxfExportPath(outputPath);
                    _dxfExport.WriteCombined(combinedPath, sheets, partGapMm, sheetMarginMm, labels, cts.Token);
                }
                else
                {
                    var batchTimestamp = _exportFileNameService.GetBatchTimestamp();
                    var exports = sheets.Select(sheet =>
                        (_exportFileNameService.GetDxfExportPath(outputPath, sheet, batchTimestamp), sheet)).ToList();
                    _dxfExport.WriteSheets(exports, partGapMm, sheetMarginMm, labels, cts.Token);
                    exportedCount = exports.Count;
                }
            }, cts.Token);
            StatusText = exportCombined
                ? $"DXF exported to {combinedPath}"
                : $"DXF exported to {outputPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            // Сообщаем, на каком листе прервался экспорт, а не только общий текст исключения —
            // без этого частичный экспорт (часть файлов уже на диске) остаётся незамеченным
            // пользователю (см. docs/Audit_Reliability.md, issue R5).
            var message = !exportCombined && exportedCount < sheets.Count
                ? $"{ex.Message}\n\nExported {exportedCount} of {sheets.Count} sheet(s) before the error occurred."
                : ex.Message;
            MessageBox.Show(message, _localization.ErrorTitleExport, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _currentCts = null;
            IsBusy = false;
        }
    }

    private async void ExportReport()
    {
        if (_nestingResultManager.Current == null) return;
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusText = _localization["StatusOutputFolderRequired"];
            return;
        }
        using var cts = new CancellationTokenSource();
        _currentCts = cts;
        IsBusy = true;
        try
        {
            Directory.CreateDirectory(OutputPath);

            var parts = Rows.Where(r => r.IsEnabled).Select(r => r.ToPart()).ToList();
            var sheets = _nestingResultManager.Current.Sheets;
            var thumbnails = sheets.Select(s => SheetThumbnailService.Render(s, showPartNumbers: true)).ToList();

            var path = string.IsNullOrWhiteSpace(Report.FileName)
                ? _exportFileNameService.GetReportExportPath(OutputPath)
                : Path.Combine(OutputPath, Formatters.ReportFileNameFormatter.Format(Report, DateTime.Now));
            var materials = Materials.Select(m => m.ToMaterial()).ToList();
            var reportLabels = BuildReportLabels();
            var reportOptions = new ReportContentOptions(
                Report.Title,
                Report.TitleIncludeDate,
                Report.TitleIncludeTime,
                Report.ShowSummary,
                Report.IncludeMaterial,
                Report.ShowPartNames,
                Report.ShowArea,
                Report.AreaUnit == Models.ReportAreaUnit.SquareM);
            await Task.Run(() =>
                _reportService.GenerateReport(path, ProjectName, parts, materials, sheets, thumbnails, reportLabels, reportOptions, cts.Token), cts.Token);
            _nestingResultManager.SetLastReportPath(path);
            StatusText = $"Report saved: {path}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleExport, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _currentCts = null;
            IsBusy = false;
        }
    }

    private void OpenOutputFolder()
    {
        if (!Directory.Exists(OutputPath)) return;
        Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true });
    }

    private void OpenReport()
    {
        if (!_nestingResultManager.HasLastReportPath) return;
        Process.Start(new ProcessStartInfo(_nestingResultManager.LastReportPath!) { UseShellExecute = true });
    }

    private void Cancel()
    {
        _currentCts?.Cancel();
        StatusText = "Cancelling...";
    }

    private void SaveProject()
    {
        var dlg = new SaveFileDialog { Filter = "NStCut project (*.nstcut)|*.nstcut", FileName = "project.nstcut" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var data = new ProjectData
            {
                SearchPath = SearchPath,
                OutputPath = OutputPath,
                Parts = Rows.Select(r => r.ToPart()).ToList(),
                Materials = Materials.Select(m => m.ToMaterial()).ToList()
            };
            _projectService.Save(dlg.FileName, data);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleSave, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenProject()
    {
        var dlg = new OpenFileDialog { Filter = "NStCut project (*.nstcut)|*.nstcut" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            OpenProject(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _localization.ErrorTitleOpen, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    internal void OpenProject(string path)
    {
        if (!_projectService.TryLoad(path, out var data))
        {
            MessageBox.Show(
                _localization["ErrorProjectFileCorrupted"],
                _localization.ErrorTitleOpen,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        PushUndoSnapshot();
        if (!string.IsNullOrWhiteSpace(data.SearchPath)) SearchPath = data.SearchPath;
        if (!string.IsNullOrWhiteSpace(data.OutputPath)) OutputPath = data.OutputPath;
        _suspendRecompute = true;
        try
        {
            Rows.Clear();
        }
        finally
        {
            _suspendRecompute = false;
        }
        Materials.Clear();
        foreach (var m in data.Materials)
            Materials.Add(MaterialRow.FromMaterial(m));
        AddRowsBatch(data.Parts.Select(PartRow.FromPart));
        RecomputeOversizedForAll();
    }

    private void SetLanguage(object? lang)
    {
        if (lang is string s)
            _localization.Culture = s;
        OnPropertyChanged(string.Empty);
    }

    private void SetTheme(object? theme)
    {
        if (theme is not string s || !ThemeService.TryParse(s, out var parsed))
            return;

        _currentTheme = parsed;
        ThemeService.Apply(parsed);
    }

    private void ShowAbout()
    {
        var window = new Views.AboutWindow { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private NestingOptions CreateNestingOptions()
    {
        return new NestingOptions
        {
            PartGapMm = PartGapMm,
            SheetMarginMm = SheetMarginMm,
            AllowMarginRelax = AllowMarginRelax,
            MaxSearchPasses = MaxSearchPasses,
            NoImprovementStopCount = NoImprovementStopCount,
            AcceptableMaxUnplaced = AcceptableMaxUnplaced,
            AcceptableMaxSheets = AcceptableMaxSheets
        };
    }

    private static string BuildSessionLogPath()
    {
        var logsDir = Path.Combine(AppPaths.RootFolder, "nesting_logs");
        Directory.CreateDirectory(logsDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(logsDir, $"nesting_session_{timestamp}.txt");
    }

    private string BuildNestingLogPath()
    {
        LastNestingLogPath = _sessionLogPath;
        return _sessionLogPath;
    }
}
