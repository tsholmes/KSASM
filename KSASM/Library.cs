
using System;
using System.Collections.Generic;
using System.IO;
using KSASM.Assembly;

namespace KSASM
{
  public static class Library
  {
    public static string LibraryDir;
    public static readonly List<string> Index = [];

    private static Dictionary<string, SourceString> cache = [];

    public static SourceString LoadImport(string name)
    {
      if (cache.TryGetValue(name, out var cached))
        return cached;
      var path = Path.Join(LibraryDir, $"{name}.ksasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown import '{name}'");
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

    public static void CacheAll()
    {
      RefreshIndex();
      foreach (var name in Index)
        cache[name] = LoadImport(name);
    }
  }
}