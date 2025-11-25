
using System.Collections.Generic;

namespace KSASM.Assembly
{
  public class DebugSymbols
  {
    private SortedDictionary<int, string> instLocations = [];

    public void AddInst(int address, string loc) => instLocations.Add(address, loc);

    public string InstLocation(int address) => instLocations.GetValueOrDefault(address, "?");
  }
}