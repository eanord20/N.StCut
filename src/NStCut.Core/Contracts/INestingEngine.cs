using NStCut.Core.Models;

namespace NStCut.Core.Contracts;

public interface INestingEngine
{
    NestingResult Nest(
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        NestingOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
