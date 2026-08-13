using System.Text.Json.Serialization;
using NStCut.Core.Constants;
using NStCut.Core.Models;

namespace NStCut.App.Models;

public sealed class ProjectData
{
    public string SearchPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public List<Part> Parts { get; set; } = new();
    public List<SheetMaterial> Materials { get; set; } = new();
    public NestingOptions Options { get; set; } = DefaultNestingOptions.Value;
}
