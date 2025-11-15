
using System;
using System.IO;

namespace KSASM
{
  public static class Library
  {
    public static string LibraryDir;

    public static SourceString LoadImport(string name)
    {
      var path = Path.Join(LibraryDir, $"{name}.ksasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown import '{name}");
      return new(name, File.ReadAllText(path));
    }
  }
}