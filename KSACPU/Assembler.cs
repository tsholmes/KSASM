
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KSACPU
{
  public partial class Assembler
  {
    public static bool Debug = false;

    public static void Assemble(string source, Memory target)
    {
      var parser = new Parser(source);
      parser.Parse();

      var state = new State();

      foreach (var stmt in parser.Statements)
        stmt.FirstPass(state);
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(state);

      var sb = new StringBuilder();

      foreach (var (addr, type, val) in state.Values)
      {
        target.Write(addr, type, val);
        if (Debug) {
          sb.Clear();
          sb.Append("ASM ");
          foreach (var (label, laddr) in state.Labels)
            if (laddr == addr)
              sb.Append(label).Append(": ");
          sb.Append($"{addr}: {type} {val.As(type)}");
          Console.WriteLine(sb.ToString());
        }
      }
    }

    public class State
    {
      public readonly Dictionary<string, int> Labels = [];
      public readonly List<(int, DataType, Value)> Values = [];

      public int Addr = 0;

      public void Emit(DataType type, Value value)
      {
        Values.Add((Addr, type, value));
        Addr += type.SizeBytes();
      }
    }
  }
}