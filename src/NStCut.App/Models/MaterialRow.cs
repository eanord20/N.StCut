using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStCut.Core.Models;

namespace NStCut.App.Models;

public sealed class MaterialRow : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private double _length;
    private double _width;
    private double? _thickness;
    private bool _hasGrain;
    private GrainDirection _grainDirection;
    private double? _sheetMarginMm;
    private double? _partGapMm;
    private bool? _allowMarginRelax;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _id;
        set { _id = value ?? Guid.NewGuid().ToString("N"); OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(); }
    }

    public double Length
    {
        get => _length;
        set { _length = double.IsFinite(value) ? Math.Max(0, value) : 0; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _width;
        set { _width = double.IsFinite(value) ? Math.Max(0, value) : 0; OnPropertyChanged(); }
    }

    public double? Thickness
    {
        get => _thickness;
        set { _thickness = value is double t && double.IsFinite(t) ? Math.Max(0, t) : value; OnPropertyChanged(); }
    }

    public bool HasGrain
    {
        get => _hasGrain;
        set { _hasGrain = value; OnPropertyChanged(); }
    }

    public GrainDirection GrainDirection
    {
        get => _grainDirection;
        set { _grainDirection = value; OnPropertyChanged(); }
    }

    public double? SheetMarginMm
    {
        get => _sheetMarginMm;
        set { _sheetMarginMm = value is double v && double.IsFinite(v) ? Math.Max(0, v) : null; OnPropertyChanged(); }
    }

    public double? PartGapMm
    {
        get => _partGapMm;
        set { _partGapMm = value is double v && double.IsFinite(v) ? Math.Max(0, v) : null; OnPropertyChanged(); }
    }

    public bool? AllowMarginRelax
    {
        get => _allowMarginRelax;
        set { _allowMarginRelax = value; OnPropertyChanged(); }
    }

    public SheetMaterial ToMaterial()
    {
        return new SheetMaterial
        {
            Id = Id,
            Name = Name,
            Length = Length,
            Width = Width,
            Thickness = Thickness,
            HasGrain = HasGrain,
            GrainDirection = GrainDirection,
            SheetMarginMm = SheetMarginMm,
            PartGapMm = PartGapMm,
            AllowMarginRelax = AllowMarginRelax
        };
    }

    public MaterialRow Clone() => FromMaterial(ToMaterial());

    public static MaterialRow FromMaterial(SheetMaterial material)
    {
        return new MaterialRow
        {
            Id = material.Id,
            Name = material.Name,
            Length = material.Length,
            Width = material.Width,
            Thickness = material.Thickness,
            HasGrain = material.HasGrain,
            GrainDirection = material.GrainDirection,
            SheetMarginMm = material.SheetMarginMm,
            PartGapMm = material.PartGapMm,
            AllowMarginRelax = material.AllowMarginRelax
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
