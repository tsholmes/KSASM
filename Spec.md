# KSASM

- PC (Program Counter), SP (Stack Pointer), FP (Frame Pointer) registers
- instructions pop inputs from stack (after immediate values) and push results onto tstack
- instructions encode data type of inputs/outputs
- applicable instructions can operate on up to 8 sequential values
- 24 bit wide instructions
- 24 bit addresses
- stack grows downwards from end of memory

## Instruction Encoding
| DataType C | DataType B | DataType A | ImmCount | Width | OpCode |
| :---: | :---: | :---: | :---: | :---: | :---: |
| 23-20 | 19-16 | 15-12 | 11-10 | 9-7 | 6-0 |

Some Instructions have fixed values for Width and DataTypes, where the encoded values will be ignored.

### Opcode (7 bits)
Specifies which operation to perform

### Width (3 bits)
Specifies the number of adjacent values to operate on, offset by 1.
* `000` data width 1
* `001` data width 2
* ...
* `111` data width 8

### ImmCount (2 bit)
Specifies number of operands which are read at the PC instead of popped from the stack (in order A,B,C).

### Data Type (4 bits)
Specifies the data type of an input or output

| Bits | Name | Description | |
| ---: | :--- | :--- | :--- |
| `0000` | u8 | unsigned 8-bit int | |
| `0001` | u16 | unsigned 16-bit int | |
| `0010` | u32 | unsigned 32-bit int | |
| `0011` | u64 | unsigned 64-bit int | |
| `0100` | i8 | signed 8-bit int | |
| `0101` | i16 | signed 16-bit int | |
| `0110` | i32 | signed 32-bit int | |
| `0111` | i64 | signed 64-bit int | |
| `1000` | f64 | 64-bit floating point | |
| `1001` | c128 | 128-bit complex | pair of f64, real part in low bits, imaginary part in high bits |
| `1010` | p24 | unsigned 24-bit address | |
| `1011` | s48 | 48-bit string address | pair of p24, address in low bits, length in high bits |
| `1100-1111` | reserved | | |

## Instruction Execution

### Sequence
* PC is advanced +3 to end of instruction
* For each operand up to ImmCount
  * Operand is read from PC
  * PC is advanced by size of Operand (sizeof(DataType) * Width)
* Non-immediate input operands are popped off the stack in order A,B,C
* Computation is performed
* Result operands are pushed onto the stack in order A,B,C

### Value Modes
When executing, operand values are read as the data type specified in the instruction and expanded into the largest similar type:
- u8, u16, u32, u64, p24, p48: Unsigned (64-bit unsigned integer)
- i8, i16, i32, i64: Signed (64-bit signed integer)
- f64: Float (64-bit double precision floating point)
- c128: Complex (128-bit pair of double precision floating points)

The operands are then converted between value modes before execution according to the OpMode column in the table below (`A` means value is converted to mode of operand A, `Mode` means its converted to a specific mode, `-` means no conversion is done before operation).

## Opcodes

* TODO: decide if adjf and ret should run of fp instead of sp (auto-truncate stack to correct height)
* TODO: arithmetic vs logical shift
* TODO: real/imag for getting complex parts
* TODO: device enumeration and info commands? when we have dynamic devices from parts
* TODO: interrupts if they end up being necessary
* TODO: assign static opcode numbers once instruction list is finalized

Stack effects are listed in the form `Op:Type*Width ... -> Op:Type*Width`. Operands to the left of `->` are inputs, operands to the right are results. If `Type` and/or `Width` are specified for an operand, those values are fixed for this instruction, otherwise they follow the DataType field for that operand and the Width field of the instruction.

