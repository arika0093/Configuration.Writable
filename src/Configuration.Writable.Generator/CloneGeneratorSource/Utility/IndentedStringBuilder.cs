using System;
using System.Text;

namespace IDeepCloneable.Generator;

/// <summary>
/// Utility class for efficiently building indented strings.
/// </summary>
/// <param name="IndentLevel">Initial indent level (0 or greater).</param>
/// <remarks>Initializes a new instance of the <see cref="IndentedStringBuilder"/> class with an existing StringBuilder.</remarks>
internal class IndentedStringBuilder(StringBuilder stringBuilder, int indentLevel = 0)
{
    /// <summary>Initializes a new instance of the <see cref="IndentedStringBuilder"/> class.</summary>
    public IndentedStringBuilder(int indentLevel = 0)
        : this(new StringBuilder(), indentLevel) { }

    /// <summary>Number of spaces per indent level.</summary>
    public const int IndentSize = 4;

    /// <summary>Current indent level (0 or greater).</summary>
    public int IndentLevel { get; private set; } = indentLevel;

    /// <summary>Returns a string of spaces representing the current indent.</summary>
    public string Indent => new(' ', IndentLevel * IndentSize);

    /// <summary>Returns a new builder with the indent level increased by one.</summary>
    public void IncreaseIndent() => IndentLevel += 1;

    /// <summary>Returns a new builder with the indent level decreased by one (not below zero).</summary>
    public void DecreaseIndent() => IndentLevel = Math.Max(0, IndentLevel - 1);

    /// <summary>
    /// Appends the specified text with the current indent. Newlines in the text are handled per line.
    /// </summary>
    /// <param name="text">The text to append (may contain multiple lines).</param>
    public void AppendLine(string text)
    {
        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        foreach (var line in lines)
        {
            stringBuilder.AppendLine(Indent + line);
        }
    }

    /// <summary>Appends the contents of another IndentedStringBuilder to this builder.</summary>
    /// <param name="builder">The builder whose contents will be appended.</param>
    public void AppendLine(IndentedStringBuilder builder) => AppendLine(builder.ToString());

    /// <summary>Returns the current contents of the builder as a string.</summary>
    public override string ToString() => stringBuilder.ToString();
}
