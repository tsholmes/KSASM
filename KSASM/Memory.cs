
using System;
using System.Collections.Generic;

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
    private struct MemRange
    {
      public int Addr;
      public IMemory Memory;
      public int MemAddr;
      public int Length;
    }

    private readonly IMemory main;
    private readonly List<MemRange> ranges = [];

    public MappedMemory(IMemory main, int mainSize)
    {
      this.main = main;

      ranges.Add(new() { Addr = 0, Memory = main, MemAddr = 0, Length = mainSize });
    }

    public void MapRange(int addr, IMemory mem, int memAddr, int len)
    {
      var newRanges = new List<MemRange>();
      var newRange = new MemRange { Addr = addr, Memory = mem, MemAddr = memAddr, Length = len };
      var newEnd = addr + len;

      foreach (var range in ranges)
      {
        var end = range.Addr + range.Length;
        if (range.Addr >= newEnd || end <= addr)
        {
          // no overlap
          newRanges.Add(range);
          continue;
        }
        if (addr > range.Addr)
        {
          // take start chunk
          newRanges.Add(range with { Length = addr - range.Addr });
        }
        newRanges.Add(newRange);
        if (newEnd < end)
        {
          // take end chunk
          var endLen = end - newEnd;
          var endRem = range.Length - endLen;
          newRanges.Add(range with { Addr = newEnd, MemAddr = range.MemAddr + endRem, Length = endLen });
        }
      }
      SetRanges(newRanges);
    }

    public void UnmapRange(int addr, int len) => MapRange(addr, main, addr, len);

    private void SetRanges(List<MemRange> newRanges)
    {
      ranges.Clear();
      var prev = newRanges[0];
      ranges.Add(prev);
      for (var i = 1; i < ranges.Count; i++)
      {
        var range = newRanges[i];
        if (range.Memory == prev.Memory &&
            range.Addr == prev.Addr + prev.Length &&
            range.MemAddr == prev.MemAddr + prev.Length)
        {
          prev.Length += range.Length;
          ranges[^1] = prev;
        }
        else
        {
          prev = range;
          ranges.Add(range);
        }
      }
    }

    public void Read(Span<byte> buffer, int address)
    {
      for (var idx = FindStartIndex(address); idx < ranges.Count && buffer.Length > 0; idx++)
      {
        var range = ranges[idx];
        var offset = address - range.Addr;
        var len = buffer.Length;
        if (offset + len > range.Length)
          len = range.Length - offset;
        range.Memory.Read(buffer[..len], range.MemAddr + offset);
        address += len;
        buffer = buffer[len..];
      }
    }

    public void Write(Span<byte> data, int address)
    {
      for (var idx = FindStartIndex(address); idx < ranges.Count && data.Length > 0; idx++)
      {
        var range = ranges[idx];
        var offset = address - range.Addr;
        var len = data.Length;
        if (offset + len > range.Length)
          len = range.Length - offset;
        range.Memory.Write(data[..len], range.MemAddr + offset);
        address += len;
        data = data[len..];
      }
    }

    private int FindStartIndex(int address)
    {
      var lo = 0;
      var hi = ranges.Count;
      while (lo + 1 < hi)
      {
        var mid = (lo + hi) / 2;
        var range = ranges[mid];
        if (address < range.Addr)
          lo = mid + 1;
        else if (address >= range.Addr + range.Length)
          hi = mid - 1;
        else
          return mid;
      }
      return lo;
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
        Console.WriteLine($"READ {ptr.Address} {ptr.Type}*{ptr.Width}");
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
        Console.WriteLine($"READ {addr} {type}");
      ReadToBuf(addr, type.SizeBytes());
      return DecodeAt(0, type);
    }

    public void Write(ValuePointer ptr, ValArray vals)
    {
      if (DebugWrite)
        Console.WriteLine($"WRITE {ptr.Address} {ptr.Type}*{ptr.Width}");
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
        Console.WriteLine($"WRITE {addr} {type}");
      EncodeAt(0, type, value);
      WriteFromBuf(addr, type.SizeBytes());
    }

    private Value DecodeAt(int index, DataType type)
    {
      var val = default(Value);
      switch (type)
      {
        case DataType.U8:
          val.Unsigned = Encoding.U8.Decode(From(index));
          break;
        case DataType.I16:
          val.Signed = Encoding.I16.Decode(From(index));
          break;
        case DataType.I32:
          val.Signed = Encoding.I32.Decode(From(index));
          break;
        case DataType.I64:
          val.Signed = Encoding.I64.Decode(From(index));
          break;
        case DataType.U64:
          val.Unsigned = Encoding.U64.Decode(From(index));
          break;
        case DataType.F64:
          val.Float = Encoding.F64.Decode(From(index));
          break;
        case DataType.P24:
          val.Unsigned = Encoding.P24.Decode(From(index));
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      if (DebugRead)
        Console.WriteLine($"  {index} {type} = {val.As(type)}");
      return val;
    }

    private void EncodeAt(int index, DataType type, Value val)
    {
      switch (type)
      {
        case DataType.U8:
          Encoding.U8.Encode(val.Unsigned, From(index));
          break;
        case DataType.I16:
          Encoding.I16.Encode(val.Signed, From(index));
          break;
        case DataType.I32:
          Encoding.I32.Encode(val.Signed, From(index));
          break;
        case DataType.I64:
          Encoding.I64.Encode(val.Signed, From(index));
          break;
        case DataType.U64:
          Encoding.U64.Encode(val.Unsigned, From(index));
          break;
        case DataType.F64:
          Encoding.F64.Encode(val.Float, From(index));
          break;
        case DataType.P24:
          Encoding.P24.Encode(val.Unsigned, From(index));
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      if (DebugWrite)
        Console.WriteLine($"  {index} {type} = {val.As(type)}");
    }
  }
}