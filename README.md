# KSASM (final name TBD)

A programmable autopilot computer for KSA using a fictional assembly language.

[Processor Spec](Spec.md)

[Assembler Spec](Assembler.md)

[Example scripts](KSASM/Examples) (load them in-game from the examples dropdown on the editor tab)

[Library scripts](KSASM/Library) (reference in your scripts by name e.g. `.import libterm`)

While there is a significant amount working with the current design, it is still all subject to change depending on how well it works and fits the vision.

## Installation
- Requires [Starmap](https://github.com/StarMapLoader/StarMap)
- Download zip from Releases and extract into game `Content` folder (for now until mods can be loaded from user folder).
- Add to `manifest.toml` in `%USER%/my games/Kitten Space Agency`
    ```toml
    [[mods]]
    id = "KSASM"
    enabled = true
    ```

## Usage
On load the KSASM window and the Terminal Gauge will be visible. Click `Assemble` to compile the script, and then `Run` to execute it. Editor and debug tools can be dragged in the KSASM window to rearrange them. Check the example scripts for syntax and device usage. Scripts in the `Library` folder can be included with `.import`. Check them for available device fields, and for other utilities.

## Status
- Assembler:
  - Working compilation of text instructions to encoded instructions
  - Incomplete set of built-in macros
  - Working user-defined macro expansion for utilities and meta-programming
  - Missing some error handling. Import/macro loops will hang and then crash the game
- Library scripts:
  - data layouts for currently implemented devices
  - some vector/quaternion utility macros
  - string utilities
    - uses s48 type of address in low bits, length in highbits
    - limited ftoa implementations
      - ftoa_n3: full integer part (up to i64 max value) and 3 decimal places
      - TODO: scientific notation
  - terminal utilites to clear/print to the Terminal gauge while scrolling previous text upwards
- Execution:
  - Working execution of execution of encoded instructions
  - Currently runs up to 10k instructions per frame
  - Some instructions still missing implementations
  - Only executes a script for the currently controlled vehicle
  - Missing some bounds checking/error handling, so you may get exceptions when running invalid instructions
  - Complex value type not implemented
- Devices:
  - Can memory-map static list of devices
    - system for general info view (time, astronomical lookup)
    - vehicle for vehicle-specific info (parts, extra reference frames, inputs)
    - fc for flight-computer calculations (burns and their resulting flight plans)
    - terminal for display gauge
  - Still missing a lot of info/inputs
  - TODO: dynamic devices for parts
  - TODO: separate user inputs from programmatic inputs
  - TODO: add input mask device field to control what user inputs are passed to vehicle for control
- UI/Debug Tools:
  - In-game editor is just a multiline text input with no features
    - can import from examples folder, but has no other save/load
  - MacroView lets you walk into macro expansion from source, or backwards from final result
    - still has some bugs with debug symbols that cause rare crashes
  - InstView shows encoded instructions grouped by the nearest label
  - StackView shows the current stack state
  - MemView shows hex view of memory with instructions and values from source inlined
    - TODO: character view for strings
    - TODO: right-click menu for adding watches/highlighting values
    - TODO: handle window resizing (currently a fixed size of 16x16 bytes)
  - MemWatch lets you watch values by address
    - Currently requires you to type an address or entire label to match
    - Functional, but difficult to use if every value you want to watch doesn't have a label
    - TODO: put search suggestions in clickable dropdown instead of tooltip
    - TODO: don't require 0x prefix to input hex addresses
    - TODO: allow address expressions `label+offset`
    - TODO: allow value expressions `$(addr1:f64 + addr2:u64)`
  - DevView shows the mappable devices and their fields
