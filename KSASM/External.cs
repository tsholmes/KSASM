
using System;
using Brutal.VulkanApi.Abstractions;
using KSA;

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

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  internal class KxAssetInjectAttribute(Type parent, string member, string xmlElement) : Attribute;

  [AttributeUsage(AttributeTargets.Struct)]
  internal class KxUniformBufferAttribute(string xmlElement) : Attribute;

  [AttributeUsage(AttributeTargets.Field)]
  internal class KxUniformBufferLookupAttribute() : Attribute;

  public delegate BufferEx KxBufferLookup(KeyHash hash);
  public delegate MappedMemory KxMemoryLookup(KeyHash hash);
  public delegate Span<T> KxSpanLookup<T>(KeyHash hash) where T : unmanaged;
  public unsafe delegate T* KxPtrLookup<T>(KeyHash hash) where T : unmanaged;
}