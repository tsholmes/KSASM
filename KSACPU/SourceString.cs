
using System.Collections.Generic;

namespace KSACPU
{
  public class SourceString
  {
    private readonly string source;
    private readonly List<int> lineStarts = new();
    public SourceString(string source)
    {
      this.source = source;
      lineStarts.Add(0);
      for (var i = 0; i < source.Length; i++)
        if (source[i] == '\n')
          lineStarts.Add(i + 1);
      lineStarts.Add(source.Length);
    }

    public int Length => source.Length;

    public char this[int idx] => source[idx];
    public string this[Assembler.Token token] => source[token.Pos..(token.Pos + token.Len)];

    public string Pos(int pos)
    {
      var line = lineStarts.FindIndex(start => start > pos);
      var lpos = pos - lineStarts[line - 1];
      return $"{line}:{lpos}";
    }
  }
}