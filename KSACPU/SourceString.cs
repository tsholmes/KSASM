
using System.Collections.Generic;

namespace KSACPU
{
  public class SourceString
  {
    private readonly string name;
    private readonly string source;
    private readonly List<int> lineStarts = new();
    public SourceString(string name, string source)
    {
      this.name = name;
      this.source = source;
      lineStarts.Add(0);
      for (var i = 0; i < source.Length; i++)
        if (source[i] == '\n')
          lineStarts.Add(i + 1);
      lineStarts.Add(source.Length + 1);
    }

    public int Length => source.Length;

    public char this[int idx] => source[idx];

    public Assembler.Token Token(Assembler.TokenType type, int pos, int len) => new()
    {
      Source = this,
      Type = type,
      Pos = pos,
      Len = len,
    };

    public string TokenStr(Assembler.Token token) => source[token.Pos..(token.Pos + token.Len)];

    public string PosStr(int pos)
    {
      var line = lineStarts.FindIndex(start => start > pos);
      var lpos = pos - lineStarts[line - 1];
      return $"{name}:{line}:{lpos}";
    }
  }
}