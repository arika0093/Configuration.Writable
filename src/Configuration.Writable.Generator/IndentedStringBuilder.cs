using System.Text;

namespace Configuration.Writable.Generator;

internal sealed class IndentedStringBuilder
{
    private readonly StringBuilder _builder = new();
    private int _indentation;

    public void IncreaseIndent() => _indentation++;

    public void DecreaseIndent() => _indentation--;

    public void AppendLine(string value)
    {
        foreach (var line in value.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length > 0)
            {
                _builder.Append(' ', _indentation * 4);
            }

            _builder.AppendLine(line);
        }
    }

    public override string ToString() => _builder.ToString();
}
