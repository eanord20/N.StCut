using System.Text;

namespace NStCut.Core.Models;

/// <summary>
/// Разбивает единое имя детали (как раньше хранилось в Part.Name, либо как оно приходит
/// из Excel/буфера обмена одной колонкой) на "Номер" (ведущий числовой/точечный токен)
/// и "Название" (остаток строки). Используется для миграции старых проектов и импорта.
/// </summary>
public static class PartNameParser
{
    public static (string Number, string Title) Split(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (string.Empty, string.Empty);

        var trimmed = raw.Trim();
        var sb = new StringBuilder();
        int i = 0;
        while (i < trimmed.Length && IsNumberToken(trimmed[i]))
        {
            sb.Append(trimmed[i]);
            i++;
        }

        var number = sb.ToString().Trim('.', '-');
        var rest = trimmed[i..].Trim();

        if (string.IsNullOrEmpty(number))
            return (string.Empty, trimmed);

        return (number, rest);
    }

    private static bool IsNumberToken(char ch)
        => char.IsDigit(ch) || ch is '.' or '-';
}
