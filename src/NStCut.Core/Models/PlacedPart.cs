using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStCut.Core.Geometry;

namespace NStCut.Core.Models;

public sealed class PlacedPart : INotifyPropertyChanged
{
    private Part _source = null!;
    private int _copyIndex;
    private Point _position;
    private double _rotation;
    private Rect _bounds;

    public Part Source
    {
        get => _source;
        set
        {
            _source = value;
            OnPropertyChanged();
        }
    }

    public int CopyIndex
    {
        get => _copyIndex;
        set
        {
            _copyIndex = value;
            OnPropertyChanged();
        }
    }

    public Point Position
    {
        get => _position;
        set
        {
            _position = value;
            OnPropertyChanged();
        }
    }

    public double Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            OnPropertyChanged();
        }
    }

    public Rect Bounds
    {
        get => _bounds;
        set
        {
            _bounds = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
