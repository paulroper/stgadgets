using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Stgadgets.Core.Maps;

namespace Stgadgets.Core.Derived;

/// <summary>A value declared in derived.js.</summary>
public sealed record DerivedValue(string Name, string? Description = null);

/// <summary>A derived value that failed to compute on one tick. Its value is null for that tick.</summary>
public sealed record DerivedError(string Name, string Message);

/// <summary>
/// A pack's derived.js: values computed from the map's variables each tick, so layouts only display state.
/// See docs/memory-maps.md#derived-values. This and <see cref="DerivedEvaluator"/> are the only code that knows
/// about the script engine (Jint).
/// </summary>
public sealed class DerivedScript
{
    public const string FileName = "derived.js";

    private DerivedScript(string path, string source, IReadOnlyList<DerivedValue> values)
    {
        Path = path;
        Source = source;
        Values = values;
    }

    public string Path { get; }

    public string Source { get; }

    /// <summary>The derived values, in the order they're computed.</summary>
    public IReadOnlyList<DerivedValue> Values { get; }

    /// <summary>
    /// Loads and checks <paramref name="path"/>: it must default-export an object whose entries are functions, or
    /// <c>{ description, value }</c> objects whose <c>value</c> is a function.
    /// </summary>
    public static DerivedScript Load(string path)
    {
        var source = File.ReadAllText(path);
        try
        {
            using var engine = CreateEngine(out var deadline);
            var values = Functions(engine, deadline, source).Select(f => f.Value).ToList();
            return new DerivedScript(path, source, values);
        }
        catch (Exception ex) when (ex is JavaScriptException or JintException or TimeoutException or Acornima.ParseErrorException or FormatException)
        {
            throw new FormatException($"{path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A fresh evaluator for one run of the game: it keeps the previous tick's values. It's warmed up on a few ticks of
    /// made-up <paramref name="map"/> variables before it's returned, because the first calls into the engine cost
    /// several milliseconds, and that belongs at game start, not in the first ticks.
    /// </summary>
    public DerivedEvaluator CreateEvaluator(MemoryMap map)
    {
        var engine = CreateEngine(out var deadline);
        var evaluator = new DerivedEvaluator(engine, Functions(engine, deadline, Source));
        foreach (byte fill in (byte[])[0x00, 0x01, 0x11, 0x01])   // vary the values so scripts take more of their branches
        {
            var bytes = Enumerable.Repeat(fill, 8).ToArray();
            evaluator.Evaluate(map.Variables.ToDictionary(v => v.Name, v => MapDecoder.Decode(v.Type, bytes.AsSpan(0, v.Type.Size), map.Endianness)));
        }
        evaluator.Restart();
        return evaluator;
    }

    // Sandboxed: no .NET access and no I/O. Time, memory and recursion are bounded for each tick so a runaway script
    // can't stall the game loop: one frame is the most a tick may spend, and real scripts take microseconds.
    private static Engine CreateEngine(out Deadline deadline)
    {
        var limit = new Deadline(TickLimit);
        deadline = limit;
        return new(options => options
            .Strict()
            .Constraint(limit)
            .LimitMemory(16_000_000)
            .LimitRecursion(256));
    }

    private static readonly TimeSpan TickLimit = TimeSpan.FromMilliseconds(16);

    // Loading parses and runs the whole module, once, before the game loop uses it. Cold (the first script in the
    // process, while .NET is still compiling Jint) that takes tens of milliseconds, so it gets more time than a tick.
    private static readonly TimeSpan LoadLimit = TimeSpan.FromSeconds(2);

    private static List<(DerivedValue Value, Function Function)> Functions(Engine engine, Deadline deadline, string source)
    {
        engine.Modules.Add("derived", source);
        deadline.Limit = LoadLimit;
        deadline.Reset();
        JsValue module;
        try
        {
            module = engine.Modules.Import("derived").Get("default");
        }
        finally
        {
            deadline.Limit = TickLimit;
        }
        if (module is not ObjectInstance exports)
            throw new FormatException("must 'export default' an object of functions: { name: (v, prev) => value }");

        return exports.GetOwnPropertyKeys(Types.String)
            .Select(key => Parse(key.AsString(), exports.Get(key)))
            .ToList();
    }

    // An entry is either the function itself or { description, value }.
    private static (DerivedValue, Function) Parse(string name, JsValue entry)
    {
        if (entry is Function function)
            return (new DerivedValue(name), function);

        if (entry is ObjectInstance form && form.Get("value") is Function value)
        {
            var unknown = form.GetOwnPropertyKeys(Types.String).Select(k => k.AsString()).Except(["value", "description"]).FirstOrDefault();
            if (unknown is not null)
                throw new FormatException($"'{name}' has an unknown key '{unknown}'; expected only 'value' and 'description'");

            var description = form.Get("description");
            if (!description.IsUndefined() && description is not JsString)
                throw new FormatException($"'{name}': description must be a string");
            return (new DerivedValue(name, description.IsUndefined() ? null : description.ToString()), value);
        }

        throw new FormatException($"'{name}' must be a function (v, prev) => value, or {{ description, value: (v, prev) => value }}");
    }
}

/// <summary>Stops a script that runs past <see cref="Limit"/> since the last <see cref="Reset"/>.</summary>
internal sealed class Deadline(TimeSpan limit) : Constraint
{
    private long _deadline = long.MaxValue;

    public TimeSpan Limit { get; set; } = limit;

    public override void Check()
    {
        if (Stopwatch.GetTimestamp() > _deadline)
            throw new TimeoutException($"Script ran for more than {Limit.TotalMilliseconds} ms");
    }

    public override void Reset() => _deadline = Stopwatch.GetTimestamp() + (long)(Limit.TotalSeconds * Stopwatch.Frequency);
}

/// <summary>Computes a pack's derived values tick by tick. Not thread-safe: one per game loop.</summary>
public sealed class DerivedEvaluator : IDisposable
{
    private readonly Engine _engine;
    private readonly List<(DerivedValue Value, Function Function)> _functions;
    private JsValue _previous = JsValue.Null;

    internal DerivedEvaluator(Engine engine, List<(DerivedValue Value, Function Function)> functions)
    {
        _engine = engine;
        _functions = functions;
    }

    /// <summary>
    /// Adds the derived values to <paramref name="values"/> (one tick's decoded variables). Each function gets the
    /// tick's values, including derived values declared before it, and the previous tick's values (null on the first).
    /// A value that throws, or returns something other than a finite number, boolean, string or null, is null for this tick.
    /// </summary>
    public IReadOnlyList<DerivedError> Evaluate(Dictionary<string, object?> values)
    {
        List<DerivedError>? errors = null;
        _engine.Constraints.Reset();
        var current = new JsObject(_engine);
        foreach (var (name, value) in values)
            current.Set(name, ToJs(value));

        foreach (var ((name, _), function) in _functions)
        {
            object? result;
            try
            {
                result = FromJs(function.Call(JsValue.Undefined, [current, _previous]));
            }
            catch (Exception ex) when (ex is JavaScriptException or JintException or TimeoutException or FormatException)
            {
                (errors ??= []).Add(new DerivedError(name, ex.Message));
                result = null;
            }
            values[name] = result;
            current.Set(name, ToJs(result));
        }

        _previous = current;
        return errors ?? (IReadOnlyList<DerivedError>)[];
    }

    /// <summary>Forgets the previous tick, so the next is treated as the first.</summary>
    public void Restart() => _previous = JsValue.Null;

    public void Dispose() => _engine.Dispose();

    private static JsValue ToJs(object? value) => value switch
    {
        null => JsValue.Null,
        long l => new JsNumber(l),
        double d => new JsNumber(d),
        bool b => b ? JsBoolean.True : JsBoolean.False,
        string s => new JsString(s),
        _ => throw new NotSupportedException($"Can't pass {value.GetType().Name} to a derived value"),
    };

    /// <summary>Whole numbers become <see cref="long"/>, like decoded variables; other numbers stay <see cref="double"/>.</summary>
    private static object? FromJs(JsValue value) => value switch
    {
        _ when value.IsNull() => null,
        _ when value.IsUndefined() => throw new FormatException("returned undefined (a misspelt variable name?); return null for no value"),
        JsBoolean b => b.AsBoolean(),
        JsString s => s.ToString(),
        JsNumber n when double.IsFinite(n.AsNumber()) => n.AsNumber() is var d && d == Math.Truncate(d) && Math.Abs(d) <= (1L << 53) ? (object)(long)d : d,
        _ => throw new FormatException($"must return a number, boolean, string or null, not {value}"),
    };
}
