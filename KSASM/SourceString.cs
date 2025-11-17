
using System.Collections.Generic;

namespace KSASM
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

    public Assembler.Token Token(Assembler.TokenType type, int pos, int len) => new()
    {
      Source = this,
      Type = type,
      Pos = pos,
      Len = len,
    };

    public string TokenStr(Assembler.Token token) => Source[token.Pos..(token.Pos + token.Len)];

    public string PosStr(int pos)
    {
      var line = lineStarts.FindIndex(start => start > pos);
      var lpos = pos - lineStarts[line - 1];
      return $"{Name}:{line}:{lpos}";
    }
  }
}