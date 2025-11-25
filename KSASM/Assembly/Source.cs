
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class SourceString
  {
    public readonly string Name;
    public readonly string Source;
    private readonly List<int> lineStarts = [];
    public SourceString(string name, string source)
    {
      this.Name = name;
      this.Source = source;
      lineStarts.Add(0);
      for (var i = 0; i < source.Length; i++)
        if (source[i] == '\n')
          lineStarts.Add(i + 1);
      lineStarts.Add(source.Length + 1);
    }

    public int Length => Source.Length;

    public char this[int idx] => Source[idx];

    public Token Token(TokenType type, int pos, int len) => new()
    {
      Source = this,
      Type = type,
      Pos = pos,
      Len = len,
    };

    public string TokenStr(Token token) => Source[token.Pos..(token.Pos + token.Len)];

    public ReadOnlySpan<char> TokenSpan(Token token) =>
      Source.AsSpan()[token.Pos..(token.Pos + token.Len)];

    public (int line, int lpos) LinePos(int pos)
    {
      var line = lineStarts.BinarySearch(pos + 1);
      if (line < 0)
        line = ~line;
      var lpos = pos - lineStarts[line - 1];
      return (line, lpos);
    }
  }
}