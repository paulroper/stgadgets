namespace Stgadgets.Core.Memory;

/// <summary>A contiguous range in the emulated system's address space.</summary>
public readonly record struct AddressRange(uint Start, int Length)
{
    /// <summary>First address after the range.</summary>
    public uint End => Start + (uint)Length;

    public bool Contains(uint address, int length) => address >= Start && address + (uint)length <= End;

    public override string ToString() => $"0x{Start:X}..0x{End - 1:X}";
}
