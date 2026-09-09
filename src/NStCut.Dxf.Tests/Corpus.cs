using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NStCut.Dxf.Tests;

public static class Corpus
{
    private const string FolderName = "Corpus";

    private static readonly Lazy<string> _baseDirectory = new(() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, FolderName)));

    public static string BaseDirectory => _baseDirectory.Value;

    public static IReadOnlyList<string> Enumerate() =>
        System.IO.Directory
            .GetFiles(BaseDirectory, "corpus-*.dxf")
            .OrderBy(Path.GetFileName)
            .ToList();
}
