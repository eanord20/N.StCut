using System.Buffers.Binary;
using System.Text;
using System.Threading;
using NStCut.Core.Constants;
using NStCut.Core.Contracts;
using NStCut.Core.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace NStCut.Reports.Services;

public sealed class ReportService : IReportService
{
    private const string ReportFontFamily = "Arial";
    private const string DefaultDirectionHeader = "Direction";
    private const string DefaultNoGrain = "No grain";
    private const string DefaultHorizontal = "Horizontal";
    private const string DefaultVertical = "Vertical";

    public void GenerateReport(
        string outputPath,
        string projectName,
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetMaterial> materials,
        IReadOnlyList<SheetInstance> sheets,
        IReadOnlyList<byte[]> sheetThumbnails,
        ReportLabels? labels = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var doc = DocX.Create(outputPath);
        doc.SetDefaultFont(new Xceed.Document.NET.Font(ReportFontFamily));

        var materialNames = materials.ToDictionary(m => m.Id, m => m.Name);

        doc.InsertParagraph(projectName)
           .Bold()
           .FontSize(18)
           .Alignment = Alignment.center;

        doc.InsertParagraph($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
           .FontSize(10)
           .Alignment = Alignment.center;

        doc.InsertParagraph();

        // Materials summary
        doc.InsertParagraph("Materials").Bold().FontSize(14);
        var materialTable = doc.AddTable(sheets.GroupBy(s => s.Material.Id).Count() + 1, 4);
        materialTable.Design = TableDesign.TableGrid;
        materialTable.Rows[0].Cells[0].Paragraphs[0].Append("Material").Bold();
        materialTable.Rows[0].Cells[1].Paragraphs[0].Append("Size").Bold();
        materialTable.Rows[0].Cells[2].Paragraphs[0].Append("Sheets").Bold();
        materialTable.Rows[0].Cells[3].Paragraphs[0].Append("Avg. utilization %").Bold();

        int row = 1;
        foreach (var g in sheets.GroupBy(s => s.Material.Id).OrderBy(g => g.First().Material.Name))
        {
            materialTable.Rows[row].Cells[0].Paragraphs[0].Append(g.First().Material.Name);
            materialTable.Rows[row].Cells[1].Paragraphs[0].Append($"{g.First().Material.Length} x {g.First().Material.Width}");
            materialTable.Rows[row].Cells[2].Paragraphs[0].Append(g.Count().ToString());
            materialTable.Rows[row].Cells[3].Paragraphs[0].Append($"{g.Average(s => s.UtilizationPercent):F1}");
            row++;
        }
        doc.InsertTable(materialTable);
        doc.InsertParagraph();

        // Parts table
        doc.InsertParagraph("Parts").Bold().FontSize(14);
        var partTable = doc.AddTable(parts.Count + 1, 5);
        partTable.Design = TableDesign.TableGrid;
        partTable.Rows[0].Cells[0].Paragraphs[0].Append("Part").Bold();
        partTable.Rows[0].Cells[1].Paragraphs[0].Append("Material").Bold();
        partTable.Rows[0].Cells[2].Paragraphs[0].Append("Qty").Bold();
        partTable.Rows[0].Cells[3].Paragraphs[0].Append("Area, mm²").Bold();
        partTable.Rows[0].Cells[4].Paragraphs[0].Append("Perimeter, mm").Bold();

        for (int i = 0; i < parts.Count; i++)
        {
            var materialName = materialNames.TryGetValue(parts[i].MaterialId, out var name) ? name : parts[i].MaterialId;
            partTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(parts[i].DisplayName);
            partTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(materialName);
            partTable.Rows[i + 1].Cells[2].Paragraphs[0].Append(parts[i].Quantity.ToString());
            partTable.Rows[i + 1].Cells[3].Paragraphs[0].Append(parts[i].TrueArea.ToString("F1"));
            partTable.Rows[i + 1].Cells[4].Paragraphs[0].Append(parts[i].TruePerimeter.ToString("F1"));
        }
        doc.InsertTable(partTable);
        doc.InsertParagraph();

        // Sheets table + thumbnails
        doc.InsertParagraph("Sheets").Bold().FontSize(14);
        var sheetTable = doc.AddTable(sheets.Count + 1, 5);
        sheetTable.Design = TableDesign.TableGrid;
        sheetTable.Rows[0].Cells[0].Paragraphs[0].Append("#").Bold();
        sheetTable.Rows[0].Cells[1].Paragraphs[0].Append("Material").Bold();
        sheetTable.Rows[0].Cells[2].Paragraphs[0].Append("Utilization %").Bold();
        sheetTable.Rows[0].Cells[3].Paragraphs[0].Append("Waste area, mm²").Bold();
        sheetTable.Rows[0].Cells[4].Paragraphs[0].Append(labels?.DirectionHeader ?? DefaultDirectionHeader).Bold();

        for (int i = 0; i < sheets.Count; i++)
        {
            sheetTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(sheets[i].Index.ToString());
            sheetTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(sheets[i].Material.Name);
            sheetTable.Rows[i + 1].Cells[2].Paragraphs[0].Append($"{sheets[i].UtilizationPercent:F1}");
            sheetTable.Rows[i + 1].Cells[3].Paragraphs[0].Append($"{sheets[i].WasteArea:F1}");
            sheetTable.Rows[i + 1].Cells[4].Paragraphs[0].Append(GetDirectionLabel(sheets[i], labels));
        }
        doc.InsertTable(sheetTable);
        doc.InsertParagraph();

        for (int i = 0; i < sheets.Count && i < sheetThumbnails.Count; i++)
        {
            doc.InsertParagraph($"Sheet {sheets[i].Index} — {sheets[i].Material.Name}").Bold();
            AddThumbnail(doc, sheetThumbnails[i]);
            doc.InsertParagraph();
        }

        doc.Save();
    }

    private static string GetDirectionLabel(SheetInstance sheet, ReportLabels? labels)
    {
        if (!sheet.Material.HasGrain)
            return labels?.NoGrain ?? DefaultNoGrain;

        return sheet.Material.GrainDirection == GrainDirection.Horizontal
            ? labels?.Horizontal ?? DefaultHorizontal
            : labels?.Vertical ?? DefaultVertical;
    }

    private static void AddThumbnail(DocX doc, byte[] pngBytes)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(tempPath, pngBytes);
            var image = doc.AddImage(tempPath);
            var (width, height) = GetThumbnailSize(pngBytes);
            var picture = image.CreatePicture(height, width);
            var paragraph = doc.InsertParagraph();
            paragraph.AppendPicture(picture);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    internal static (int Width, int Height) GetThumbnailSize(byte[] pngBytes)
    {
        if (TryReadPngIhdrDimensions(pngBytes, out var originalWidth, out var originalHeight) &&
            originalWidth > 0 && originalHeight > 0)
        {
            if (originalWidth <= ExportPresentationConstants.MaxReportThumbnailWidth &&
                originalHeight <= ExportPresentationConstants.MaxReportThumbnailHeight)
            {
                return (originalWidth, originalHeight);
            }

            var scale = Math.Min(
                (double)ExportPresentationConstants.MaxReportThumbnailWidth / originalWidth,
                (double)ExportPresentationConstants.MaxReportThumbnailHeight / originalHeight);

            return ((int)(originalWidth * scale), (int)(originalHeight * scale));
        }

        return (ExportPresentationConstants.MaxReportThumbnailWidth, ExportPresentationConstants.MaxReportThumbnailHeight);
    }

    private static bool TryReadPngIhdrDimensions(byte[] pngBytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        ReadOnlySpan<byte> signature = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        const int ihdrHeaderSize = 8 + 13; // chunk length + chunk type + IHDR data
        if (pngBytes == null || pngBytes.Length < signature.Length + ihdrHeaderSize)
            return false;

        if (!pngBytes.AsSpan(0, signature.Length).SequenceEqual(signature))
            return false;

        var offset = signature.Length;
        if (pngBytes.Length < offset + 8)
            return false;

        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset, 4));
        offset += 4;
        var chunkType = Encoding.ASCII.GetString(pngBytes, offset, 4);
        offset += 4;

        if (chunkType != "IHDR" || length != 13)
            return false;

        if (pngBytes.Length < offset + 13)
            return false;

        width = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset, 4));
        offset += 4;
        height = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(offset, 4));

        return width > 0 && height > 0;
    }
}
