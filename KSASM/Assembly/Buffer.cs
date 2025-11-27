
using System;

namespace KSASM.Assembly
{
  public class ParseBuffer
  {
    private readonly AppendBuffer<char> sourceData = new();
    private readonly AppendBuffer<SourceRecord> sources = new();
    private readonly AppendBuffer<Token> tokens = new();

    public RawSourceBuilder NewRawSource(string name, ReadOnlySpan<char> data, TokenIndex producer)
    {
      var start = sourceData.AddRange(data);
      return new(this, name, new(start, data.Length), producer);
    }

    public SyntheticSourceBuilder NewSynthSource(string name, TokenIndex producer) => new(this, name, producer);

    private void FinishSource(SourceIndex index, FixedRange data, FixedRange tokens)
    {
      var source = sources[index.Index];
      if (source.Tokens.Length != -1)
        throw new InvalidOperationException();
      sources[index.Index] = new(source.Name, data, tokens, source.Synthetic, source.Producer);
    }

    private SourceIndex ReserveSource(string name, bool synthetic, FixedRange dataRange, TokenIndex producer) =>
      new(sources.Add(new SourceRecord(name, dataRange, new(tokens.Length, -1), synthetic, producer)));

    private TokenIndex AddToken(SourceIndex source, TokenType type, FixedRange data, TokenIndex previous) =>
      new(tokens.Add(new(source, new(tokens.Length), type, data, previous)));

    public int TokenCount => tokens.Length;
    public Token this[TokenIndex idx] => tokens[idx.Index];
    public ReadOnlySpan<char> this[Token token] => Data(token.Data);
    public ReadOnlySpan<char> Data(FixedRange range) => sourceData[range];

    public ReadOnlySpan<char> SourceName(SourceIndex source) => sources[source.Index].Name;
    public SourceRecord Source(SourceIndex source) => sources[source.Index];
    public TokenReader SourceReader(SourceIndex source) => new(this, source);
    public TokenEnumerable SourceTokens(SourceIndex src) => TokenRange(sources[src.Index].Tokens);
    public TokenEnumerable TokenRange(FixedRange range) => new(this, range);
    public ReadOnlySpan<Token> TokenSpan(FixedRange range) => tokens[range];

    public struct TokenEnumerable(ParseBuffer buf, FixedRange range)
    {
      public TokenEnumerator GetEnumerator() => new(buf, range);
    }

    public struct TokenEnumerator(ParseBuffer buf, FixedRange range)
    {
      private int index = -1;
      public Token Current => buf[new TokenIndex(range.Start + index)];
      public bool MoveNext() => ++index < range.Length;
    }

    public struct TokenReader(ParseBuffer buf, SourceIndex source)
    {
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
        token = buf[new TokenIndex(tokens.Start + index)];
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
      public readonly SourceIndex Source = buf.ReserveSource(name, false, dataRange, producer);
      private readonly int tokenStart = buf.tokens.Length;

      public TokenIndex AddToken(TokenType type, FixedRange range)
      {
        range += dataRange.Start;
        if (range.End > dataRange.End)
          throw new IndexOutOfRangeException();
        return buf.AddToken(Source, type, range, producer);
      }

      public void Dispose() =>
        buf.FinishSource(Source, dataRange, new(tokenStart, buf.tokens.Length - tokenStart));
    }

    public readonly struct SyntheticSourceBuilder(ParseBuffer buf, string name, TokenIndex producer) : IDisposable
    {
      public readonly SourceIndex Source = buf.ReserveSource(name, true, new(buf.sourceData.Length, -1), producer);
      private readonly int dataStart = buf.sourceData.Length;
      private readonly int tokenStart = buf.tokens.Length;

      public TokenIndex FirstToken => new(tokenStart);
      public TokenIndex NextToken => new(buf.tokens.Length);
      public TokenIndex LastToken => new(buf.tokens.Length - 1);

      public TokenIndex CopyToken(TokenIndex from)
      {
        var tok = buf[from];
        return buf.AddToken(Source, tok.Type, tok.Data, from);
      }

      public SyntheticTokenBuilder MakeToken(TokenType type, TokenIndex from) =>
        new(buf, Source, type, from);

      public void Dispose()
      {
        var data = new FixedRange(dataStart, buf.sourceData.Length - dataStart);
        var tokens = new FixedRange(tokenStart, buf.tokens.Length - tokenStart);
        buf.FinishSource(Source, data, tokens);
      }
    }

    public readonly struct SyntheticTokenBuilder(
      ParseBuffer buf, SourceIndex source, TokenType type, TokenIndex from) : IDisposable
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

      public void Dispose() => buf.AddToken(source, type, new(dataStart, buf.sourceData.Length - dataStart), from);
    }
  }

  public readonly struct Token(
    SourceIndex source, TokenIndex index, TokenType type, FixedRange data, TokenIndex previous)
  {
    public readonly SourceIndex Source = source;
    public readonly TokenIndex Index = index;
    public readonly TokenType Type = type;
    public readonly FixedRange Data = data;
    public readonly TokenIndex Previous = previous;
  }

  public readonly struct TokenIndex(int index)
  {
    public static readonly TokenIndex Invalid = new(-1);
    public readonly int Index = index;

    public static bool operator ==(TokenIndex left, TokenIndex right) => left.Index == right.Index;
    public static bool operator !=(TokenIndex left, TokenIndex right) => left.Index != right.Index;

    public override bool Equals(object obj) => obj is TokenIndex other && Index == other.Index;
    public override int GetHashCode() => Index;
  }

  public readonly struct SourceIndex(int index) { public readonly int Index = index; }

  public readonly struct SourceRecord(string name, FixedRange data, FixedRange tokens, bool synthetic, TokenIndex producer)
  {
    public readonly string Name = name;
    public readonly FixedRange Data = data;
    public readonly FixedRange Tokens = tokens;
    public readonly bool Synthetic = synthetic;
    public readonly TokenIndex Producer = producer;
  }
}