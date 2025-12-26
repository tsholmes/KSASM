
using System;

#pragma warning disable CS9113

namespace StarMap.API
{
  [AttributeUsage(AttributeTargets.Class)]
  internal class StarMapModAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapImmediateLoadAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapAllModsLoadedAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapUnloadAttribute : Attribute { }
}

namespace KittenExtensions
{
  [AttributeUsage(AttributeTargets.Class)]
  internal class KxAssetAttribute(string xmlElement) : Attribute;

  [AttributeUsage(AttributeTargets.Class)]
  internal class KxAssetInjectAttribute(Type parent, string member, string xmlElement) : Attribute;
}