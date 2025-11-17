
using System;
using System.Collections.Generic;
using System.IO;

namespace KSASM
{
  public static class Library
  {
    public static string LibraryDir;
    public static readonly List<string> Index = [];

    public static SourceString LoadImport(string name)
    {
      var path = Path.Join(LibraryDir, $"{name}.ksasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown import '{name}");
      return new(name, File.ReadAllText(path));
    }

    public static void RefreshIndex()
    {
      Index.Clear();
      foreach (var file in Directory.EnumerateFiles(LibraryDir))
      {
        if (Path.GetExtension(file) != ".ksasm")
          continue;
        Index.Add(Path.GetFileNameWithoutExtension(file));
      }
    }
  }
}