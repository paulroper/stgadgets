namespace Stgadgets.Core.Memory;

/// <summary>Bytes read from one address range.</summary>
public sealed record MemoryBlock(AddressRange Range, ReadOnlyMemory<byte> Data);

/// <summary>Memory read from the emulator at one point in time, addressed in the original hardware's address space.</summary>
/// <param name="EmulatedFrame">The emulator's frame number, when the provider is frame-synced.</param>
/// <param name="Timestamp">Time since the stream started.</param>
public sealed record Snapshot(long? EmulatedFrame, TimeSpan Timestamp, IReadOnlyList<MemoryBlock> Blocks)
{
    public ReadOnlySpan<byte> Read(uint address, int length)
    {
        foreach (var block in Blocks)
        {
            if (block.Range.Contains(address, length))
                return block.Data.Span.Slice((int)(address - block.Range.Start), length);
        }
        throw new ArgumentOutOfRangeException(nameof(address), $"0x{address:X} (+{length}) isn't in this snapshot");
    }
}
