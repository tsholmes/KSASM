
using System;

namespace KSASM
{
  public interface IMemory
  {
    public void Read(Span<byte> buffer, int address);
    public void Write(Span<byte> data, int address);
  }

  public class ByteArrayMemory : IMemory
  {
    private readonly byte[] mem;

    public ByteArrayMemory(int size)
    {
      mem = new byte[size];
    }

    public void Read(Span<byte> buffer, int address)
    {
      if (address >= mem.Length)
        return;
      var end = address + buffer.Length;
      if (end > mem.Length) end = mem.Length;
      mem.AsSpan()[address..end].CopyTo(buffer);
    }

    public void Write(Span<byte> data, int address)
    {
      if (address >= mem.Length)
        return;
      var end = data.Length;
      if (address + end > mem.Length)
        end = mem.Length - address;
      data[..end].CopyTo(mem.AsSpan()[address..]);
    }
  }

  public class MappedMemory : IMemory
  {
    private struct MemRange : IRange<MemRange>
    {
      public int Offset { get; set; }
      public int Length { get; set; }

      public IMemory Memory;
      public int MemAddr;


      public MemRange Slice(int offset, int length) => new()
      {
        Offset = offset,
        Length = length,
        Memory = Memory,
        MemAddr = MemAddr + offset - Offset,
      };

      public bool TryMerge(MemRange next, out MemRange merged)
      {
        if (next.Offset != Offset + Length || next.Memory != Memory || next.MemAddr != MemAddr + Length)
        {
          merged = default;
          return false;
        }
        merged = this with { Length = Length + next.Length };
        return true;
      }
    }

    private readonly RangeList<MemRange> ranges = new();

    public MappedMemory() { }

    public void MapRange(int addr, IMemory mem, int memAddr, int len) =>
      ranges.AddRange(new()
      {
        Offset = addr,
        Length = len,
        Memory = mem,
        MemAddr = memAddr,
      });

    public void Read(Span<byte> buffer, int address)
    {
      var iter = ranges.Overlap(new SpanRange<byte>
      {
        Span = buffer,
        Offset = address,
      });

      while (iter.Next(out var mrange, out var brange))
      {
        mrange.Memory.Read(brange.Span, mrange.MemAddr);
      }
    }

    public void Write(Span<byte> data, int address)
    {
      var iter = ranges.Overlap(new SpanRange<byte>
      {
        Span = data,
        Offset = address,
      });

      while (iter.Next(out var mrange, out var brange))
      {
        mrange.Memory.Write(brange.Span, mrange.MemAddr);
      }
    }
  }

  public class MemoryAccessor
  {
    public static bool DebugRead = false;
    public static bool DebugWrite = false;

    private readonly IMemory memory;
    private readonly byte[] buffer = new byte[16 * 8];

    public MemoryAccessor(IMemory memory) => this.memory = memory;

    private Span<byte> To(int len) => buffer.AsSpan()[..len];
    private Span<byte> From(int index) => buffer.AsSpan()[index..];

    private void ReadToBuf(int addr, int len) => memory.Read(To(len), addr);
    private void WriteFromBuf(int addr, int len) => memory.Write(To(len), addr);

    public void Read(ValuePointer ptr, ValArray vals)
    {
      if (DebugRead)
        Console.WriteLine($"READ {ptr.Address:X6} {ptr.Type}*{ptr.Width}");
      var elSize = ptr.Type.SizeBytes();
      ReadToBuf(ptr.Address, elSize * ptr.Width);

      vals.Width = ptr.Width;
      vals.Mode = ptr.Type.VMode();

      for (var i = 0; i < ptr.Width; i++)
        vals.Values[i] = DecodeAt(i * elSize, ptr.Type);
    }

    public Value Read(int addr, DataType type)
    {
      if (DebugRead)
        Console.WriteLine($"READ {addr:X6} {type}");
      ReadToBuf(addr, type.SizeBytes());
      return DecodeAt(0, type);
    }

    public void Write(ValuePointer ptr, ValArray vals)
    {
      if (DebugWrite)
        Console.WriteLine($"WRITE {ptr.Address:X6} {ptr.Type}*{ptr.Width}");
      var elSize = ptr.Type.SizeBytes();

      if (ptr.Width != vals.Width)
        throw new InvalidOperationException($"Mismatched data width {ptr.Width} != {vals.Width}");

      vals.Convert(ptr.Type.VMode());

      for (var i = 0; i < ptr.Width; i++)
        EncodeAt(i * elSize, ptr.Type, vals.Values[i]);

      WriteFromBuf(ptr.Address, elSize * ptr.Width);
    }

    public void Write(int addr, DataType type, Value value)
    {
      if (DebugWrite)
        Console.WriteLine($"WRITE {addr:X6} {type}");
      EncodeAt(0, type, value);
      WriteFromBuf(addr, type.SizeBytes());
    }

    private Value DecodeAt(int index, DataType type)
    {
      var val = Encoding.Decode(From(index), type);
      if (DebugRead)
        Console.WriteLine($"  {index} {type} = {val.As(type)}");
      return val;
    }

    private void EncodeAt(int index, DataType type, Value val)
    {
      Encoding.Encode(From(index), type, val);
      if (DebugWrite)
        Console.WriteLine($"  {index} {type} = {val.As(type)}");
    }
  }
}