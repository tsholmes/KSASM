
using System;
using System.Collections.Generic;
using System.IO;
using KSASM.Assembly;

namespace KSASM
{
  public static class Library
  {
    public static readonly List<string> Examples = [];
    private static string libraryDir;
    private static string examplesDir;

    public static void Init(string cwd)
    {
      libraryDir = ResolveDir(cwd, "Library");
      examplesDir = ResolveDir(cwd, "Examples");
    }

    private static string ResolveDir(string cwd, string name)
    {
      cwd ??= Directory.GetCurrentDirectory();
      var asmDir = Directory.GetParent(typeof(Library).Assembly.Location).FullName;
      var candidates = new string[] {
        Path.Join(asmDir, name),
        Path.Join(cwd, name),
      };
      foreach (var p in candidates)
      {
        if (Directory.Exists(p))
          return p;
      }
      return cwd;
    }

    public static SourceString LoadImport(string name)
    {
      var path = Path.Join(libraryDir, $"{name}.ksasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown import '{name}'");
      return new(name, File.ReadAllText(path));
    }

    public static SourceString LoadExample(string name)
    {
      var path = Path.Join(examplesDir, $"{name}.ksasm");
      if (!File.Exists(path))
        throw new InvalidOperationException($"unknown example '{name}'");
      return new(name, File.ReadAllText(path));
    }

    public static void RefreshIndex()
    {
      Examples.Clear();
      foreach (var file in Directory.EnumerateFiles(examplesDir))
      {
        if (Path.GetExtension(file) != ".ksasm")
          continue;
        Examples.Add(Path.GetFileNameWithoutExtension(file));
      }
    }
  }
}