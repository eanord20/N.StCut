using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NStCut.Core.Models;

namespace NStCut.Core.Nesting;

/// <summary>
/// Computes a deterministic hash for a set of nested sheets.
/// Two layouts with the same coordinates, sizes and rotations produce the same hash,
/// regardless of the order the parts were placed.
/// </summary>
public static class LayoutHash
{
    public static string Compute(IReadOnlyList<SheetInstance> sheets)
    {
        var sb = new StringBuilder();

        foreach (var sheet in sheets.OrderBy(s => s.Index))
        {
            foreach (var part in sheet.Parts
                         .OrderBy(p => p.Source.Number)
                         .ThenBy(p => p.CopyIndex)
                         .ThenBy(p => p.Position.X)
                         .ThenBy(p => p.Position.Y))
            {
                sb.Append(sheet.Index.ToString(CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Source.Number);
                sb.Append(';');
                sb.Append(part.CopyIndex.ToString(CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Position.X.ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Position.Y.ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Bounds.Width.ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Bounds.Height.ToString("F6", CultureInfo.InvariantCulture));
                sb.Append(';');
                sb.Append(part.Rotation.ToString("F6", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
