using NStCut.Core.Models;

namespace NStCut.App.Services;

/// <summary>
/// Разбирает одну строку, вставленную из буфера обмена (Excel) или прочитанную из .xlsx,
/// на Номер/Название/Количество. Общий для PasteFromClipboard и ImportExcelFile,
/// чтобы оба способа вставки понимали один и тот же набор форматов колонок.
/// </summary>
public static class PastedRowParser
{
    public static (string Number, string Title, int Quantity) Parse(IReadOnlyList<string> cells)
    {
        if (cells.Count == 0)
            return (string.Empty, string.Empty, 1);

        if (cells.Count == 1)
        {
            var (number, title) = PartNameParser.Split(cells[0]);
            return (number, title, 1);
        }

        var num = cells[0].Trim();
        var titleText = cells[1].Trim();
        var quantity = 1;
        if (cells.Count >= 3 && int.TryParse(cells[2].Trim(), out var parsedQty))
            quantity = Math.Max(1, parsedQty);

        return (num, titleText, quantity);
    }
}
