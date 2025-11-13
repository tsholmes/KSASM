
namespace KSACPU
{
  public partial class Processor
  {
    private void OpCopy(ValuePointer opA, ValuePointer opB)
    {
      B.Load(Memory, opB);
      B.Store(Memory, opA);
    }

    private void OpReorder(ValuePointer opA, ValuePointer opB)
    {
      A.Load(Memory, opA);
      B.Load(Memory, opB);
      B.Convert(ValueMode.Unsigned);
      C.Init(A.Mode, A.Width);

      for (var i = 0; i < A.Width; i++)
        C.Values[i].Add(A.Values[(int)B.Values[i].Unsigned], C.Mode);

      C.Store(Memory, opA);
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
      A.Load(Memory, opA);
      B.Load(Memory, opB);
      B.Convert(A.Mode);

      for (var i = 0; i < A.Width; i++)
        A.Values[i].Add(B.Values[i], A.Mode);

      A.Store(Memory, opA);
    }

    // private void OpSubtract(ValuePointer opA, ValuePointer opB)
    // private void OpMultiply(ValuePointer opA, ValuePointer opB)
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
    // private void OpJump(ValuePointer opA, ValuePointer opB)
    // private void OpCall(ValuePointer opA, ValuePointer opB)
    // private void OpBranchIfZero(ValuePointer opA, ValuePointer opB)
    // private void OpBranchIfPos(ValuePointer opA, ValuePointer opB)
    // private void OpBranchIfNeg(ValuePointer opA, ValuePointer opB)
    // private void OpSwitch(ValuePointer opA, ValuePointer opB)
    // private void OpSleep(ValuePointer opA, ValuePointer opB)
    // private void OpDevID(ValuePointer opA, ValuePointer opB)
    // private void OpDevType(ValuePointer opA, ValuePointer opB)
    // private void OpDevRead(ValuePointer opA, ValuePointer opB)
    // private void OpDevWrite(ValuePointer opA, ValuePointer opB)
    // private void OpIHandler(ValuePointer opA, ValuePointer opB)
    // private void OpIData(ValuePointer opA, ValuePointer opB)
    // private void OpIReturn(ValuePointer opA, ValuePointer opB)
  }
}