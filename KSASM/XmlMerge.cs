
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using KSA;

namespace KSASM
{
  public class MergeVehicle<T> : SerializedId where T : SerializedId
  {
    private static object Collection { get; } = GetLibraryCollection();

    [XmlElement("Value")]
    public T Value;

    public override SerializedId Populate() => throw new NotImplementedException();
    public override TableString.Row ToRow() => TableString.Row.Create("mergeasset", this.Id);

    public override void OnDataLoad(Mod mod)
    {
      base.OnDataLoad(mod);
      Value.SetHash();

      if (Value.Id == "*")
      {
        foreach (var instance in GetAll())
          MergeNested(mod, typeof(T), Value, instance);
      }
      else
      {
        var instance = Lookup(Value.Hash) ??
          throw new InvalidOperationException($"Could not find {typeof(T).Name} '{Value.Id}'");
        MergeNested(mod, typeof(T), Value, instance);
      }
    }

    private static void MergeNested(Mod mod, Type type, object from, object to)
    {
      // TODO: handle more cases here. walk down children
      foreach (var field in type.GetFields())
      {
        if (field.GetCustomAttribute<XmlElementAttribute>() == null
            && field.GetCustomAttribute<XmlAttributeAttribute>() == null)
          continue;
        if (field.FieldType.IsAssignableTo(typeof(IList)))
        {
          var flist = (IList)field.GetValue(from);
          var tlist = (IList)field.GetValue(to);
          foreach (var f in flist)
          {
            tlist.Add(f);
            if (f is SerializedId sid)
              sid.OnDataLoad(mod);
          }
        }
      }
    }

    private static T Lookup(uint hash) =>
      Collection.GetType().GetMethod("Find").Invoke(Collection, [hash]) as T;

    private static IEnumerable<T> GetAll() =>
      ((IList)Collection.GetType().GetMethod("GetList").Invoke(Collection, [])).OfType<T>();

    private static object GetLibraryCollection()
    {
      var fields = typeof(ModLibrary).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
      foreach (var field in fields)
      {
        var ftype = field.FieldType;
        if (!ftype.IsConstructedGenericType || ftype.GetGenericTypeDefinition() != typeof(SerializedCollection<>))
          continue;
        var innerType = ftype.GetGenericArguments()[0];
        if (typeof(T).IsAssignableTo(innerType))
          return field.GetValue(null);
      }
      return null;
    }
  }

  public static class XmlMergeLoader
  {
    private static XmlAttributeOverrides overrides { get; } = SetupOverrides();

    private static XmlAttributeOverrides SetupOverrides()
    {
      var overrides = new XmlAttributeOverrides();

      var attrs = new XmlAttributes();
      var field = typeof(AssetBundle).GetField("Assets");
      foreach (var attr in field.GetCustomAttributes<XmlElementAttribute>())
        attrs.XmlElements.Add(attr);

      attrs.XmlElements.Add(new("MergeVehicle", typeof(MergeVehicle<VehicleTemplate>)));

      overrides.Add(typeof(AssetBundle), "Assets", attrs);

      return overrides;
    }

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public static AssetBundle? Load(string filePath, Mod mod)
    {
      if (!TryDeserialize(filePath, out var result))
        return default;
      result?.OnDataLoad(mod);
      return result;
    }

    private static bool TryDeserialize(string filePath, out AssetBundle? result)
    {
      try
      {
        result = Deserialize(filePath);
        return result != null;
      }
      catch (Exception)
      {
        result = default;
        return false;
      }
    }

    private static AssetBundle? Deserialize(string filePath)
    {
      if (!File.Exists(filePath))
        return default;
      using FileStream fileStream = File.OpenRead(filePath);
      return (new XmlSerializer(typeof(AssetBundle), overrides).Deserialize(fileStream) as AssetBundle) ?? default;
    }
  }
}