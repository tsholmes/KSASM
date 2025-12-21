
using System.Collections.Generic;

namespace KSASM
{
  public class ProgramArgs
  {
    private readonly HashSet<string> flags = [];
    private readonly HashSet<(string, string)> subflags = [];
    private readonly Dictionary<string, string> vals = [];
    private readonly HashSet<string> allSub = [];
    private readonly List<string> positional = [];

    public static ProgramArgs Parse(string[] args)
    {
      var pargs = new ProgramArgs();
      foreach (var arg in args)
        pargs.AddArg(arg);
      return pargs;
    }

    private void AddArg(string arg)
    {
      if (!arg.StartsWith('-'))
      {
        positional.Add(arg);
        return;
      }
      arg = arg.ToLowerInvariant().TrimStart('-');

      var eqIdx = arg.IndexOf('=');
      if (eqIdx == -1)
      {
        flags.Add(arg);
        return;
      }
      var flag = arg[..eqIdx];
      var val = arg[(eqIdx + 1)..];
      vals[flag] = val;
      var sflags = val.Split(',');
      foreach (var subflag in sflags)
      {
        subflags.Add((flag, subflag));
        if (subflag == "*")
          allSub.Add(flag);
      }
    }

    public bool HasFlag(string flag) => flags.Contains(flag.ToLowerInvariant());
    public bool HasFlag(string flag, string subflag)
    {
      flag = flag.ToLowerInvariant();
      return allSub.Contains(flag) || subflags.Contains((flag, subflag.ToLowerInvariant()));
    }

    public bool Positional(int index, out string arg)
    {
      if (index < 0 || index >= positional.Count)
      {
        arg = "";
        return false;
      }
      arg = positional[index];
      return true;
    }

    public bool Val(string flag, out string val) => vals.TryGetValue(flag, out val);
  }
}