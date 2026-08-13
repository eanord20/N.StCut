using NStCut.Core.Contracts;
using NStCut.Core.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace NStCut.Reports.Services;

public sealed class ReportService : IReportService
{
    public void GenerateReport(
        string outputPath,
        string projectName,
        IReadOnlyList<Part> parts,
        IReadOnlyList<SheetInstance> sheets,
        IReadOnlyList<byte[]> sheetThumbnails)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var doc = DocX.Create(outputPath);

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
        var partTable = doc.AddTable(parts.Count + 1, 4);
        partTable.Design = TableDesign.TableGrid;
        partTable.Rows[0].Cells[0].Paragraphs[0].Append("Part").Bold();
        partTable.Rows[0].Cells[1].Paragraphs[0].Append("Material").Bold();
        partTable.Rows[0].Cells[2].Paragraphs[0].Append("Qty").Bold();
        partTable.Rows[0].Cells[3].Paragraphs[0].Append("Area, mm²").Bold();

        for (int i = 0; i < parts.Count; i++)
        {
            partTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(parts[i].Name);
            partTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(parts[i].MaterialId);
            partTable.Rows[i + 1].Cells[2].Paragraphs[0].Append(parts[i].Quantity.ToString());
            partTable.Rows[i + 1].Cells[3].Paragraphs[0].Append(parts[i].Area.ToString("F1"));
        }
        doc.InsertTable(partTable);
        doc.InsertParagraph();

        // Sheets table + thumbnails
        doc.InsertParagraph("Sheets").Bold().FontSize(14);
        var sheetTable = doc.AddTable(sheets.Count + 1, 4);
        sheetTable.Design = TableDesign.TableGrid;
        sheetTable.Rows[0].Cells[0].Paragraphs[0].Append("#").Bold();
        sheetTable.Rows[0].Cells[1].Paragraphs[0].Append("Material").Bold();
        sheetTable.Rows[0].Cells[2].Paragraphs[0].Append("Utilization %").Bold();
        sheetTable.Rows[0].Cells[3].Paragraphs[0].Append("Waste area, mm²").Bold();

        for (int i = 0; i < sheets.Count; i++)
        {
            sheetTable.Rows[i + 1].Cells[0].Paragraphs[0].Append(sheets[i].Index.ToString());
            sheetTable.Rows[i + 1].Cells[1].Paragraphs[0].Append(sheets[i].Material.Name);
            sheetTable.Rows[i + 1].Cells[2].Paragraphs[0].Append($"{sheets[i].UtilizationPercent:F1}");
            sheetTable.Rows[i + 1].Cells[3].Paragraphs[0].Append($"{sheets[i].WasteArea:F1}");
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

    private static void AddThumbnail(DocX doc, byte[] pngBytes)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(tempPath, pngBytes);
            var image = doc.AddImage(tempPath);
            var picture = image.CreatePicture(400, 300);
            var paragraph = doc.InsertParagraph();
            paragraph.AppendPicture(picture);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
