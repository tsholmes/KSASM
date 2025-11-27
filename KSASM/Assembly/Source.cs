
using System;

namespace KSASM.Assembly
{
  public readonly struct SourceString(string name, string source)
  {
    public readonly string Name = name;
    public readonly string Source = source;

    public int Length => Source.Length;

    public char this[int idx] => Source[idx];
    public ReadOnlySpan<char> this[Range range] => Source.AsSpan()[range];
  }
}