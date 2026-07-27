using System.Text;

namespace GsproLighting.Gspro.Framing;

/// <summary>
/// Splits a TCP byte stream into discrete JSON messages.
/// Prefers newline-delimited JSON; falls back to brace-balanced extraction.
/// </summary>
public sealed class NewlineJsonFramer
{
    private readonly StringBuilder _buffer = new();

    public IEnumerable<string> Push(ReadOnlySpan<byte> bytes)
    {
        _buffer.Append(Encoding.UTF8.GetString(bytes));
        return Drain();
    }

    public IEnumerable<string> Push(string text)
    {
        _buffer.Append(text);
        return Drain();
    }

    private List<string> Drain()
    {
        var messages = new List<string>();
        while (TryExtractMessage(out var message))
            messages.Add(message);
        return messages;
    }

    private bool TryExtractMessage(out string message)
    {
        message = string.Empty;
        var content = _buffer.ToString();
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var start = content.IndexOf('{');
        if (start < 0)
        {
            _buffer.Clear();
            return false;
        }

        if (start > 0)
            _buffer.Remove(0, start);

        content = _buffer.ToString();
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (c == '\\')
                    escaped = true;
                else if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth != 0)
                    continue;

                message = content[..(i + 1)].Trim();
                var consumed = i + 1;
                while (consumed < content.Length &&
                       (content[consumed] == '\r' || content[consumed] == '\n' ||
                        char.IsWhiteSpace(content[consumed])))
                    consumed++;

                _buffer.Remove(0, consumed);
                return !string.IsNullOrWhiteSpace(message);
            }
        }

        return false;
    }
}
