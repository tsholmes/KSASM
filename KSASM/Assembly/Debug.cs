
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class DebugSymbols(ParseBuffer buffer)
  {
    private readonly ParseBuffer buffer = buffer;
    private readonly AddrList<InstAddr> insts = new();
    private readonly AddrList<LabelAddr> labels = new();
    private readonly AddrList<DataRecord> data = new();
    private readonly Dictionary<int, SourceLines> lines = [];
    private readonly char[] lineBuffer = new char[AppendBuffer.CHUNK_SIZE];

    public bool InstToken(int addr, out TokenIndex inst)
    {
      if (!insts.FindExact(addr, out var iaddr, out _))
      {
        inst = TokenIndex.Invalid;
        return false;
      }
      inst = iaddr.Inst;
      return true;
    }

    public TokenIndex RootToken(TokenIndex tokeni)
    {
      var token = buffer[tokeni];
      var root = token;
      while (true)
      {
        var source = buffer.Source(token.Source);
        if (source.Producer != TokenIndex.Invalid)
          token = buffer[source.Producer];
        else if (token.Previous != TokenIndex.Invalid)
          token = buffer[token.Previous];
        else
          break;
        if (token.Type != TokenType.Macro && buffer[token] != ".import")
          root = token;
      }
      return root.Index;
    }

    public ReadOnlySpan<char> SourceName(TokenIndex tokeni) => buffer.SourceName(buffer[tokeni].Source);

    public ReadOnlySpan<char> SourceLine(TokenIndex tok, out int lnum, out int loff)
    {
      var inst = buffer[tok];
      var slines = GetLines(inst.Source);

      if (slines.Source.Synthetic)
      {
        if (!slines.Lines.LastLE(inst.Index.Index, out var lrange, out lnum) || lrange.End <= inst.Index.Index)
        {
          lnum = loff = 0;
          return [];
        }

        var line = new LineBuilder(lineBuffer);

        var trem = tok.Index - lrange.Start;
        loff = 0;

        var prevType = TokenType.Invalid;
        var prevEnd = (char)0;
        for (var i = lrange.Start; i < lrange.End && line.Length < lineBuffer.Length; i++)
        {
          var token = buffer[new TokenIndex(i)];
          if (token.Type == TokenType.EOL)
            break;
          var tdata = buffer[token];
          if (line.Length > 0 && tdata.Length > 0 && Lexer.NeedsSpace(prevType, prevEnd, token.Type, tdata[0]))
            line.Sp();
          prevType = token.Type;
          prevEnd = tdata.Length > 0 ? tdata[^1] : default;
          if (line.Length + tdata.Length > lineBuffer.Length)
            tdata = tdata[..(lineBuffer.Length - line.Length)];
          line.Add(tdata);
          if (--trem == 0)
            loff = line.Length;
        }
        return line.Line;
      }
      else
      {
        if (!slines.Lines.LastLE(inst.Data.Start, out var line, out lnum))
        {
          lnum = loff = 0;
          return [];
        }

        loff = inst.Data.Start - line.Start;
        var ldata = buffer.Data(line);
        if (ldata.Length > 0 && ldata[^1] == '\n')
          ldata = ldata[..^1];
        return ldata;
      }
    }

    public AddrId ID(int addr)
    {
      if (!labels.LastLE(addr, out var label, out _))
        return new(addr, "", 0);

      return new(addr, label.Label, addr - label.Addr);
    }

    public void GetAddrInfo(int startAddr, Span<AddrInfo> infos, bool includeInst = true, bool includeData = true)
    {
      infos.Clear();
      var endAddr = startAddr + infos.Length;

      if (includeInst && insts.FirstGE(startAddr, out _, out int index))
      {
        while (index < insts.Length)
        {
          var inst = insts[index++];
          if (inst.Addr >= endAddr)
            break;
          infos[inst.Addr - startAddr].Inst = inst.Inst;
        }
      }
      if (labels.FirstGE(startAddr, out _, out index))
      {
        while (index < labels.Length)
        {
          var label = labels[index++];
          if (label.Addr >= endAddr)
            break;
          infos[label.Addr - startAddr].LabelIndex = index;
        }
      }
      if (includeData && data.FirstGE(startAddr, out _, out index))
      {
        while (index < data.Length)
        {
          var dat = data[index++];
          if (dat.Addr >= endAddr)
            break;
          infos[dat.Addr - startAddr].Type = dat.Type;
          infos[dat.Addr - startAddr].Width = dat.Width;
        }
      }
    }

    public int LabelCount => labels.Length;
    public LabelAddr Label(int index) => labels[index];

    public LabelAddr? FindLabel(ReadOnlySpan<char> label)
    {
      for (var i = 0; i < labels.Length; i++)
      {
        var l = labels[i];
        if (label.Equals(l.Label, StringComparison.Ordinal))
          return l;
      }
      return null;
    }

    public void AddInst(int addr, TokenIndex token) => insts.Add(new(addr, token));
    public void AddLabel(string label, int addr) => labels.Add(new(addr, label));
    public void AddData(int addr, DataType type, int width) => data.Add(new(addr, type, width));

    private SourceLines GetLines(SourceIndex source)
    {
      var srecord = buffer.Source(source);
      // token end is -1 until completed
      if (lines.TryGetValue(source.Index, out var existing) && existing.Source.Tokens.End != -1)
        return existing;
      var slines = existing.Lines ?? new();
      slines.Clear();

      if (srecord.Synthetic)
      {
        var range = srecord.Tokens;
        // if range unfinished, take all tokens to end
        if (range.End == -1)
          range = new(range.Start, buffer.TokenCount);

        var start = range.Start;
        var current = start;
        while (current < range.End)
        {
          var cend = (start + AppendBuffer.CHUNK_SIZE) & ~AppendBuffer.CHUNK_MASK;
          if (cend > range.End)
            cend = range.End;
          var tokens = buffer.TokenSpan(new(current, cend - current));
          for (var i = 0; i < tokens.Length; i++)
          {
            var lend = i + 1 + current;
            if (tokens[i].Type == TokenType.EOL || lend - start == AppendBuffer.CHUNK_SIZE)
            {
              slines.Add(new(start, lend - start));
              start = lend;
            }
          }
          current = cend;
        }
        slines.Add(new(start, range.End - start));
      }
      else
      {
        var range = srecord.Data;
        var start = range.Start;
        var current = start;
        while (current < range.End)
        {
          var cend = (current + AppendBuffer.CHUNK_SIZE) & ~AppendBuffer.CHUNK_MASK;
          if (cend > range.End)
            cend = range.End;
          var data = buffer.Data(new(current, cend - current));
          for (var i = 0; i < data.Length; i++)
          {
            var lend = i + 1 + current;
            if (data[i] == '\n' || lend - start == AppendBuffer.CHUNK_SIZE)
            {
              slines.Add(new(start, lend - start));
              start = lend;
            }
          }
          current = cend;
        }
        slines.Add(new(start, range.End - start));
      }

      return lines[source.Index] = new(srecord, slines);
    }

    private readonly struct InstAddr(int addr, TokenIndex inst) : IAddr
    {
      public readonly int Addr = addr;
      public readonly TokenIndex Inst = inst;
      int IAddr.Addr => Addr;
    }

    public readonly struct LabelAddr(int addr, string label) : IAddr
    {
      public readonly int Addr = addr;
      public readonly string Label = label;
      int IAddr.Addr => Addr;
    }

    public readonly struct DataRecord(int addr, DataType type, int width) : IAddr
    {
      public readonly int Addr = addr;
      public readonly DataType Type = type;
      public readonly int Width = width;
      int IAddr.Addr => Addr;
    }

    public readonly struct AddrId(int addr, string label, int offset)
    {
      public readonly int Addr = addr;
      public readonly string Label = label;
      public readonly int Offset = offset;
    }

    private readonly struct SourceLines(SourceRecord source, AddrList<FixedRange> lines)
    {
      public readonly SourceRecord Source = source;
      public readonly AddrList<FixedRange> Lines = lines;
    }

    public InstIterator InstIter() => new(this);

    public struct InstIterator(DebugSymbols debug)
    {
      private readonly DebugSymbols debug = debug;
      private int labelIndex = 0;
      private int instIndex = 0;

      public bool Next(out int addr, out string label)
      {
        if (debug == null || instIndex >= debug.insts.Length)
        {
          addr = default;
          label = null;
          return false;
        }

        addr = debug.insts[instIndex++].Addr;
        while (labelIndex < debug.labels.Length && debug.labels[labelIndex].Addr < addr)
          labelIndex++;

        if (labelIndex < debug.labels.Length && debug.labels[labelIndex].Addr == addr)
          label = debug.labels[labelIndex].Label;
        else
          label = null;

        return true;
      }
    }
  }

  public struct AddrInfo
  {
    public TokenIndex? Inst;
    public DataType? Type;
    public int? Width;
    public int? LabelIndex;
  }

  public class AddrList<T> where T : struct, IAddr
  {
    private T[] data = new T[10];
    private int length = 0;
    private bool sorted = true;

    public int Length => length;

    public T this[int index]
    {
      get
      {
        if (index < 0 || index >= length)
          throw new IndexOutOfRangeException();
        SortIfNeeded();
        return data[index];
      }
    }

    public void Clear() => length = 0;

    public void Add(T el)
    {
      if (length == data.Length)
      {
        var oldData = data;
        data = new T[length * 2];
        oldData.CopyTo(data);
      }
      data[length++] = el;
      sorted = length < 2;
    }

    public bool FindExact(int addr, out T res, out int idx)
    {
      idx = SearchAddr(addr);
      if (idx < 0)
      {
        res = default;
        return false;
      }
      res = data[idx];
      return true;
    }

    public bool LastLE(int addr, out T res, out int idx)
    {
      idx = SearchAddr(addr + 1);
      if (idx < 0)
        idx = ~idx;
      if (idx >= length)
        idx = length - 1;
      while (idx >= 0 && data[idx].Addr > addr)
        idx--;
      if (idx < 0)
      {
        res = default;
        return false;
      }
      res = data[idx];
      return true;
    }

    public bool FirstGE(int addr, out T res, out int idx)
    {
      idx = SearchAddr(addr - 1);
      if (idx < 0)
        idx = ~idx;
      while (idx < length && data[idx].Addr < addr)
        idx++;
      if (idx >= length)
      {
        res = default;
        return false;
      }
      res = data[idx];
      return true;
    }

    private int SearchAddr(int addr)
    {
      if (length == 0)
        return -1;
      SortIfNeeded();
      var ds = data.AsSpan(..length);
      return ds.BinarySearch(new AddrComp(addr));
    }

    private void SortIfNeeded()
    {
      if (length < 2 || sorted)
        return;
      var ds = data.AsSpan(..length);
      ds.SortStable(Comparer.Instance);
      sorted = true;
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(AddrList<T> list)
    {
      private readonly AddrList<T> list = list;
      private int index = -1;

      public T Current => list.data[index];
      public bool MoveNext() => ++index < list.length;
    }

    private readonly struct AddrComp(int addr) : IComparable<T>
    {
      public readonly int Addr = addr;
      public int CompareTo(T other) => Addr.CompareTo(other.Addr);
    }

    private class Comparer : Comparer<T>
    {
      public static readonly Comparer Instance = new();
      public override int Compare(T x, T y) => x.Addr.CompareTo(y.Addr);
    }
  }
}