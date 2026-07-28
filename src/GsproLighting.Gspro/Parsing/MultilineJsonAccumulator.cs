using System.Text;

namespace GsproLighting.Gspro.Parsing;

/// <summary>
/// Buffers log fragments until a brace-balanced JSON object is complete.
/// Handles Connect/Unity pretty-printed ball payloads that span multiple lines.
/// </summary>
public sealed class MultilineJsonAccumulator
{
    private readonly StringBuilder _buffer = new();
    private int _depth;
    private bool _inString;
    private bool _escape;
    private bool _started;

    public bool IsBuffering => _started;
    public int Length => _buffer.Length;

    public void Reset()
    {
        _buffer.Clear();
        _depth = 0;
        _inString = false;
        _escape = false;
        _started = false;
    }

    /// <summary>
    /// Appends a fragment. Returns a complete JSON object when braces balance, otherwise null.
    /// </summary>
    public string? Append(string fragment)
    {
        if (string.IsNullOrEmpty(fragment) && !_started)
            return null;

        foreach (var ch in fragment)
        {
            if (!_started)
            {
                if (ch != '{')
                    continue;
                _started = true;
                _depth = 1;
                _buffer.Append(ch);
                continue;
            }

            _buffer.Append(ch);

            if (_escape)
            {
                _escape = false;
                continue;
            }

            if (ch == '\\' && _inString)
            {
                _escape = true;
                continue;
            }

            if (ch == '"')
            {
                _inString = !_inString;
                continue;
            }

            if (_inString)
                continue;

            if (ch == '{')
                _depth++;
            else if (ch == '}')
            {
                _depth--;
                if (_depth == 0)
                {
                    var json = _buffer.ToString();
                    Reset();
                    return json;
                }
            }
        }

        if (_buffer.Length > 64_000)
            Reset();

        return null;
    }

    /// <summary>
    /// Starts (or continues) from the first '{' in <paramref name="line"/>.
    /// </summary>
    public string? AppendFromFirstBrace(string line)
    {
        var start = line.IndexOf('{');
        if (start < 0)
            return null;
        return Append(line[start..]);
    }
}
