using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using NStCut.App.Services;
using NStCut.Core.Constants;
using NStCut.Core.Geometry;
using NStCut.Core.Models;

namespace NStCut.App.ViewModels;

public sealed class SheetPreviewViewModel : INotifyPropertyChanged
{
    private double _zoom = 1.0;

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

    public SheetPreviewViewModel(NestingResult result)
    {
        foreach (var sheet in result.Sheets)
            Sheets.Add(new SheetPreviewItemViewModel(sheet));
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
