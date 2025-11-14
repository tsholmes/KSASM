
using System;
using System.IO;

namespace KSACPU
{
  public static class Library
  {
    public static SourceString LoadImport(string name)
    {
      var path = Path.Join(KSACPUMain.StartDir, $"{name}.kasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown import '{name}");
      return new(name, File.ReadAllText(path));
    }
  }
}