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
        ReportContentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var doc = DocX.Create(outputPath);
        doc.SetDefaultFont(new Xceed.Document.NET.Font(ReportFontFamily));

        var materialNames = materials.ToDictionary(m => m.Id, m => m.Name);
        var showSummary = options?.ShowSummary ?? true;
        var includeMaterial = options?.IncludeMaterial ?? true;
        var showPartNames = options?.ShowPartNames ?? true;
        var showArea = options?.ShowArea ?? true;
        var areaScale = options?.AreaInSquareMeters == true ? 1e-6 : 1.0;
        var areaFormat = options?.AreaInSquareMeters == true ? "F3" : "F1";
        var areaHeader = options?.AreaInSquareMeters == true
            ? (labels?.AreaM2Header ?? "Area, m²")
            : (labels?.AreaMm2Header ?? "Area, mm²");
        string AreaText(double v) => (v * areaScale).ToString(areaFormat);

        doc.InsertParagraph(string.IsNullOrWhiteSpace(options?.Title) ? projectName : options!.Title)
           .Bold()
           .FontSize(18)
           .Alignment = Alignment.center;

        var stamp = string.Join(" ", new[]
        {
            options is null || options.TitleIncludeDate ? DateTime.Now.ToString("yyyy-MM-dd") : null,
            options is null || options.TitleIncludeTime ? DateTime.Now.ToString("HH:mm") : null
        }.Where(v => v != null));
        if (stamp.Length > 0)
            doc.InsertParagraph($"{labels?.Generated ?? "Generated"}: {stamp}").FontSize(10).Alignment = Alignment.center;

        doc.InsertParagraph();

        if (showSummary)
        {
            // Materials summary
            doc.InsertParagraph(labels?.MaterialsHeader ?? "Materials").Bold().FontSize(14);
            var materialTable = doc.AddTable(sheets.GroupBy(s => s.Material.Id).Count() + 1, 4);
            materialTable.Design = TableDesign.TableGrid;
            materialTable.Rows[0].Cells[0].Paragraphs[0].Append(labels?.MaterialHeader ?? "Material").Bold();
            materialTable.Rows[0].Cells[1].Paragraphs[0].Append(labels?.SizeHeader ?? "Size").Bold();
            materialTable.Rows[0].Cells[2].Paragraphs[0].Append(labels?.SheetsHeader ?? "Sheets").Bold();
            materialTable.Rows[0].Cells[3].Paragraphs[0].Append(labels?.AvgUtilizationHeader ?? "Avg. utilization %").Bold();

            int row = 1;
            foreach (var g in sheets.GroupBy(s => s.Material.Id).OrderBy(g => g.First().Material.Name))
            {
                materialTable.Rows[row].Cells[0].Paragraphs[0].Append(g.First().Material.Name);
                var mat = g.First().Material;
                var size = mat.Thickness is double t
                    ? $"{mat.Length} x {mat.Width} x {t}"
                    : $"{mat.Length} x {mat.Width}";
                materialTable.Rows[row].Cells[1].Paragraphs[0].Append(size);
                materialTable.Rows[row].Cells[2].Paragraphs[0].Append(g.Count().ToString());
                materialTable.Rows[row].Cells[3].Paragraphs[0].Append($"{g.Average(s => s.UtilizationPercent):F1}");
                row++;
            }
            doc.InsertTable(materialTable);
            doc.InsertParagraph();

            // Parts table
            doc.InsertParagraph(labels?.PartsHeader ?? "Parts").Bold().FontSize(14);
            var partCols = showArea ? 6 : 5;
            var partTable = doc.AddTable(parts.Count + 1, partCols);
            partTable.Design = TableDesign.TableGrid;
            partTable.Rows[0].Cells[0].Paragraphs[0].Append(labels?.PartHeader ?? "Part").Bold();
            partTable.Rows[0].Cells[1].Paragraphs[0].Append(labels?.MaterialHeader ?? "Material").Bold();
            partTable.Rows[0].Cells[2].Paragraphs[0].Append(labels?.QtyHeader ?? "Qty").Bold();
            partTable.Rows[0].Cells[3].Paragraphs[0].Append(labels?.SizeMmHeader ?? "Size, mm").Bold();
            partTable.Rows[0].Cells[4].Paragraphs[0].Append(showArea ? areaHeader : (labels?.PerimeterHeader ?? "Perimeter, mm")).Bold();
            if (showArea)
                partTable.Rows[0].Cells[5].Paragraphs[0].Append(labels?.PerimeterHeader ?? "Perimeter, mm").Bold();

            for (int i = 0; i < parts.Count; i++)
            {
                var materialName = includeMaterial && materialNames.TryGetValue(parts[i].MaterialId, out var name)
                    ? name
                    : string.Empty;
                partTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(showPartNames ? parts[i].DisplayName : parts[i].Number);
                partTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(materialName);
                partTable.Rows[i + 1].Cells[2].Paragraphs[0].Append(parts[i].Quantity.ToString());
                partTable.Rows[i + 1].Cells[3].Paragraphs[0].Append($"{parts[i].Bounds.Width:F1} x {parts[i].Bounds.Height:F1}");
                var c = 4;
                if (showArea)
                    partTable.Rows[i + 1].Cells[c++].Paragraphs[0].Append(AreaText(parts[i].TrueArea));
                partTable.Rows[i + 1].Cells[c].Paragraphs[0].Append(parts[i].TruePerimeter.ToString("F1"));
            }
            doc.InsertTable(partTable);
            doc.InsertParagraph();
        }

        // Sheets table + thumbnails
        doc.InsertParagraph(labels?.SheetsHeader ?? "Sheets").Bold().FontSize(14);
        var sheetCols = showArea ? 5 : 4;
        var sheetTable = doc.AddTable(sheets.Count + 1, sheetCols);
        sheetTable.Design = TableDesign.TableGrid;
        sheetTable.Rows[0].Cells[0].Paragraphs[0].Append("#").Bold();
        sheetTable.Rows[0].Cells[1].Paragraphs[0].Append(labels?.MaterialHeader ?? "Material").Bold();
        sheetTable.Rows[0].Cells[2].Paragraphs[0].Append(labels?.UtilizationHeader ?? "Utilization %").Bold();
        var sc = 3;
        if (showArea)
            sheetTable.Rows[0].Cells[sc++].Paragraphs[0].Append(labels?.WasteAreaHeader ?? "Waste area").Bold();
        sheetTable.Rows[0].Cells[sc].Paragraphs[0].Append(labels?.DirectionHeader ?? DefaultDirectionHeader).Bold();

        for (int i = 0; i < sheets.Count; i++)
        {
            sheetTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(sheets[i].Index.ToString());
            sheetTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(includeMaterial ? sheets[i].Material.Name : string.Empty);
            sheetTable.Rows[i + 1].Cells[2].Paragraphs[0].Append($"{sheets[i].UtilizationPercent:F1}");
            var cc = 3;
            if (showArea)
                sheetTable.Rows[i + 1].Cells[cc++].Paragraphs[0].Append(AreaText(sheets[i].WasteArea));
            sheetTable.Rows[i + 1].Cells[cc].Paragraphs[0].Append(GetDirectionLabel(sheets[i], labels));
        }
        doc.InsertTable(sheetTable);
        doc.InsertParagraph();

        for (int i = 0; i < sheets.Count && i < sheetThumbnails.Count; i++)
        {
            var sheet = sheets[i];
            doc.InsertParagraph($"{labels?.SheetLabel ?? "Sheet"} {sheet.Index} — {sheet.Material.Name}").Bold();

            var numbers = SheetPartNumbering.Build(sheet);
            var groups = sheet.Parts
                .GroupBy(p => p.Source)
                .OrderBy(g => numbers[g.Key])
                .ToList();
            var sheetPartTable = doc.AddTable(groups.Count + 1, 5);
            sheetPartTable.Design = TableDesign.TableGrid;
            sheetPartTable.Rows[0].Cells[0].Paragraphs[0].Append("№").Bold();
            sheetPartTable.Rows[0].Cells[1].Paragraphs[0].Append(labels?.PartHeader ?? "Part").Bold();
            sheetPartTable.Rows[0].Cells[2].Paragraphs[0].Append(labels?.NumberHeader ?? "Number").Bold();
            sheetPartTable.Rows[0].Cells[3].Paragraphs[0].Append(labels?.QtyHeader ?? "Qty").Bold();
            sheetPartTable.Rows[0].Cells[4].Paragraphs[0].Append(labels?.SizeMmHeader ?? "Size, mm").Bold();
            var r = 1;
            foreach (var g in groups)
            {
                var part = g.Key;
                sheetPartTable.Rows[r].Cells[0].Paragraphs[0].Append(numbers[part].ToString());
                sheetPartTable.Rows[r].Cells[1].Paragraphs[0].Append(showPartNames ? part.DisplayName : part.Number);
                sheetPartTable.Rows[r].Cells[2].Paragraphs[0].Append(part.Number);
                sheetPartTable.Rows[r].Cells[3].Paragraphs[0].Append(g.Count().ToString());
                sheetPartTable.Rows[r].Cells[4].Paragraphs[0].Append($"{part.Bounds.Width:F1} x {part.Bounds.Height:F1}");
                r++;
            }
            doc.InsertTable(sheetPartTable);
            doc.InsertParagraph();

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
