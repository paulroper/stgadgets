using System.Runtime.CompilerServices;
using Stgadgets.Core.Memory;
using Stgadgets.Core.Providers;

namespace Stgadgets.Cli.Tests;

/// <summary>
/// Plays <see cref="Memory"/> from 0x100, one array per tick, then ends the stream as if the game had changed. Like
/// a real provider it refuses to stream without its settings, and it calls <see cref="OnTick"/> before each
/// snapshot.
/// </summary>
internal sealed class FakeEmulator : IEmulatorProvider
{
    public byte[][] Memory { get; set; } = [];

    public Action<int>? OnTick { get; set; }

    /// <summary>The endpoint it was connected to, if one was given.</summary>
    public string? Endpoint { get; private set; }

    public FakeEmulator Connect(string? endpoint)
    {
        Endpoint = endpoint;
        return this;
    }

    public string Id => "fake";

    public EmulatorProcessInfo? Process => null;

    public Task<ContentInfo?> GetContentAsync(CancellationToken cancellationToken) =>
        Task.FromResult<ContentInfo?>(new ContentInfo("test", "fake_core"));

    public async IAsyncEnumerable<Snapshot> StreamAsync(
        ContentInfo content, ReadPlan plan, ProviderSettings? settings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (settings is null)
            throw new NotSupportedException($"no settings for {content.System}");

        for (var tick = 0; tick < Memory.Length; tick++)
        {
            await Task.Yield();
            var memory = new MemoryBlock(new AddressRange(0x100, Memory[tick].Length), Memory[tick]);
            var blocks = plan.Ranges.Select(r => new MemoryBlock(r, memory.Data.Slice((int)(r.Start - 0x100), r.Length))).ToList();
            OnTick?.Invoke(tick);   // before the snapshot arrives, as when someone marks during a frame
            yield return new Snapshot(null, TimeSpan.FromMilliseconds(16.7 * tick), blocks);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