| Hex | Name | Stack | OpMode | Description |
| - | - | - | - | - |
| <td colspan=3 align=center>**Stack Manipulation**</td> |
| | push | `A -> ` | - | `push A` |
| | pop | `A ->` | - | - |
| | dup | `A:u8*1 B ->` | - | `repeat clamp(A,2,8) push B` (duplicates B up to 8 times) |
| | swz | `A:u8 B -> C` | - | `B[A[i]%Width] -> C[i]` |
| <td colspan=3 align=center>**Memory Load/Store**</td> |
| | ld | `A:p24*1 -> B` | - | `Mem[A] -> B`|
| | st | `A:p24*1 B ->` | - | `B -> Mem[A]` |
| <td colspan=3 align=center>**Offset Load/Store**</td> |
| | ldf | `A:p24*1 -> B` | - | `Mem[FP+A] -> B` |
| | lds | `A:p24*1 -> B` | - | `Mem[SP+A] -> B` |
| | stf | `A:p24*1 B ->` | - | `B -> Mem[FP+A]` |
| | sts | `A:p24*1 B ->` | - | `B -> Mem[FP+A]` |
| <td colspan=3 align=center>**Register Manipulation**</td> |
| | ldfp | `-> A:p24*1` | - | `FP -> A` |
| | stfp | `A:p24*1 ->` | - | `A -> FP` |
| | modfp | `A:p24*1 ->` | - | `FP+A -> FP` |
| | ldsp | `-> A:p24*1` | - | `SP -> A` |
| | stsp | `A:p24*1 ->` | - | `A -> SP` |
| | modsp | `A:p24*1 ->` | - | `SP+A -> SP` |
| <td colspan=3 align=center>**Bitwise**</td> |
| | not | `A -> B` | - | `~A[i] -> B[i]` |
| | and | `A B -> C` | - | `A[i] & B[i] -> C[i]` |
| | or | `A B -> C` | - | `A[i] \| B[i] -> C[i]` |
| | xor | `A B -> C` | - | `A[i] ^ B[i] -> C[i]` |
| | shl | `A B -> C` | Unsigned, - | `B[i] << A[i] -> C[i]` |
| | shr | `A B -> C` | Unsigned, - | `B[i] >> A[i] -> C[i]` |
| <td colspan=3 align=center>**Math**</td> |
| | neg | `A -> B` | - | `-A[i] -> B[i]` |
| | sign | `A -> B` | - | `sign(A[i]) -> B[i]` ({0,1} for Unsigned, {-1,0,1} for rest) |
| | abs | `A -> B` | - | `\|A[i]\| -> B[i]` |
| | add |  `A B -> C` | B, - | `A[i] + B[i] -> C[i]` |
| | sub | `A B -> C` | B, - | `B[i] - A[i] -> C[i]` |
| | mul | `A B -> C` | B, - | `A[i] * B[i] -> C[i]` |
| | div | `A B -> C` | B, - | `B[i] / A[i] -> C[i]` |
| | rem | `A B -> C` | B, - | `rem(B[i] / A[i]) -> C[i]` (`(-\|A[i]\|, \|A[i]\|)` with sign of `B[i]`) |
| | mod | `A B -> C` | B, - | `B[i] % A[i] -> C[i]` (`[0, \|A[i]\|)`) |
| | pow | `A B -> C` | B, - | `B[i]**A[i] -> C[i]` |
| | max | `A B -> C` | B, - | `max(A[i], B[i]) -> C[i]` |
| | min | `A B -> C` | B, - | `min(A[i], B[i]) -> C[i]` |
| <td colspan=3 align=center>**Float Math**</td> |
| | floor | `A -> B` | Float | `floor(A[i]) -> B[i]` |
| | ceil | `A -> B` | Float | `ceil(A[i]) -> B[i]` |
| | round | `A -> B` | Float | `round(A[i]) -> B[i]` |
| | trunc | `A -> B` | Float | `trunc(A[i]) -> B[i]` |
| | sqrt | `A -> B` | Float | `sqrt(A[i]) -> B[i]` |
| | exp | `A -> B` | Float | `e**A[i] -> B[i]` |
| | log | `A -> B` | Float | `log(A[i]) -> B[i]` |
| | log2 | `A -> B` | Float | `log2(A[i]) -> B[i]` |
| | log10 | `A -> B` | Float | `log10(A[i]) -> B[i]` |
| | sin | `A -> B` | Float | `sin(A[i]) -> B[i]` |
| | cos | `A -> B` | Float | `cos(A[i]) -> B[i]` |
| | tan | `A -> B` | Float | `tan(A[i]) -> B[i]` |
| | sinh | `A -> B` | Float | `sinh(A[i]) -> B[i]` |
| | cosh | `A -> B` | Float | `cosh(A[i]) -> B[i]` |
| | tanh | `A -> B` | Float | `tanh(A[i]) -> B[i]` |
| | asin | `A -> B` | Float | `asin(A[i]) -> B[i]` |
| | acos | `A -> B` | Float | `acos(A[i]) -> B[i]` |
| | atan | `A -> B` | Float | `atan(A[i]) -> B[i]` |
| | asinh | `A -> B` | Float | `asinh(A[i]) -> B[i]` |
| | acosh | `A -> B` | Float | `acosh(A[i]) -> B[i]` |
| | atanh | `A -> B` | Float | `atanh(A[i]) -> B[i]` |
| <td colspan=3 align=center>**Complex Math**</td> |
| | conj | `A -> B` | Complex | `conj(A[i]) -> B[i]` (`x + yi -> x - yi`) |
| <td colspan=3 align=center>**Reduce**</td> |
| | andr | `A -> B*1` | - | `reduce(and A[i]) -> B` |
| | orr | `A -> B*1` | - | `reduce(or A[i]) -> B` |
| | xorr | `A -> B*1` | - | `reduce(xor A[i]) -> B` |
| | addr | `A -> B*1` | - | `reduce(add A[i]) -> B` |
| | mulr | `A -> B*1` | - | `reduce(mul A[i]) -> B` |
| | minr | `A -> B*1` | - | `reduce(min A[i]) -> B` |
| | maxr | `A -> B*1` | - | `reduce(max A[i]) -> B` |
| <td colspan=3 align=center>**Branching**</td> |
| | jump | `A:p24*1 ->` | - | `A -> PC` |
| | bzero | `A:p24*1 B*1 ->` | - | `if (B = 0) A -> PC` |
| | bpos | `A:p24*1 B*1 ->` | - | `if (B > 0) A -> PC` |
| | bneg | `A:p24*1 B*1 ->` | - | `if (B < 0) A -> PC` |
| | blt | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C < B) A -> PC` |
| | ble | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C <= B) A -> PC` |
| | beq | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C = B) A -> PC` |
| | bne | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C != B) A -> PC` |
| | bge | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C >= B) A -> PC` |
| | bgt | `A:p24*1 B*1 C*1 ->` | -, C, - | `if (C > B) A -> PC` |
| | sw | `A:p24 B*1 ->` | -, Unsigned | `if (B < Width) A[B] -> PC` |
| <td colspan=3 align=center>**Function**</td> |
| | call | `A:p24*1 -> PC FP` | - | `SP -> temp; push PC; push FP; temp -> FP; A -> PC` |
| | adjf | `A:p24*1` | - | `pop temp:p24*2; SP+A -> SP; SP -> FP; push temp`<br/>when return FP,PC on stack, move on stack by A and save addr in FP |
| | ret | `PC FP ->` | - | `pop FP; pop PC` |
| <td colspan=3 align=center>**Misc**</td> |
| | rand | `A -> B` | - | `rand(A[i]) -> B[i]` (`[0, x)` when `x>0`, `(x, \|x\|)` when `x<0`) |
| | sleep | `A*1 ->` | Unsigned | `sleep(A)` (pause A ticks, minimum 1) |
| | devmap | `A:p24*4 ->` | - | `map mem[A[0]..A[0]+A[1]] -> device[A[2]].mem[A[3]..]` |
| 7F | debug | `A ->` | - | `print(A)` |
