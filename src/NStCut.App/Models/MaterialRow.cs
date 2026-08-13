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
        set { _length = value; OnPropertyChanged(); }
    }

    public double Width
    {
        get => _width;
        set { _width = value; OnPropertyChanged(); }
    }

    public double? Thickness
    {
        get => _thickness;
        set { _thickness = value; OnPropertyChanged(); }
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
            GrainDirection = GrainDirection
        };
    }

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
            GrainDirection = material.GrainDirection
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
