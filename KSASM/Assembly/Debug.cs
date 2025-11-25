
using System;
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class DebugSymbols
  {
    private readonly Dictionary<int, Token> instTokens = [];
    private readonly Dictionary<string, int> labelToAddr = [];
    private readonly Dictionary<int, string> addrToLabel = [];
    private readonly List<DataRecord> dataRecords = [];
    private bool dataProcessed = false;

    private readonly Dictionary<int, AddrId> addrToId = [];
    private readonly List<AddrId> allIds = [];

    public void AddInst(int address, Token token) => instTokens[address] = token;
    public ReadOnlySpan<char> SourceLine(int address)
    {
      if (instTokens.TryGetValue(address, out var token))
        return token.SourceLine();
      return [];
    }

    public AddrId ID(int address)
    {
      ProcessData();
      if (addrToId.TryGetValue(address, out var id))
        return id;

      if (allIds.Count == 0)
        return new(address, "?", 0);

      var idx = allIds.BinarySearch(new AddrId(address + 1, "", 0));
      if (idx < 0)
        idx = ~idx;

      if (idx == allIds.Count)
        idx--;
      if (allIds[idx].Address > address)
        idx--;

      id = allIds[idx];
      return new(address, id.Label, id.Offset + address - id.Address);
    }

    public void AddLabel(string label, int addr)
    {
      labelToAddr[label] = addr;
      addrToLabel[addr] = label;
      dataProcessed = false;
    }

    public IEnumerable<string> Labels => labelToAddr.Keys;

    public void AddData(int addr, DataType type, int width)
    {
      dataRecords.Add(new(addr, type, width));
      dataProcessed = false;
    }

    private void ProcessData()
    {
      if (dataProcessed)
        return;

      var labels = new List<AddrId>();
      foreach (var (addr, label) in addrToLabel)
        labels.Add(new(addr, label, 0));

      var addrs = new List<int>();
      foreach (var iaddr in instTokens.Keys)
        addrs.Add(iaddr);
      foreach (var data in dataRecords)
      {
        for (var i = 0; i < data.Width; i++)
          addrs.Add(data.Address + data.Type.SizeBytes() * i);
      }

      labels.Sort();
      addrs.Sort();
      dataRecords.Sort((a, b) => a.Address.CompareTo(b.Address));

      addrToId.Clear();
      allIds.Clear();

      var li = 0;
      foreach (var addr in addrs)
      {
        while (li < labels.Count - 1 && addr >= labels[li + 1].Address)
          li++;

        AddrId id;

        if (li >= labels.Count || addr < labels[li].Address)
          id = new(addr, "?", 0);
        else
          id = new(addr, labels[li].Label, addr - labels[li].Address);

        addrToId[addr] = id;
        allIds.Add(id);
      }

      dataProcessed = true;
    }

    public record struct AddrId(int Address, string Label, int Offset) : IComparable, IComparable<AddrId>
    {
      public int CompareTo(AddrId other) => Address.CompareTo(other.Address);
      public int CompareTo(object obj) => obj switch { AddrId other => CompareTo(other), _ => 0 };
    }
    public record struct DataRecord(int Address, DataType Type, int Width);
  }
}