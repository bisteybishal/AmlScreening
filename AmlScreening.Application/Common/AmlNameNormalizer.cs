using System.Text;

using System.Globalization;

namespace AmlScreening.Application.Common;


public static class AmlNameNormalizer
{
    public static string Normalize(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(
                name))
        {
            return string.Empty;
        }


        var builder =
            new StringBuilder(
                name.Length);


        var previousWasSpace =
            false;


        foreach (var character
            in name
                .Normalize(NormalizationForm.FormKC)
                .Trim()
                .ToUpperInvariant())
        {
            // Preserve combining marks used in Nepali and other names.
            if (char.IsLetterOrDigit(character) || char.GetUnicodeCategory(character) is
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                builder.Append(
                    character);

                previousWasSpace =
                    false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');

                previousWasSpace =
                    true;
            }
        }


        return string.Join(
            " ",
            builder
                .ToString()
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries));
    }


    public static string GetPrefix(
        string? name)
    {
        var normalized =
            Normalize(name);


        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            return string.Empty;
        }


        var longestWord =
            normalized
                .Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .OrderByDescending(
                    x => x.Length)
                .First();


        return longestWord.Length <= 4
            ? longestWord
            : longestWord[..4];
    }
}
