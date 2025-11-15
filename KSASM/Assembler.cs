
using System;
using System.Collections.Generic;
using System.Text;

namespace KSASM
{
  public partial class Assembler
  {
    public static bool Debug = false;

    public static void Assemble(SourceString source, Memory target)
    {
      var parser = new Parser(source);
      parser.Parse();

      var state = new State();

      foreach (var stmt in parser.Statements)
        stmt.FirstPass(state);
      state.EmitConstants();
      state.Addr = 0;
      foreach (var stmt in parser.Statements)
        stmt.SecondPass(state);

      var sb = new StringBuilder();

      foreach (var (addr, type, val) in state.Values)
      {
        target.Write(addr, type, val);
        if (Debug)
        {
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

      public readonly Dictionary<(DataType, Value), int> ConstWidths = [];
      public readonly Dictionary<(DataType, Value), int> ConstAddrs = [];

      public readonly Dictionary<(DataType, string), int> ConstLabelWidths = [];
      public readonly Dictionary<(DataType, string), int> ConstLabelAddrs = [];

      public int Addr = 0;

      public void Emit(DataType type, Value value)
      {
        Values.Add((Addr, type, value));
        Addr += type.SizeBytes();
      }

      public void RegisterConst(DataType type, Value val, int width)
      {
        if (Debug)
          Console.WriteLine($"ASM RCONST {type}*{width} {val.As(type)}");
        ConstWidths[(type, val)] = Math.Max(width, ConstWidths.GetValueOrDefault((type, val)));
      }
      public void RegisterConst(DataType type, string label, int width) =>
        ConstLabelWidths[(type, label)] = Math.Max(width, ConstLabelWidths.GetValueOrDefault((type, label)));

      public void EmitConstants()
      {
        // register label consts as regular consts
        foreach (var ((type, label), width) in ConstLabelWidths)
        {
          if (!Labels.TryGetValue(label, out var laddr))
            throw new InvalidOperationException($"Unknown const label {label}");
          var val = new Value { Unsigned = (ulong)laddr };
          val.Convert(ValueMode.Unsigned, type.VMode());
          RegisterConst(type, val, width);
        }

        if (ConstWidths.Count == 0)
          return;

        if (!Labels.TryGetValue("CONST", out var caddr))
          throw new InvalidOperationException("Cannot emit inlined constants without CONST label");

        Addr = caddr;
        foreach (var ((type, val), width) in ConstWidths)
        {
          ConstAddrs[(type, val)] = Addr;
          for (var i = 0; i < width; i++)
            Emit(type, val);
        }

        // build label mapping
        foreach (var ((type, label), width) in ConstLabelWidths)
        {
          var val = new Value { Unsigned = (ulong)Labels[label] };
          val.Convert(ValueMode.Unsigned, type.VMode());
          ConstLabelAddrs[(type, label)] = ConstAddrs[(type, val)];
        }
      }
    }
  }
}