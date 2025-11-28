# KSASM Assembler

This describes the textual language the assembler parses to generate the instruction binary

## Tokens

| Name | Pattern | Notes |
| --- | --- | --- |
| `EOL` | `\n` | newline without preceding escape `\` |
| `Placeholder` | `_` | |
| `Word` | `[_A-Za-z][_A-Za-z0-9.]*` | when not followed by `:`+whitespace |
| `Label` | `<Word>:` | must be followed by whitespace |
| `Position` | `@[0-9]+` | |
| `Width` | `*[0-9]+` | |
| `IOpen` | `[` | |
| `IClose` | `]` | |
| `Comma` | `,` | |
| `Type` | `:<type>` | where `<type>` is one of `u8` `i16` `i32` `i64` `u64` `f64` `p24` `c128` |
| `Offset` | `[+-]` | |
| `Number` | `0[xX][0-9a-fA-F]+` | hex number |
|          | `0[bB][01]+` | binary number |
|          | `[0-9]+(.[0-9]+)([eE][+-]?[0-9]+)` | decimal integer, float, or scientific notation |
| `COpen` | `$(` | |
| `CIOpen` | `$[` | |
| `POpen` | `(` | |
| `PClose` | `)` | |
| `Macro` | `.[_A-Za-z0-9.]+` | |
| `String` | `"[^"\n]*"` | can contain any characters other than double quote and newline |
| `BOpen` | `{` | |
| `BClose` | `}` | |
| `Mult` | `*` | when not followed by digit |
| `Div` | `/` | |
| `Not` | `~` | |

## Grammar

```
line = line-prefix* statement? EOL

line-prefix = Label | Position

statement = data-line | instruction

data-line = (Type (Label | Position | String | data-number | data-const)+)+
  :u8 0*10 label: "string" @300 :u64 $(const * val + 3)

data-number = (Number | Word) Width?

data-const = COpen const-expr PClose Width?
  $(a*(b+123))

instruction = Word Type? Width? operands
  add*3 a+0:u8, +64:i64

operands = base-offab-operands | a-b-operands | placeholder-operands

base-offab-operands = simple-operand offset-operand Type? Comma offset-operand Type?
  a+4, -3:u64
  [a][+4]:u64, [-3]

a-b-operands = value-operand Type? Comma (value-operand | offset-operand) Type?
  a, b
  $[a+4], +10
  [a], $(1,2,3*x,4)

placeholder-operands = (value-operand Type? Comma Placeholder) | (Placeholder Comma value-operand Type?)

value-operand = simple-operand | const-operand | iconst-operand

simple-operand = simple-operand-val | (IOpen simple-operand-val IClose)

offset-operand = (Offset simple-operand-val) | (IOpen Offset simple-operand-val IClose)

simple-operand-val = Word | Number

const-operand = COpen const-expr (Comma const-expr)* PClose

iconst-operand = CIOpen const-expr IClose

const-expr = addsub-expr

addsub-expr = muldiv-expr (Offset muldiv-expr)*

muldiv-expr = group-expr muldiv-expr-right*

muldiv-expr-right = Width | ((Mult | Div) group-expr)

group-expr = Number | Word | (POpen addsub-expr PClose) | (Offset group-expr) | (Not group-expr)
```

## Macros
Macros operate on the stream of tokens before they are parsed into statements. They consume tokens for their inputs and may output other tokens (including other macros which will then be processed the same way).

### Builtin Macros

`.import Word`
- replaced with all tokens in the library file identified by `Word`

`.region Word Offset? Number`
- allocations a chunk `Number` bytes starting at the end of 1MB counting down, creating a label as a marker
- no offset or positive offset produces `@<chunk-address> Word:`
- negative offset produces `@<chunk-address-plus-size> Word: @<chunk-address>`
  - this puts the label at the end of the chunk instead of the beginning

`.add POpen (Width | Number) (Comma Number)* PClose`
- adds the numeric values, outputting the same type of token as the first
  - `.add(1, 2, 3)` outputs `6`
  - `.add(*3, 1)` outputs `*4`

`.ifdef Word BOpen any-with-matched-brackets BClose`
- outputs the contained tokens if `Word` identifies a user-defined macro

`.ifndef Word BOpen any-with-matched-brackets BClose`
- outputs the contained tokens if `Word` does not identify a user-defined macro

`.if POpen any-with-matched-parens PClose BOpen any-with-matched-brackets BClose`
- outputs the contained tokens if there are any tokens between the parenthesis
- useful in the output of a user-defined macro to include a section when an optional argument is present

`.ns Word`
- pushes a Word onto the namespace stack
- all labels and user-defined macros are prefixed with the current namespace stack concatenated
   ```
   .ns ns1
   .ns ns2.
   label: # output as ns1ns2.label:
   .macro umacro # defined as .ns1ns2.macro
   ```

`.endns`
- pops a Word from the namespace stack
- this will cause an error if the namespace stack is empty

`.addns POpen (Word | Macro) PClose`
- adds the current namespace to a Word or Macro token
   ```
   .ns ns1.
   :u8 label: 1 # output as ns1.label
   add:u8 .addns(label), .addns(label) # add:u8 ns1.label, ns1.label 
   ```

`.tomacro POpen Word PClose`
- converts a Word into a Macro token by prefixing with `.`
- `.tomacro(name)` outputs `.name`

`.concat POpen (Word | Macro | Placeholder) (Word | Macro | Placeholder | Number)* PClose`
- concatenates a sequence of tokens into one
- when the first token is a Word or Placeholder, the result is a Word
  - `.concat(_ name .a)` outputs `_name.a`
- when the first token is a Macro, the result is a Macro
  - `.concat(.name _ name 1)` outputs `.name_name1`
- all Number tokens must be decimal integers

`.label POpen Word PClose`
- converts a Word token into a Label by adding a `:` suffix
- `.tolabel(name)` outputs `name:`

### User-Defined Macros
```
.macro Word arg-defs? tokens
arg-defs = (POpen Word (Comma Word)* (Comma ...Word)? PClose) | (POpen ...Word PClose)
tokens = (any-non-eol* EOL) | (BOpen any-with-matched-brackets* BClose)
```
- defines a new macro identified by `Word` which expands to the supplied token sequence
- any `Word` tokens defined in the args list will be replaced by the tokens sequence passed as that argument position when the macro is called
- the final argument may have a `...` prefix, which will cause it to include all tokens after the previous argument values, including `Comma` tokens

`.unmacro Word`
- removes the definition of a macro defined by `Word` prefixed with the current namespace
= errors if macro is not defined
  ```
  .macro mymacro(a, b) a + b
  .unmacro mymacro
  .mymacro(1, 2) # errors
  .unmacro mymacro # also errors
  ```

```
.mymacro macro-args?
macro-args = POpen macro-arg (Comma macro-arg)* PClose
macro-arg = any-non-eol-matched-parens* | (BOpen any-matched-brackets* BClose)
```
- non-bracketed arguments include all tokens until the next Comma not inside matched parenthesis
  - errors on EOL
- bracketed arguments can contain EOL and end at the next unmatched BClose
  - outermost brackets are not included in argument value (unless in ...Rest argument)

### Macro Expansion

When a user-defined macro is run, it expands its arguments to create a new token sequence which is then read first before any further tokens in the file. If there are fewer arguments supplied than defined in the macro, those argument references are removed.

```
.macro constmacro 3
.constmacro # 3
.constmacro() # also 3
.constmacro(4) # still 3
.constmacro({
  anything here .things 1 + 2
}) # just 3

.macro argsmacro(a, b) a + b
.argsmacro(1, 2) # 1 + 2
.argsmacro(1, (2, 3)) # 1 + (2, 3)
.argsmacro(1, {2 3}) # 1 + 2 3
.argsmacro(1) # 1 +
.argsmacro() # +

.macro restmacro(a, ...b) a + b
.restmacro(1, 2) # 1 + 2
.restmacro(1, 2, 3) # 1 + 2, 3
.restmacro(1, {2 3}) # 1 + {2 3}

.macro multiline(a, b) {
  a + b
}
.multiline(1, 2) # EOL 1 + 2 EOL
```

In a macro definition the body may contain macros prefixed with `..` instead of `.` which are executed during expansion instead of being produced as-is and executed when reaching them again. This is useful when a macro defines another macro as part of its output

```
.macro unexpanded(a, b) .macro a(.concat(_ b)) .concat(a .concat(_ b))
.unexpanded(test1, test2)
# outputs:
.macro test1(.concat(_ test2)) .concat(test1 .concat(_ b)) # errors due to malformed argument list

.macro expanded(a, b) .macro a(..concat(_ b)) .concat(a ..concat(_ b))
.expanded(test1, test2)
.test1(test3)
# outputs:
.macro test1(_test2) .concat(test1 _test2)
.concat(test1 test3)
# which then outputs
test1test3
```