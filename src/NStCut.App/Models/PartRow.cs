using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStCut.Core.Contracts;
using NStCut.Core.Geometry;
using NStCut.Core.Models;
using NStCut.Core.Nesting;

namespace NStCut.App.Models;

public sealed class PartRow : INotifyPropertyChanged
{
    private int _quantity = 1;
    private string _number = string.Empty;
    private string _title = string.Empty;
    private string _materialId = string.Empty;
    private string _dxfPath = string.Empty;
    private PartStatus _status = PartStatus.Pending;
    private bool _isManuallyResolved;
    private GrainMode _grainMode = GrainMode.FollowMaterial;
    private bool _isEnabled = true;
    private int _rowNumber;
    private int _rowNumberInSection;
    private bool _isDuplicateNumber;
    private Rect _bounds;
    private double _area;
    private double _perimeter;
    private DxfGeometryQuality _geometryQuality = DxfGeometryQuality.Invalid;
    private IReadOnlyList<DxfGeometryDiagnosticCode> _geometryDiagnostics = new List<DxfGeometryDiagnosticCode>();
    private string _geometryDiagnosticText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Порядковый номер строки в общем списке (служебное, используется для Undo/сортировки).</summary>
    public int RowNumber
    {
        get => _rowNumber;
        set { _rowNumber = value; OnPropertyChanged(); }
    }

    /// <summary>Порядковый номер строки внутри раздела материала — отображается в первой колонке таблицы.</summary>
    public int RowNumberInSection
    {
        get => _rowNumberInSection;
        set { _rowNumberInSection = value; OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>Признак того, что где-то в таблице есть другая строка с таким же Number (визуальная подсказка пользователю).</summary>
    public bool IsDuplicateNumber
    {
        get => _isDuplicateNumber;
        set { _isDuplicateNumber = value; OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = Math.Max(1, value); OnPropertyChanged(); }
    }

    public string Number
    {
        get => _number;
        set { _number = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Title
    {
        get => _title;
        set { _title = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => string.Join(" ", new[] { Number, Title }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public Rect Bounds
    {
        get => _bounds;
        set { _bounds = value; OnPropertyChanged(); OnPropertyChanged(nameof(Dimensions)); }
    }

    public double Area
    {
        get => _area;
        set { _area = value; OnPropertyChanged(); }
    }

    public double Perimeter
    {
        get => _perimeter;
        set { _perimeter = value; OnPropertyChanged(); }
    }

    public DxfGeometryQuality GeometryQuality
    {
        get => _geometryQuality;
        set
        {
            _geometryQuality = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGeometryWarning));
            OnPropertyChanged(nameof(HasGeometryError));
        }
    }

    public IReadOnlyList<DxfGeometryDiagnosticCode> GeometryDiagnostics
    {
        get => _geometryDiagnostics;
        set { _geometryDiagnostics = value ?? new List<DxfGeometryDiagnosticCode>(); OnPropertyChanged(); }
    }

    public string GeometryDiagnosticText
    {
        get => _geometryDiagnosticText;
        set { _geometryDiagnosticText = value ?? string.Empty; OnPropertyChanged(); }
    }

    public bool HasGeometryWarning => GeometryQuality is DxfGeometryQuality.Partial or DxfGeometryQuality.BoundsOnly;
    public bool HasGeometryError => GeometryQuality == DxfGeometryQuality.Invalid;

    public string Dimensions => Bounds.Width > 0 && Bounds.Height > 0
        ? $"{Bounds.Width:F1} x {Bounds.Height:F1}"
        : "-";

    private SheetFitResult? _oversizedEvaluation;

    /// <summary>
    /// Result of evaluating the part against its assigned sheet using current margin/gap and grain policy.
    /// Null means the check has not been performed or could not be evaluated.
    /// </summary>
    public SheetFitResult? OversizedEvaluation
    {
        get => _oversizedEvaluation;
        set
        {
            _oversizedEvaluation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOversized));
        }
    }

    /// <summary>
    /// True when the evaluator has determined the part does not fit on the assigned material.
    /// Missing material, missing bounds or an unevaluated state are reported as false.
    /// </summary>
    public bool IsOversized => _oversizedEvaluation?.IsOversized ?? false;

    public void IncreaseQuantity() => Quantity = Quantity >= int.MaxValue ? int.MaxValue : Quantity + 1;

    public void DecreaseQuantity() => Quantity -= 1;

    public string MaterialId
    {
        get => _materialId;
        set { _materialId = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string DxfPath
    {
        get => _dxfPath;
        set { _dxfPath = value ?? string.Empty; OnPropertyChanged(); }
    }

    public PartStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsManuallyResolved
    {
        get => _isManuallyResolved;
        set { _isManuallyResolved = value; OnPropertyChanged(); }
    }

    public GrainMode GrainMode
    {
        get => _grainMode;
        set { _grainMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRotate)); }
    }

    public bool CanRotate
    {
        get => GrainMode == GrainMode.Allow90;
        set
        {
            GrainMode = value ? GrainMode.Allow90 : GrainMode.FollowMaterial;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GrainMode));
        }
    }

    public Part ToPart()
    {
        return new Part
        {
            Number = Number,
            Title = Title,
            Quantity = Quantity,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode,
            IsEnabled = IsEnabled,
            Bounds = Bounds,
            Area = Area,
            Perimeter = Perimeter
        };
    }

    public static PartRow FromPart(Part part)
    {
        return new PartRow
        {
            Number = part.Number,
            Title = part.Title,
            Quantity = part.Quantity,
            MaterialId = part.MaterialId,
            DxfPath = part.DxfPath,
            Status = part.Status,
            IsManuallyResolved = part.IsManuallyResolved,
            GrainMode = part.GrainMode,
            IsEnabled = part.IsEnabled,
            Bounds = part.Bounds,
            Area = part.Area,
            Perimeter = part.Perimeter
        };
    }

    public PartRow Clone()
    {
        return new PartRow
        {
            RowNumber = RowNumber,
            RowNumberInSection = RowNumberInSection,
            Quantity = Quantity,
            Number = Number,
            Title = Title,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode,
            IsEnabled = IsEnabled,
            Bounds = Bounds,
            Area = Area,
            Perimeter = Perimeter,
            OversizedEvaluation = OversizedEvaluation
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
