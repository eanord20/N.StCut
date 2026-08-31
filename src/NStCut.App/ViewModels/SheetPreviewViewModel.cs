using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
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

    public bool CanRenest => _nestingEngine is not null && _options is not null;

    private readonly INestingEngine? _nestingEngine;
    private readonly NestingOptions? _options;

    public SheetPreviewViewModel(NestingResult result, INestingEngine? nestingEngine = null, NestingOptions? options = null)
    {
        _nestingEngine = nestingEngine;
        _options = options;

        for (var i = 0; i < result.Sheets.Count; i++)
            Sheets.Add(new SheetPreviewItemViewModel(result.Sheets[i]));

        if (Sheets.Count > 0)
        {
            SelectedSheet = Sheets[0];
            _selectedIndex = 0;
        }

        RenestCurrentSheetCommand = new RelayCommand(_ => RenestCurrentSheet(), _ => CanRenest && SelectedSheet is not null);
        FitToWindowCommand = new RelayCommand(_ => Zoom = 1.0);
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
            var replacement = new SheetPreviewItemViewModel(result.Sheets[0]);
            Sheets[originalIndex] = replacement;
            SelectedSheet = replacement;
            OnPropertyChanged(nameof(Sheets));
        }
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
    public SheetInstance Sheet { get; }
    public SheetMaterial Material => Sheet.Material;
    public int Index => Sheet.Index;
    public double UtilizationPercent => Sheet.UtilizationPercent;
    public IReadOnlyList<PreviewPartViewModel> Parts { get; }
    public bool HasGrain => Material.HasGrain;
    public string DirectionText { get; }
    public string ArrowGeometry { get; }

    public SheetPreviewItemViewModel(SheetInstance sheet)
    {
        Sheet = sheet;
        Parts = sheet.Parts.Select(p => new PreviewPartViewModel(p.Bounds.Left,
            sheet.Material.Width - p.Bounds.Top, p.Bounds.Width, p.Bounds.Height)).ToList();
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
}

public sealed record PreviewPartViewModel(double Left, double Top, double Width, double Height);
