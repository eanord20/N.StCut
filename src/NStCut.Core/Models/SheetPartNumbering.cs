namespace NStCut.Core.Models;

/// <summary>
/// Порядковая нумерация уникальных деталей на листе: номер присваивается
/// по порядку первого появления исходной детали (все копии одной детали
/// получают один и тот же номер). Используется и в отчёте, и в превью.
/// </summary>
public static class SheetPartNumbering
{
    public static IReadOnlyDictionary<Part, int> Build(SheetInstance sheet)
    {
        var numbers = new Dictionary<Part, int>();
        var next = 1;
        foreach (var placed in sheet.Parts)
        {
            if (!numbers.ContainsKey(placed.Source))
                numbers[placed.Source] = next++;
        }
        return numbers;
    }
}
