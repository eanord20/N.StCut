using NStCut.Core.Models;

namespace NStCut.App.Converters;

public sealed class GrainModeProxy
{
    public GrainMode Value { get; set; }
    public string DisplayName => Value.ToString();
}
