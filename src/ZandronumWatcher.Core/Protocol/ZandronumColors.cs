using System.Text;

namespace ZandronumWatcher.Core.Protocol;

/// <summary>Strips Zandronum's in-band colour escapes from server-provided text.</summary>
public static class ZandronumColors
{
    private const char Escape = '\u001c';

    /// <summary>
    /// Removes <c>\x1c</c> escapes. Both the legacy single-character form (<c>\x1cA</c>) and the
    /// bracketed form (<c>\x1c[Red]</c>) are handled.
    /// </summary>
    public static string Strip(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(Escape))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != Escape)
            {
                builder.Append(text[i]);
                continue;
            }

            // Skip the escape itself, then its argument.
            i++;
            if (i >= text.Length)
            {
                break;
            }

            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i);
                if (close < 0)
                {
                    break;
                }

                i = close;
            }
        }

        return builder.ToString();
    }
}
