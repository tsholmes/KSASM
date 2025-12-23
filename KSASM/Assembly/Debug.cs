
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class DebugSymbols(ParseBuffer buffer, AppendBuffer<Token> tokens)
  {
    private readonly ParseBuffer buffer = buffer;
    private readonly AppendBuffer<Token> tokens = tokens;
    private AppendBuffer<Token> finalTokens;
    private readonly AddrList<InstAddr> insts = new();
    private readonly AddrList<LabelAddr> labels = new();
    private readonly AddrList<DataRecord> data = new();
    private readonly Dictionary<int, SourceLines> lines = [];
    private readonly char[] lineBuffer = new char[AppendBuffer.CHUNK_SIZE];

    private SourceLines? finalLines;

    public ParseBuffer Buffer => buffer;
    public AppendBuffer<Token>.Enumerator FinalTokens => GetFinalTokens().GetEnumerator();
    public int FinalCount => tokens.Length;

    public Token Token(TokenIndex index)
    {
      if (index.Index < 0)
        return finalTokens[~index.Index];
      return buffer[index];
    }

    public TokenIndex GetProducer(Token token)
    {
      if (token.Source.Index < 0)
        return token.Previous;
      var source = buffer.Source(token.Source);
      if (source.Producer == TokenIndex.Invalid)
        return source.Producer;
      var producer = buffer[source.Producer];
      if (token.Previous != TokenIndex.Invalid)
      {
        var prev = buffer[token.Previous];
        if (prev.Source.Index == producer.Source.Index)
          return prev.Index;
      }
      return producer.Index;
    }

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

    public SourceLineIterator SourceLineIter(SourceIndex index) => new(this, index);

    public ReadOnlySpan<char> SourceLine(TokenIndex tok, out int lnum, out int loff)
    {
      Token inst;
      SourceLines slines;
      int index;
      var final = tok.Index < 0;
      if (final)
      {
        index = ~tok.Index;
        inst = finalTokens[index];
        slines = GetFinalLines();
      }
      else
      {
        index = tok.Index;
        inst = buffer[tok];
        slines = GetLines(inst.Source);
      }

      if (slines.Source.Synthetic)
      {
        if (!slines.Lines.LastLE(index, out var lrange, out lnum) || lrange.End <= index)
        {
          lnum = loff = 0;
          return [];
        }

        loff = 0;

        var line = new LineBuilder(lineBuffer);
        var tline = new TokenLineBuilder();

        for (var i = lrange.Start; i < lrange.End && line.Length < lineBuffer.Length; i++)
        {
          var token = final ? finalTokens[i] : buffer[new TokenIndex(i)];
          var tdata = buffer[token];
          var trange = tline.Add(token.Type, tdata, out var sp);
          if (trange.End > lineBuffer.Length)
            trange = new(trange.Start, lineBuffer.Length - trange.Start);
          if (i == tok.Index)
            loff = trange.Start;
          if (sp)
            line.Sp();
          line.Add(tdata[..trange.Length]);
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

        var lineStart = range.Start;
        var chunkStart = lineStart;
        var lastLen = -1;
        while (chunkStart < range.End)
        {
          var cend = (chunkStart + AppendBuffer.CHUNK_SIZE) & ~AppendBuffer.CHUNK_MASK;
          if (cend > range.End)
            cend = range.End;
          var tokens = buffer.TokenSpan(new(chunkStart, cend - chunkStart));
          for (var i = 0; i < tokens.Length; i++)
          {
            var lend = i + 1 + chunkStart;
            if (tokens[i].Type == TokenType.EOL || lend - lineStart == AppendBuffer.CHUNK_SIZE)
            {
              var end = tokens[i].Type == TokenType.EOL ? lend - 1 : lend;
              if (end - lineStart > 0 || lastLen > 0) // skip multiple blank lines
                slines.Add(new(lineStart, end - lineStart));
              lastLen = end - lineStart;
              lineStart = lend;
            }
          }
          chunkStart = cend;
        }
        slines.Add(new(lineStart, range.End - lineStart));
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
              var end = lend;
              while (end > current && end > start && data[end - 1 - current] is '\n' or '\r') end--;
              slines.Add(new(start, end - start));
              start = lend;
            }
          }
          current = cend;
        }
        slines.Add(new(start, range.End - start));
      }

      return lines[source.Index] = new(srecord, slines);
    }

    private AppendBuffer<Token> GetFinalTokens()
    {
      if (finalTokens != null)
        return finalTokens;
      var final = new AppendBuffer<Token>();
      for (var i = 0; i < tokens.Length; i++)
      {
        var tok = tokens[i];
        final.Add(new(new(-1), new(~i), tok.Type, tok.Data, tok.Index));
      }
      return finalTokens = final;
    }

    private SourceLines GetFinalLines()
    {
      if (finalLines != null)
        return finalLines.Value;
      GetFinalTokens();

      var lines = new AddrList<FixedRange>();
      var start = 0;
      var lastLen = -1;
      for (var i = 0; i < finalTokens.Length; i++)
      {
        var tok = finalTokens[i];
        if (tok.Type == TokenType.EOL)
        {
          if (i != start || lastLen > 0) // skip multiple empty lines in a row
            lines.Add(new(start, i - start));
          lastLen = i - start;
          start = i + 1;
        }
      }
      if (start < finalTokens.Length)
        lines.Add(new(start, finalTokens.Length - start));

      finalLines = new(
        new("output", FixedRange.Invalid, FixedRange.Invalid, true, TokenIndex.Invalid),
        lines);

      return finalLines.Value;
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

    public struct SourceLineIterator(DebugSymbols debug, SourceIndex sindex)
    {
      private readonly DebugSymbols debug = debug;
      private readonly SourceIndex sindex = sindex;
      private readonly SourceLines slines =
        sindex.Index < 0 ? debug.GetFinalLines() : debug.GetLines(sindex);

      private int lindex = -1;
      private int lastTokStart = 0;
      private int nextTokStart = sindex.Index < 0 ? 0 : debug.buffer.Source(sindex).Tokens.Start;

      public bool Next(out int lnum)
      {
        lnum = ++lindex;
        if (debug == null)
          return false;
        if (lnum >= slines.Lines.Length)
          return false;
        lastTokStart = nextTokStart;
        return true;
      }

      public SourceLineTokenIterator Tokens() =>
        new(debug, sindex, slines.Source, slines.Lines[lindex], lastTokStart);

      public void Build(ref LineBuilder line)
      {
        if (slines.Source.Synthetic)
        {
          var iter = Tokens();
          var lastEnd = 0;
          while (iter.Next(out var token, out var range) && token.Type != TokenType.EOL)
          {
            if (range.Start > lastEnd && slines.Source.Synthetic)
              line.Sp();
            line.Add(debug.buffer[token]);
            lastEnd = range.End;

            nextTokStart = token.Index.Index + 1;
          }
        }
        else
          line.Add(debug.buffer.Data(slines.Lines[lindex]));
      }
    }

    public struct SourceLineTokenIterator(
      DebugSymbols debug, SourceIndex sindex, SourceRecord source, FixedRange range, int tokenStart = -1)
    {
      private readonly DebugSymbols debug = debug;
      private readonly SourceRecord source = source;
      private readonly SourceIndex sindex = sindex;
      private readonly FixedRange range = range;
      private readonly int tokenStart = tokenStart;
      private TokenLineBuilder line = new();

      private int tindex = -1;

      public bool Next(out Token token, out FixedRange lrange) =>
        sindex.Index < 0
        ? NextFinal(out token, out lrange)
        : source.Synthetic
          ? NextSynth(out token, out lrange)
          : NextRaw(out token, out lrange);

      private bool NextFinal(out Token token, out FixedRange lrange)
      {
        if (++tindex >= range.Length)
        {
          token = default;
          lrange = default;
          return false;
        }
        token = debug.finalTokens[range.Start + tindex];
        lrange = line.Add(token.Type, debug.buffer[token], out _);
        return true;
      }

      private bool NextSynth(out Token token, out FixedRange lrange)
      {
        if (++tindex >= range.Length)
        {
          token = default;
          lrange = default;
          return false;
        }
        token = debug.buffer[new TokenIndex(tindex + range.Start)];
        lrange = line.Add(token.Type, debug.buffer[token], out _);
        return true;
      }

      private bool NextRaw(out Token token, out FixedRange lrange)
      {
        while ((++tindex + tokenStart) < source.Tokens.End)
        {
          token = debug.buffer[new TokenIndex(tindex + tokenStart)];
          if (token.Data.Start >= range.Start & token.Data.End <= range.End)
          {
            lrange = token.Data - range.Start;
            return true;
          }
        }
        token = default;
        lrange = default;
        return false;
      }
    }

    private struct TokenLineBuilder()
    {
      private int length = 0;
      private TokenType lastType = TokenType.Invalid;
      private char lastEnd = default;

      public FixedRange Add(TokenType type, ReadOnlySpan<char> data, out bool sp)
      {
        sp = length > 0 && data.Length > 0 && Lexer.NeedsSpace(lastType, lastEnd, type, data[0]);
        lastType = type;
        lastEnd = data.Length > 0 ? data[^1] : default;
        var range = new FixedRange(length, data.Length);
        if (sp) range += 1;
        length = range.End;
        return range;
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

  public class TypeMemory
  {
    private const int CHUNK_SHIFT = 12;
    private const int CHUNK_SIZE = 1 << CHUNK_SHIFT;
    private const int CHUNK_MASK = CHUNK_SIZE - 1;
    private const int CHUNK_COUNT = Processor.MAIN_MEM_SIZE >> CHUNK_SHIFT;

    private static byte Encode(DataType type, int offset) => (byte)((int)type | ((offset & 0xF) << 4));
    private static (DataType, int) Decode(byte encoded) => ((DataType)(encoded & 0xF), (encoded >> 4) & 0xF);

    private readonly byte[][] chunks = new byte[CHUNK_COUNT][];

    public void Write(int addr, DataType type, int width)
    {
      var sz = type.SizeBytes();
      for (var w = 0; w < width; w++)
        for (var off = 0; off < sz; off++)
          this[addr++] = Encode(type, off);
    }

    public DataType? Read(int addr)
    {
      var (type, offset) = Decode(this[addr]);
      if (offset != 0)
        return null;

      var sz = type.SizeBytes();
      if (addr + sz > Processor.MAIN_MEM_SIZE)
        return null;
      for (var i = 1; i < sz; i++)
      {
        var (itype, ioff) = Decode(this[addr + i]);
        if (itype != type || ioff != i)
          return null;
      }

      return type;
    }

    private byte this[int addr]
    {
      get
      {
        var chunki = addr >> CHUNK_SHIFT;
        if (chunki < 0 || chunki >= chunks.Length)
          throw new InvalidOperationException($"{addr} {chunki}");
        var chunk = chunks[chunki];
        if (chunk == null)
          return 0;
        return chunk[addr & CHUNK_MASK];
      }
      set
      {
        var chunki = addr >> CHUNK_SHIFT;
        var chunk = chunks[chunki];
        if (chunk == null)
          chunk = chunks[chunki] = new byte[CHUNK_SIZE];
        chunk[addr & CHUNK_MASK] = value;
      }
    }
  }
}