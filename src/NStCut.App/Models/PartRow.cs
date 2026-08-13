using System.ComponentModel;
using System.Runtime.CompilerServices;
using NStCut.Core.Models;

namespace NStCut.App.Models;

public sealed class PartRow : INotifyPropertyChanged
{
    private int _quantity = 1;
    private string _name = string.Empty;
    private string _materialId = string.Empty;
    private string _dxfPath = string.Empty;
    private PartStatus _status = PartStatus.Pending;
    private bool _isManuallyResolved;
    private GrainMode _grainMode = GrainMode.FollowMaterial;
    private int _rowNumber;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int RowNumber
    {
        get => _rowNumber;
        set { _rowNumber = value; OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = Math.Max(1, value); OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(); }
    }

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
            Name = Name,
            Quantity = Quantity,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode
        };
    }

    public static PartRow FromPart(Part part)
    {
        return new PartRow
        {
            Name = part.Name,
            Quantity = part.Quantity,
            MaterialId = part.MaterialId,
            DxfPath = part.DxfPath,
            Status = part.Status,
            IsManuallyResolved = part.IsManuallyResolved,
            GrainMode = part.GrainMode
        };
    }

    public PartRow Clone()
    {
        return new PartRow
        {
            RowNumber = RowNumber,
            Quantity = Quantity,
            Name = Name,
            MaterialId = MaterialId,
            DxfPath = DxfPath,
            Status = Status,
            IsManuallyResolved = IsManuallyResolved,
            GrainMode = GrainMode
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
