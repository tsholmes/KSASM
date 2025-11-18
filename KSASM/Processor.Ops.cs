
using System.Collections.Generic;

namespace KSASM
{
  public partial class Processor
  {
    private void OpCopy(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opB, B);
      Memory.Write(opA, B);
    }

    private void OpReorder(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      C.Init(A.Mode, A.Width);

      for (var i = 0; i < A.Width; i++)
        C.Values[i].Add(A.Values[(int)B.Values[i].Unsigned], C.Mode);

      Memory.Write(opA, C);
    }

    // private void OpBitNot(ValuePointer opA, ValuePointer opB)
    // private void OpNegate(ValuePointer opA, ValuePointer opB)
    // private void OpConjugate(ValuePointer opA, ValuePointer opB)
    // private void OpSign(ValuePointer opA, ValuePointer opB)
    // private void OpAbs(ValuePointer opA, ValuePointer opB)
    // private void OpBitAnd(ValuePointer opA, ValuePointer opB)
    // private void OpBitOr(ValuePointer opA, ValuePointer opB)
    // private void OpBitXor(ValuePointer opA, ValuePointer opB)
    // private void OpShiftLeft(ValuePointer opA, ValuePointer opB)
    // private void OpShiftRight(ValuePointer opA, ValuePointer opB)

    private void OpAdd(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(A.Mode);

      for (var i = 0; i < A.Width; i++)
        A.Values[i].Add(B.Values[i], A.Mode);

      Memory.Write(opA, A);
    }

    private void OpSubtract(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(A.Mode);

      for (var i = 0; i < A.Width; i++)
        A.Values[i].Sub(B.Values[i], A.Mode);

      Memory.Write(opA, A);
    }

    private void OpMultiply(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      B.Convert(A.Mode);

      for (var i = 0; i < A.Width; i++)
        A.Values[i].Mul(B.Values[i], A.Mode);

      Memory.Write(opA, A);
    }

    // private void OpDivide(ValuePointer opA, ValuePointer opB)
    // private void OpRemainder(ValuePointer opA, ValuePointer opB)
    // private void OpModulus(ValuePointer opA, ValuePointer opB)
    // private void OpPower(ValuePointer opA, ValuePointer opB)
    // private void OpMax(ValuePointer opA, ValuePointer opB)
    // private void OpMin(ValuePointer opA, ValuePointer opB)
    // private void OpUFpu(ValuePointer opA, ValuePointer opB)
    // private void OpAll(ValuePointer opA, ValuePointer opB)
    // private void OpAny(ValuePointer opA, ValuePointer opB)
    // private void OpParity(ValuePointer opA, ValuePointer opB)
    // private void OpSum(ValuePointer opA, ValuePointer opB)
    // private void OpProduct(ValuePointer opA, ValuePointer opB)
    // private void OpMinAll(ValuePointer opA, ValuePointer opB)
    // private void OpMaxAll(ValuePointer opA, ValuePointer opB)

    private void OpJump(ValuePointer opA, ValuePointer opB)
    {
      PC = opB.Address;
    }

    private void OpCall(ValuePointer opA, ValuePointer opB)
    {
      opA.Width = 1;
      A.Init(ValueMode.Unsigned, 1);
      A.Values[0].Unsigned = (ulong)PC;
      Memory.Write(opA, A);

      PC = opB.Address;
    }

    private void OpBranchIfSign(ValuePointer opA, ValuePointer opB, int sign)
    {
      opA.Width = opB.Width = 1;
      Memory.Read(opA, A);

      if (A.Values[0].Sign(A.Mode) == sign)
        PC = opB.Address;
    }

    // private void OpSwitch(ValuePointer opA, ValuePointer opB)

    private void OpSleep(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opB, B);
      B.Convert(ValueMode.Unsigned);
      SleepTime = B.Values[0].Unsigned;
      if (SleepTime == 0)
        SleepTime = 1;
    }

    // private void OpDevID(ValuePointer opA, ValuePointer opB)
    // private void OpDevType(ValuePointer opA, ValuePointer opB)

    private void OpDevMap(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);

      A.Convert(ValueMode.Unsigned);
      B.Convert(ValueMode.Unsigned);

      var memAddr = (int)A.Values[0].Unsigned;
      var memLen = (int)A.Values[1].Unsigned;
      var devId = B.Values[0].Unsigned;
      var devAddr = (int)B.Values[1].Unsigned;

      if (devId == 0)
        MappedMemory.MapRange(memAddr, MainMemory, memAddr, memLen);
      else
      {
        var device = deviceMap.GetValueOrDefault(devId) ?? defaultDevice;
        MappedMemory.MapRange(memAddr, device, devAddr, memLen);
      }
    }

    // private void OpIHandler(ValuePointer opA, ValuePointer opB)
    // private void OpIData(ValuePointer opA, ValuePointer opB)
    // private void OpIReturn(ValuePointer opA, ValuePointer opB)

    private void OpDebug(ValuePointer opA, ValuePointer opB)
    {
      Memory.Read(opA, A);
      Memory.Read(opB, B);
      OnDebug?.Invoke(A, B);
    }
  }
}