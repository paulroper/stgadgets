using System.Collections;
using System.Text.RegularExpressions;

namespace Stgadgets.Core.Maps;

/// <summary>
/// An array variable's value: one element per entry in the table, each its fields by name. Compared by content, so a
/// tick only counts it as changed when an element did.
/// </summary>
public sealed class ArrayValue(IReadOnlyList<IReadOnlyDictionary<string, object?>> elements)
    : IReadOnlyList<IReadOnlyDictionary<string, object?>>, IEquatable<ArrayValue>
{
    public int Count => elements.Count;

    public IReadOnlyDictionary<string, object?> this[int index] => elements[index];

    /// <summary>A copy with one element's field changed.</summary>
    public ArrayValue With(int index, string field, object? value)
    {
        var copy = elements.ToArray();
        copy[index] = new Dictionary<string, object?>(copy[index]) { [field] = value };
        return new ArrayValue(copy);
    }

    public bool Equals(ArrayValue? other) =>
        other is not null && other.Count == Count && elements.Zip(other).All(pair => ElementsEqual(pair.First, pair.Second));

    public override bool Equals(object? obj) => Equals(obj as ArrayValue);

    public override int GetHashCode() => Count;

    public IEnumerator<IReadOnlyDictionary<string, object?>> GetEnumerator() => elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static bool ElementsEqual(IReadOnlyDictionary<string, object?> a, IReadOnlyDictionary<string, object?> b) =>
        a.Count == b.Count && a.All(field => b.TryGetValue(field.Key, out var value) && Equals(field.Value, value));
}

/// <summary>
/// A value's name as tests and tools write it: a variable or derived value's name, or one field of an array element,
/// <c>enemies[3].hp</c>.
/// </summary>
public sealed partial record ValuePath(string Name, int? Index = null, string? Field = null)
{
    public static ValuePath Parse(string text) =>
        ElementField().Match(text) is { Success: true } match
            ? new ValuePath(match.Groups["name"].Value, int.Parse(match.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups["field"].Value)
            : new ValuePath(text);

    /// <summary>The value at this path in one tick's values; false if there's no such variable, element or field.</summary>
    public bool TryGet(IReadOnlyDictionary<string, object?> values, out object? value)
    {
        value = null;
        if (!values.TryGetValue(Name, out var whole))
            return false;
        if (Index is not { } index)
        {
            value = whole;
            return true;
        }
        return whole is ArrayValue array && index < array.Count && array[index].TryGetValue(Field!, out value);
    }

    /// <summary>Sets the value at this path; false if there's no such variable, element or field.</summary>
    public bool TrySet(Dictionary<string, object?> values, object? value)
    {
        if (!values.TryGetValue(Name, out var whole))
            return false;
        if (Index is not { } index)
        {
            values[Name] = value;
            return true;
        }
        if (whole is not ArrayValue array || index >= array.Count || !array[index].ContainsKey(Field!))
            return false;
        values[Name] = array.With(index, Field!, value);
        return true;
    }

    public override string ToString() => Index is { } index ? $"{Name}[{index}].{Field}" : Name;

    [GeneratedRegex(@"^(?<name>[^\[\]]+)\[(?<index>\d+)\]\.(?<field>[^\[\]]+)$")]
    private static partial Regex ElementField();
}
