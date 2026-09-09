using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using NStCut.App.Commands;
using NStCut.App.Services;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.App.ViewModels;

public sealed class SheetPreviewViewModel : INotifyPropertyChanged
{
    private double _zoom = 1.0;
    private SheetPreviewItemViewModel? _selectedSheet;
    private int _selectedIndex;

    public ObservableCollection<SheetPreviewItemViewModel> Sheets { get; } = new();

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            OnPropertyChanged();
        }
    }

    public SheetPreviewItemViewModel? SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            _selectedSheet = value;
            OnPropertyChanged();
            UpdateSelectedIndex();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            OnPropertyChanged();
            UpdateSelectedSheet();
        }
    }

    public ICommand RenestCurrentSheetCommand { get; }
    public ICommand FitToWindowCommand { get; }
    public ICommand ExportDxfCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand OpenDxfCommand { get; }
    public ICommand OpenReportCommand { get; }

    public bool CanRenest => _nestingEngine is not null && _options is not null;

    private readonly NestingResult _originalResult;
    private readonly INestingEngine? _nestingEngine;
    private readonly NestingOptions? _options;
    private readonly IDxfExportService? _dxfExport;
    private readonly IReportService? _reportService;
    private readonly IExportFileNameService? _exportFileNameService;
    private readonly string? _outputPath;
    private readonly string _projectName;
    private readonly IReadOnlyList<Part> _parts;
    private readonly IReadOnlyList<SheetMaterial> _materials;
    private string? _lastReportPath;

    public SheetPreviewViewModel(NestingResult result, INestingEngine? nestingEngine = null, NestingOptions? options = null,
        IDxfExportService? dxfExport = null, IReportService? reportService = null, IExportFileNameService? exportFileNameService = null,
        string? outputPath = null, string? projectName = null, IReadOnlyList<Part>? parts = null, IReadOnlyList<SheetMaterial>? materials = null)
    {
        _originalResult = result;
        _nestingEngine = nestingEngine;
        _options = options;
        _dxfExport = dxfExport;
        _reportService = reportService;
        _exportFileNameService = exportFileNameService;
        _outputPath = outputPath;
        _projectName = projectName ?? "N.StCut Project";
        _parts = parts ?? CollectParts(result);
        _materials = materials ?? result.Sheets.Select(s => s.Material).Distinct().ToList();

        for (var i = 0; i < result.Sheets.Count; i++)
            Sheets.Add(new SheetPreviewItemViewModel(result.Sheets[i], _options));

        if (Sheets.Count > 0)
        {
            SelectedSheet = Sheets[0];
            _selectedIndex = 0;
        }

        RenestCurrentSheetCommand = new RelayCommand(_ => RenestCurrentSheet());
        FitToWindowCommand = new RelayCommand(_ => Zoom = 1.0);
        ExportDxfCommand = new RelayCommand(
            _ => ExportDxf(),
            _ => _dxfExport is not null && _exportFileNameService is not null && !string.IsNullOrWhiteSpace(_outputPath));
        ExportReportCommand = new RelayCommand(
            _ => ExportReport(),
            _ => _reportService is not null && _exportFileNameService is not null && !string.IsNullOrWhiteSpace(_outputPath));
        OpenDxfCommand = new RelayCommand(
            _ => OpenDxfFolder(),
            _ => !string.IsNullOrWhiteSpace(_outputPath) && Directory.Exists(_outputPath));
        OpenReportCommand = new RelayCommand(
            _ => OpenReport(),
            _ => !string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath));
    }

    private static IReadOnlyList<Part> CollectParts(NestingResult result)
    {
        var placed = result.Sheets.SelectMany(s => s.Parts).Select(p => p.Source);
        var unplaced = result.Unplaced;
        return placed.Concat(unplaced)
            .DistinctBy(p => (p.Number, p.MaterialId))
            .ToList();
    }

    private void RenestCurrentSheet()
    {
        if (SelectedSheet is null || _nestingEngine is null || _options is null)
            return;

        var originalIndex = SelectedIndex;
        if (originalIndex < 0 || originalIndex >= Sheets.Count)
            return;

        var selected = SelectedSheet.Sheet;

        // Group placed parts by their source Part and build a Part list with the same overall quantity.
        var parts = selected.Parts
            .GroupBy(p => p.Source)
            .Select(g =>
            {
                var clone = g.Key.Clone();
                clone.Quantity = g.Count();
                return clone;
            })
            .ToList();

        var renestOptions = new NestingOptions
        {
            PartGapMm = _options.PartGapMm,
            SheetMarginMm = _options.SheetMarginMm,
            Resolution = _options.Resolution,
            MaxSearchPasses = Math.Min(_options.MaxSearchPasses, 16),
            NoImprovementStopCount = Math.Min(_options.NoImprovementStopCount, 4),
            AcceptableMaxUnplaced = _options.AcceptableMaxUnplaced,
            AcceptableMaxSheets = _options.AcceptableMaxSheets,
            LogFilePath = null
        };

        var result = _nestingEngine.Nest(parts, new[] { selected.Material }, renestOptions);

        if (result.Sheets.Count == 1 && result.Unplaced.Count == 0)
        {
            result.Sheets[0].Index = selected.Index;
            var replacement = new SheetPreviewItemViewModel(result.Sheets[0], _options);
            Sheets[originalIndex] = replacement;
            SelectedSheet = replacement;
            OnPropertyChanged(nameof(Sheets));

            if (originalIndex < _originalResult.Sheets.Count)
                _originalResult.Sheets[originalIndex] = result.Sheets[0];
        }
    }

    private async void ExportDxf()
    {
        if (_dxfExport is null || _exportFileNameService is null || string.IsNullOrWhiteSpace(_outputPath))
            return;

        var outputPath = _outputPath;
        var partGapMm = _options?.PartGapMm ?? DefaultNestingOptions.Value.PartGapMm;
        var sheetMarginMm = _options?.SheetMarginMm ?? DefaultNestingOptions.Value.SheetMarginMm;
        var sheets = _originalResult.Sheets.ToList();
        var exportedCount = 0;

        try
        {
            Directory.CreateDirectory(outputPath);
            var localization = LocalizationService.Instance;
            var labels = new DxfExportLabels(
                localization.SheetLabel,
                localization.NoGrain,
                localization.GrainHorizontal,
                localization.GrainVertical,
                localization.MaterialLabel);

            await Task.Run(() =>
            {
                var batchTimestamp = _exportFileNameService.GetBatchTimestamp();
                var exports = sheets.Select(sheet =>
                    (_exportFileNameService.GetDxfExportPath(outputPath, sheet, batchTimestamp), sheet)).ToList();
                _dxfExport.WriteSheets(exports, partGapMm, sheetMarginMm, labels);
                exportedCount = exports.Count;
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, LocalizationService.Instance.ErrorTitleExport, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async void ExportReport()
    {
        if (_reportService is null || _exportFileNameService is null || string.IsNullOrWhiteSpace(_outputPath))
            return;

        try
        {
            Directory.CreateDirectory(_outputPath);

            var sheets = _originalResult.Sheets;
            var thumbnails = sheets.Select(s => SheetThumbnailService.Render(s)).ToList();
            var path = _exportFileNameService.GetReportExportPath(_outputPath);
            var localization = LocalizationService.Instance;
            var reportLabels = new ReportLabels(
                localization.ColumnDirection,
                localization.NoGrain,
                localization.GrainHorizontal,
                localization.GrainVertical);

            await Task.Run(() =>
                _reportService.GenerateReport(path, _projectName, _parts, _materials, sheets, thumbnails, reportLabels));
            _lastReportPath = path;
            ((RelayCommand)OpenReportCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, LocalizationService.Instance.ErrorTitleExport, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenDxfFolder()
    {
        if (string.IsNullOrWhiteSpace(_outputPath) || !Directory.Exists(_outputPath))
            return;

        Process.Start(new ProcessStartInfo(_outputPath) { UseShellExecute = true });
    }

    private void OpenReport()
    {
        if (string.IsNullOrWhiteSpace(_lastReportPath) || !File.Exists(_lastReportPath))
            return;

        Process.Start(new ProcessStartInfo(_lastReportPath) { UseShellExecute = true });
    }

    private void UpdateSelectedIndex()
    {
        if (_selectedSheet is null)
        {
            _selectedIndex = -1;
            OnPropertyChanged(nameof(SelectedIndex));
            return;
        }

        for (var i = 0; i < Sheets.Count; i++)
        {
            if (Sheets[i] == _selectedSheet)
            {
                _selectedIndex = i;
                OnPropertyChanged(nameof(SelectedIndex));
                return;
            }
        }
    }

    private void UpdateSelectedSheet()
    {
        if (_selectedIndex >= 0 && _selectedIndex < Sheets.Count)
        {
            _selectedSheet = Sheets[_selectedIndex];
            OnPropertyChanged(nameof(SelectedSheet));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class SheetPreviewItemViewModel
{
    private readonly double _partGap;
    private readonly double _sheetMargin;
    private readonly double _snapThreshold;

    public SheetInstance Sheet { get; }
    public SheetMaterial Material => Sheet.Material;
    public int Index => Sheet.Index;
    public double UtilizationPercent => Sheet.UtilizationPercent;
    public ObservableCollection<PreviewPartViewModel> Parts { get; }
    public bool HasGrain => Material.HasGrain;
    public string DirectionText { get; }
    public string ArrowGeometry { get; }

    public SheetPreviewItemViewModel(SheetInstance sheet, NestingOptions? options = null)
    {
        Sheet = sheet;
        _partGap = options?.PartGapMm ?? 0.0;
        _sheetMargin = options?.SheetMarginMm ?? 0.0;
        _snapThreshold = AppConstants.PreviewSnapThresholdMm;
        Parts = new ObservableCollection<PreviewPartViewModel>(
            sheet.Parts.Select(p => new PreviewPartViewModel(p.Bounds.Left,
                sheet.Material.Width - p.Bounds.Top, p.Bounds.Width, p.Bounds.Height) { Source = p }));
        var localization = LocalizationService.Instance;
        DirectionText = !Material.HasGrain ? localization.NoGrain :
            Material.GrainDirection == GrainDirection.Horizontal ? localization.GrainHorizontal : localization.GrainVertical;

        if (!Material.HasGrain)
        {
            ArrowGeometry = string.Empty;
            return;
        }

        var cx = Material.Length / 2;
        var cy = Material.Width / 2;
        var half = Math.Min(
            Material.GrainDirection == GrainDirection.Horizontal ? Material.Length / 4 : Material.Width / 4,
            ExportPresentationConstants.MaxArrowHalfLength);
        var start = Material.GrainDirection == GrainDirection.Horizontal
            ? new NStCut.Core.Geometry.Point(cx - half, cy)
            : new NStCut.Core.Geometry.Point(cx, cy + half);
        var end = Material.GrainDirection == GrainDirection.Horizontal
            ? new NStCut.Core.Geometry.Point(cx + half, cy)
            : new NStCut.Core.Geometry.Point(cx, cy - half);

        var (head1, head2) = ArrowGeometryHelper.GetArrowHead(
            start,
            end,
            ExportPresentationConstants.ArrowHeadLength,
            ExportPresentationConstants.ArrowHeadAngle);

        ArrowGeometry = string.Format(
            CultureInfo.InvariantCulture,
            "M {0:F6},{1:F6} L {2:F6},{3:F6} M {2:F6},{3:F6} L {4:F6},{5:F6} M {2:F6},{3:F6} L {6:F6},{7:F6}",
            start.X, start.Y, end.X, end.Y, head1.X, head1.Y, head2.X, head2.Y);
    }

    public (double Left, double Top) SnapToNearest(PreviewPartViewModel part, double newLeft, double newTop)
    {
        if (part.Source is null)
            return (newLeft, newTop);

        var width = part.Source.Bounds.Width;
        var height = part.Source.Bounds.Height;

        var leftCandidates = new List<double>();
        var topCandidates = new List<double>();

        leftCandidates.Add(_sheetMargin);
        leftCandidates.Add(Material.Length - _sheetMargin - _partGap - width);

        topCandidates.Add(_sheetMargin + _partGap);
        topCandidates.Add(Material.Width - _sheetMargin - height);

        foreach (var other in Sheet.Parts)
        {
            if (other == part.Source)
                continue;

            var otherLeft = other.Bounds.X;
            var otherTop = Material.Width - other.Bounds.Y - other.Bounds.Height;

            leftCandidates.Add(otherLeft);
            leftCandidates.Add(otherLeft - width);
            leftCandidates.Add(otherLeft - width - _partGap);
            leftCandidates.Add(otherLeft + other.Bounds.Width);
            leftCandidates.Add(otherLeft + other.Bounds.Width + _partGap);

            topCandidates.Add(otherTop);
            topCandidates.Add(otherTop - height);
            topCandidates.Add(otherTop - height - _partGap);
            topCandidates.Add(otherTop + other.Bounds.Height);
            topCandidates.Add(otherTop + other.Bounds.Height + _partGap);
        }

        var pairs = new List<(double Left, double Top, double Distance)>();
        foreach (var l in leftCandidates)
        {
            var dLeft = Math.Abs(newLeft - l);
            if (dLeft > _snapThreshold)
                continue;

            foreach (var t in topCandidates)
            {
                var dTop = Math.Abs(newTop - t);
                if (dTop > _snapThreshold)
                    continue;

                pairs.Add((l, t, dLeft + dTop));
            }
        }

        foreach (var candidate in pairs.OrderBy(p => p.Distance))
        {
            if (CanPlace(part, candidate.Left, candidate.Top))
                return (candidate.Left, candidate.Top);
        }

        return (newLeft, newTop);
    }

    public bool CanPlace(PreviewPartViewModel part, double newLeft, double newTop)
    {
        if (part.Source is null)
            return false;

        var width = part.Source.Bounds.Width;
        var height = part.Source.Bounds.Height;
        var y = Material.Width - newTop - height;

        if (newLeft < _sheetMargin || y < _sheetMargin)
            return false;

        if (newLeft + width + _partGap > Material.Length - _sheetMargin)
            return false;

        if (y + height + _partGap > Material.Width - _sheetMargin)
            return false;

        var effective = new Rect(newLeft, y, width + _partGap, height + _partGap);

        foreach (var other in Sheet.Parts)
        {
            if (other == part.Source)
                continue;

            var otherEffective = new Rect(
                other.Bounds.X,
                other.Bounds.Y,
                other.Bounds.Width + _partGap,
                other.Bounds.Height + _partGap);

            if (effective.Intersects(otherEffective))
                return false;
        }

        return true;
    }

    public void ApplyPartPosition(PreviewPartViewModel part, double newLeft, double newTop)
    {
        if (part.Source is null)
            return;

        var y = Material.Width - newTop - part.Source.Bounds.Height;
        part.Source.Position = new NStCut.Core.Geometry.Point(newLeft, y);
        part.Source.Bounds = new Rect(newLeft, y, part.Source.Bounds.Width, part.Source.Bounds.Height);
        part.Left = newLeft;
        part.Top = newTop;

        Sheet.UsedWidth = Sheet.Parts.Count > 0 ? Sheet.Parts.Max(p => p.Bounds.Right) : 0.0;
        Sheet.UsedHeight = Sheet.Parts.Count > 0 ? Sheet.Parts.Max(p => p.Bounds.Top) : 0.0;

        var sheetArea = Sheet.Material.Length * Sheet.Material.Width;
        var placedArea = Sheet.Parts.Sum(p => p.Bounds.Area);
        Sheet.WasteArea = sheetArea - placedArea;
        Sheet.UtilizationPercent = sheetArea > 0 ? placedArea / sheetArea * 100.0 : 0.0;
    }

    public bool TryRotate(PreviewPartViewModel part)
    {
        if (part.Source is null)
            return false;

        var source = part.Source;
        var oldBounds = source.Bounds;

        var newRotation = source.Rotation == 0.0 ? 90.0 : 0.0;
        var newSize = newRotation == 0.0
            ? (source.Source.Bounds.Width, source.Source.Bounds.Height)
            : (source.Source.Bounds.Height, source.Source.Bounds.Width);

        if (oldBounds.Width <= 0 || oldBounds.Height <= 0 ||
            newSize.Item1 <= 0 || newSize.Item2 <= 0)
            return false;

        var centerLeft = part.Left + oldBounds.Width / 2.0;
        var centerTop = part.Top + oldBounds.Height / 2.0;

        var candidates = new (double Left, double Top)[]
        {
            (centerLeft - newSize.Item1 / 2.0, centerTop - newSize.Item2 / 2.0),
            (part.Left, part.Top)
        };

        foreach (var (left, top) in candidates)
        {
            var clampedLeft = Math.Max(_sheetMargin, Math.Min(left, Material.Length - _sheetMargin - _partGap - newSize.Item1));
            var clampedTop = Math.Max(_sheetMargin + _partGap, Math.Min(top, Material.Width - _sheetMargin - newSize.Item2));

            source.Bounds = new Rect(clampedLeft, Material.Width - clampedTop - newSize.Item2, newSize.Item1, newSize.Item2);
            if (CanPlace(part, clampedLeft, clampedTop))
            {
                source.Rotation = newRotation;

                part.Left = clampedLeft;
                part.Top = clampedTop;
                part.Width = newSize.Item1;
                part.Height = newSize.Item2;
                part.RebuildContourGeometry();

                Sheet.UsedWidth = Sheet.Parts.Count > 0 ? Sheet.Parts.Max(p => p.Bounds.Right) : 0.0;
                Sheet.UsedHeight = Sheet.Parts.Count > 0 ? Sheet.Parts.Max(p => p.Bounds.Top) : 0.0;

                var sheetArea = Sheet.Material.Length * Sheet.Material.Width;
                var placedArea = Sheet.Parts.Sum(p => p.Bounds.Area);
                Sheet.WasteArea = sheetArea - placedArea;
                Sheet.UtilizationPercent = sheetArea > 0 ? placedArea / sheetArea * 100.0 : 0.0;

                return true;
            }
        }

        source.Bounds = oldBounds;
        return false;
    }
}

public sealed class PreviewPartViewModel : INotifyPropertyChanged
{
    private double _left;
    private double _top;
    private double _width;
    private double _height;
    private PlacedPart? _source;

    public PlacedPart? Source
    {
        get => _source;
        set
        {
            _source = value;
            OnPropertyChanged();
            RebuildContourGeometry();
        }
    }

    public double Left
    {
        get => _left;
        set
        {
            _left = value;
            OnPropertyChanged();
        }
    }

    public double Top
    {
        get => _top;
        set
        {
            _top = value;
            OnPropertyChanged();
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            _width = value;
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            _height = value;
            OnPropertyChanged();
        }
    }

    public string? ContourGeometry { get; private set; }
    public bool HasContour { get; private set; }

    public PreviewPartViewModel(double left, double top, double width, double height)
    {
        _left = left;
        _top = top;
        _width = width;
        _height = height;
    }

    public void RebuildContourGeometry()
    {
        if (Source?.Source.Contour is null || _width <= 0 || _height <= 0)
        {
            ContourGeometry = null;
            HasContour = false;
            OnPropertyChanged(nameof(ContourGeometry));
            OnPropertyChanged(nameof(HasContour));
            return;
        }

        var originalBounds = Source.Source.Bounds;
        var placedBounds = Source.Bounds;
        var vertices = Source.Source.Contour.Vertices;
        if (vertices.Count == 0)
        {
            ContourGeometry = null;
            HasContour = false;
            OnPropertyChanged(nameof(ContourGeometry));
            OnPropertyChanged(nameof(HasContour));
            return;
        }

        var isRotated = Math.Abs(Source.Rotation - 90.0) < 1e-6;
        var sb = new StringBuilder();
        for (var i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            var lx = v.X - originalBounds.X;
            var ly = v.Y - originalBounds.Y;
            double px, py;
            if (isRotated)
            {
                var cx = originalBounds.Width / 2.0;
                var cy = originalBounds.Height / 2.0;
                var rx = cx - (ly - cy);
                var ry = cy + (lx - cx);
                px = rx;
                py = placedBounds.Height - ry;
            }
            else
            {
                px = lx;
                py = placedBounds.Height - ly;
            }

            sb.Append(i == 0 ? "M " : " L ");
            sb.Append(px.ToString("F6", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(py.ToString("F6", CultureInfo.InvariantCulture));
        }

        sb.Append(" Z");
        ContourGeometry = sb.ToString();
        HasContour = true;
        OnPropertyChanged(nameof(ContourGeometry));
        OnPropertyChanged(nameof(HasContour));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
