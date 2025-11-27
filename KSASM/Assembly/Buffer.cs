
using System;

namespace KSASM.Assembly
{
  public class ParseBuffer
  {
    private readonly AppendBuffer<char> sourceData = new();
    private readonly AppendBuffer<SourceRecord> sources = new();
    private readonly AppendBuffer<SourceToken> sourceTokens = new();
    private readonly AppendBuffer<SynthToken> synthTokens = new();

    public RawSourceBuilder NewRawSource(string name, ReadOnlySpan<char> data, TokenIndex producer)
    {
      var start = sourceData.AddRange(data);
      return new(this, name, new(start, data.Length), producer);
    }

    public SyntheticSourceBuilder NewSynthSource(string name, TokenIndex producer) => new(this, name, producer);

    private void AddSource(SourceIndex source, string name, FixedRange data, FixedRange tokens, bool synthetic)
    {
      if (sources[source.Index].Name != name)
        throw new InvalidOperationException();
      sources[source.Index] = new(name, data, tokens, synthetic);
    }

    private SourceIndex ReserveSource(string name, bool synthetic, FixedRange dataRange)
    {
      sources.Add(new SourceRecord(
        name,
        dataRange,
        new(synthetic ? synthTokens.Length : sourceTokens.Length, -1),
        synthetic));
      return new(sources.Length - 1);
    }

    private int AddSourceToken(SourceIndex source, TokenType type, FixedRange range, TokenIndex producer) =>
      sourceTokens.Add(new SourceToken(source, type, range, producer));

    private int AddSynthToken(
      SourceIndex source, TokenType type, FixedRange data, TokenIndex previous, TokenIndex producer
    ) => synthTokens.Add(new SynthToken(source, type, data, previous, producer));

    public Token this[TokenIndex idx]
    {
      get
      {
        if (idx.Synthetic)
        {
          ref var tok = ref synthTokens[idx.Index];
          return new(tok.Type, tok.Data, idx, tok.Source, tok.Previous, tok.Producer);
        }
        else
        {
          ref var tok = ref sourceTokens[idx.Index];
          return new(tok.Type, tok.Data, idx, tok.Source, TokenIndex.Invalid, tok.Producer);
        }
      }
    }

    public ReadOnlySpan<char> this[Token token] => Data(token.Data);

    public int SynthTokenCount => synthTokens.Length;

    public ReadOnlySpan<char> SourceName(SourceIndex source) => sources[source.Index].Name;

    public TokenEnumerable SourceTokens(SourceIndex src)
    {
      var source = sources[src.Index];
      return TokenRange(source.Tokens, source.Synthetic);
    }

    public TokenEnumerable TokenRange(FixedRange range, bool synthetic) => new(this, range, synthetic);

    public ReadOnlySpan<SynthToken> SynthTokens(FixedRange range) => synthTokens[range];

    public TokenReader SourceReader(SourceIndex source) => new(this, source);

    public ReadOnlySpan<char> Data(FixedRange range) => sourceData[range];

    public SourceRecord Source(SourceIndex source) => sources[source.Index];

    // TODO: maybe merge SourceToken/SynthToken and get rid of synthetic flags
    private readonly struct SourceToken(SourceIndex source, TokenType type, FixedRange data, TokenIndex producer)
    {
      public readonly SourceIndex Source = source;
      public readonly TokenType Type = type;
      public readonly FixedRange Data = data;
      public readonly TokenIndex Producer = producer;
    }
    public readonly struct SynthToken(
      SourceIndex source, TokenType type, FixedRange data, TokenIndex previous, TokenIndex producer)
    {
      public readonly SourceIndex Source = source;
      public readonly TokenType Type = type;
      public readonly FixedRange Data = data;
      public readonly TokenIndex Previous = previous;
      public readonly TokenIndex Producer = producer;
    }

    public struct TokenEnumerable(ParseBuffer buf, FixedRange range, bool synthetic)
    {
      public TokenEnumerator GetEnumerator() => new(buf, range, synthetic);
    }

    public ref struct TokenEnumerator(ParseBuffer buf, FixedRange range, bool synthetic)
    {
      private int index = -1;

      public Token Current => buf[new TokenIndex(range.Start + index, synthetic)];

      public bool MoveNext() => ++index < range.Length;
    }

    public struct TokenReader(ParseBuffer buf, SourceIndex source)
    {
      private readonly bool synthetic = buf.sources[source.Index].Synthetic;
      private readonly FixedRange tokens = buf.sources[source.Index].Tokens;
      private int index = 0;

      public bool EOF => index >= tokens.Length;

      public bool Peek(out Token token)
      {
        if (index == tokens.Length)
        {
          token = default;
          return false;
        }
        token = buf[new TokenIndex(tokens.Start + index, synthetic)];
        return true;
      }

