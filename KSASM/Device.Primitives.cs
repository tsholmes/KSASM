
using Brutal.Numerics;

namespace KSASM
{
  public abstract class UintDeviceField<T>(int offset)
  : LeafDeviceField<T, uint>(DataType.U64, offset, UintValueConverter.Instance)
  { }

  public abstract class ReadOnlyUintDeviceField<T>(int offset) : UintDeviceField<T>(offset)
  {
    protected override void SetValue(ref T parent, uint value) { }
  }

  public abstract class DoubleDeviceField<T>(int offset)
  : LeafDeviceField<T, double>(DataType.F64, offset, DoubleValueConverter.Instance)
  { }

  public abstract class ReadOnlyDoubleDeviceField<T>(int offset) : DoubleDeviceField<T>(offset)
  {
    protected override void SetValue(ref T parent, double value) { }
  }

  public abstract class Double3DeviceField<T>(int offset)
  : ValueCompositeDeviceField<T, double3>(offset, X, Y, Z)
  {
    public static readonly Element X = new(0);
    public static readonly Element Y = new(1);
    public static readonly Element Z = new(2);

    public class Element(int index) : DoubleDeviceField<double3>(index * DataType.F64.SizeBytes())
    {
      protected override double GetValue(ref double3 parent) => parent[index];
      protected override void SetValue(ref double3 parent, double value) => parent[index] = value;
    }
  }
}