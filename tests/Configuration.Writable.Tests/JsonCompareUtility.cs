using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Configuration.Writable.Tests;

public class JsonCompareUtility
{
    /// <summary>
    /// Helper method to compare JSON semantically (ignores whitespace and property order)
    /// </summary>
    public static bool JsonEquals(string json1, string json2)
    {
        using var doc1 = JsonDocument.Parse(json1);
        using var doc2 = JsonDocument.Parse(json2);
        return JsonElementEquals(doc1.RootElement, doc2.RootElement);
    }

    /// <summary>
    /// Recursive helper to compare JsonElement objects semantically
    /// </summary>
    public static bool JsonElementEquals(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind)
            return false;

        switch (element1.ValueKind)
        {
            case JsonValueKind.Object:
                var props1 = element1.EnumerateObject().OrderBy(p => p.Name).ToList();
                var props2 = element2.EnumerateObject().OrderBy(p => p.Name).ToList();

                if (props1.Count != props2.Count)
                    return false;

                for (int i = 0; i < props1.Count; i++)
                {
                    if (props1[i].Name != props2[i].Name)
                        return false;
                    if (!JsonElementEquals(props1[i].Value, props2[i].Value))
                        return false;
                }
                return true;

            case JsonValueKind.Array:
                var array1 = element1.EnumerateArray().ToList();
                var array2 = element2.EnumerateArray().ToList();

                if (array1.Count != array2.Count)
                    return false;

                for (int i = 0; i < array1.Count; i++)
                {
                    if (!JsonElementEquals(array1[i], array2[i]))
                        return false;
                }
                return true;

            case JsonValueKind.String:
                return element1.GetString() == element2.GetString();

            case JsonValueKind.Number:
                // Compare numbers by their numeric value, not raw text representation
                // to handle differences in decimal/double serialization across .NET versions
                // (e.g., "99.99" vs "99.990000", or "3.14159" vs "3.1415899999999999")

                // Try double comparison first (most common for floating point)
                if (element1.TryGetDouble(out var dbl1) && element2.TryGetDouble(out var dbl2))
                {
                    // Use epsilon comparison for floating point values
                    return Math.Abs(dbl1 - dbl2) < 1e-10;
                }

                // Try decimal comparison for exact decimal values
                if (element1.TryGetDecimal(out var dec1) && element2.TryGetDecimal(out var dec2))
                    return dec1 == dec2;

                // Try int64 for integer values
                if (element1.TryGetInt64(out var i641) && element2.TryGetInt64(out var i642))
                    return i641 == i642;

                // Fallback to raw text comparison for other number types
                return element1.GetRawText() == element2.GetRawText();

            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;

            default:
                return false;
        }
    }
}