      public bool Take(out Token token)
      {
        if (!Peek(out token))
          return false;
        index++;
        return true;
      }

      public bool PeekType(TokenType type, out Token token) => Peek(out token) && token.Type == type;

      public bool TakeType(TokenType type, out Token token)
      {
        if (!Peek(out token) || token.Type != type)
          return false;
        index++;
        return true;
      }
    }

    public readonly struct RawSourceBuilder(
      ParseBuffer buf, string name, FixedRange dataRange, TokenIndex producer) : IDisposable
    {
      public readonly SourceIndex Source = buf.ReserveSource(name, false, dataRange);
      private readonly int tokenStart = buf.sourceTokens.Length;

      public TokenIndex AddToken(TokenType type, FixedRange range)
      {
        range += dataRange.Start;
        if (range.End > dataRange.End)
          throw new IndexOutOfRangeException();
        var idx = buf.AddSourceToken(Source, type, range, producer);
        return new(idx, false);
      }

      public void Dispose() =>
        buf.AddSource(Source, name, dataRange, new(tokenStart, buf.sourceTokens.Length - tokenStart), false);
    }

    public readonly struct SyntheticSourceBuilder(ParseBuffer buf, string name, TokenIndex producer) : IDisposable
    {
      public readonly SourceIndex Source = buf.ReserveSource(name, true, new(buf.sourceData.Length, -1));
      private readonly int dataStart = buf.sourceData.Length;
      private readonly int tokenStart = buf.synthTokens.Length;

      public TokenIndex FirstToken => new(tokenStart, true);
      public TokenIndex NextToken => new(buf.synthTokens.Length, true);
      public TokenIndex LastToken => new(buf.synthTokens.Length - 1, true);

      public TokenIndex CopyToken(TokenIndex from)
      {
        var tok = buf[from];
        var idx = buf.AddSynthToken(Source, tok.Type, tok.Data, from, producer);
        return new(idx, true);
      }

      public SyntheticTokenBuilder MakeToken(TokenType type, TokenIndex from) =>
        new(buf, Source, type, from, producer);

      public void Dispose()
      {
        var data = new FixedRange(dataStart, buf.sourceData.Length - dataStart);
        var tokens = new FixedRange(tokenStart, buf.synthTokens.Length - tokenStart);
        buf.AddSource(Source, name, data, tokens, true);
      }
    }

    public readonly struct SyntheticTokenBuilder(
      ParseBuffer buf, SourceIndex source, TokenType type, TokenIndex from, TokenIndex producer) : IDisposable
    {
      private readonly int dataStart = buf.sourceData.Length;

      public void AddData(params ReadOnlySpan<char> data) => buf.sourceData.AddRange(data);
      public void AddData(char c) => buf.sourceData.Add(c);

      public void AddValue(Value val, ValueMode mode)
      {
        Span<char> data = stackalloc char[32];
        if (!Values.TryFormat(val, mode, data, out var length))
          throw new InvalidOperationException($"{val.Get(mode)}");
        AddData(data[..length]);
      }

      public void AddInt(int val)
      {
        Span<char> data = stackalloc char[32];
        if (!val.TryFormat(data, out int length))
          throw new InvalidOperationException($"{val}");
        AddData(data[..length]);
      }

      public void Dispose() =>
        buf.AddSynthToken(source, type, new(dataStart, buf.sourceData.Length - dataStart), from, producer);
    }
  }

  public readonly struct Token(
    TokenType type, FixedRange data, TokenIndex index,
    SourceIndex source, TokenIndex previous, TokenIndex producer)
  {
    public readonly TokenType Type = type;
    public readonly FixedRange Data = data;
    public readonly TokenIndex Index = index;
    public readonly SourceIndex Source = source;
    public readonly TokenIndex Previous = previous;
    public readonly TokenIndex Producer = producer;
  }

  public readonly struct TokenIndex(int index, bool synthetic)
  {
    public static readonly TokenIndex Invalid = new(-1, false);

    public readonly int Index = index;
    public readonly bool Synthetic = synthetic;

    public static bool operator ==(TokenIndex left, TokenIndex right) =>
      left.Index == right.Index && left.Synthetic == right.Synthetic;
    public static bool operator !=(TokenIndex left, TokenIndex right) =>
      left.Index != right.Index || left.Synthetic != right.Synthetic;

    public override bool Equals(object obj) => obj is TokenIndex right && (this == right);
    public override int GetHashCode() => HashCode.Combine(Index, Synthetic);
  }

  public readonly struct SourceIndex(int index) { public readonly int Index = index; }

  public readonly struct SourceRecord(string name, FixedRange data, FixedRange tokens, bool synthetic)
  {
    public readonly string Name = name;
    public readonly FixedRange Data = data;
    public readonly FixedRange Tokens = tokens;
    public readonly bool Synthetic = synthetic;
  }
}