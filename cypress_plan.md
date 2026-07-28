# Plan: FX3 I2C Firmware Programmer

Build a simple Windows WPF utility in C# that uses the existing `CyUSB.dll` to discover Cypress USB devices, program FX3 boot firmware into an I2C EEPROM, verify the write, and safely erase the EEPROM when requested. The app should isolate CyUSB/control-transfer logic from the GUI, model EEPROM profiles explicitly, and make AT24CM01 the first built-in profile while keeping address, EEPROM size, page size, and addressing mode configurable in the UI.

## Steps

1. Bootstrap the WPF solution. Create a C# WPF app plus a small core/library layer, reference the existing `CyUSB.dll`, and first confirm whether the DLL requires .NET Framework 4.8 or can be used from a modern .NET WPF target. This step determines the final project target and build configuration.
2. Add core models and validation. Define `FirmwareImage`, `EepromProfile`, `UsbDeviceInfo`, `ProgrammingOptions`, `ProgrammingResult`, and `OperationProgress` models. Include validation for 7-bit I2C address input with default `0x50`, EEPROM size, page size, and maximum firmware size.
3. Build EEPROM profiles. Add AT24CM01 as the initial default profile: 128 KiB capacity, page-write chunking, and high-address/banked behavior appropriate for 1 Mbit EEPROMs. Add UI-editable settings so later EEPROM sizes can be supported without code changes.
4. Implement CyUSB device discovery. Wrap `CyUSB.dll` behind a small hardware abstraction that scans all CyUSB devices, returns VID/PID/serial/product details when available, and requires the user to explicitly select a target before write or erase.
5. Confirm and implement the Cypress FX3 command protocol. Start with the standard Cypress FX3 bootloader/control-center protocol. Implement a narrow protocol spike first: open the selected device, perform a harmless read/status operation, and confirm the request IDs and transfer shape before adding write/erase behavior.
6. Implement firmware loading. Add a file picker with filters for `.iic`, `.img`, and `.bin`. Parse `.iic` as the preferred FX3 I2C EEPROM image format. Support `.bin` as raw payload. Support `.img` only when it is valid for EEPROM programming or can be converted safely; otherwise surface a clear validation warning before programming.
7. Implement programming workflow. Preflight device selection, address/profile validity, file size, and protocol availability. Program in page-aligned chunks, show progress, retry/poll as required by the EEPROM, then perform full read-back verification against the loaded image.
8. Implement safe firmware deletion. Provide an `Erase EEPROM` action rather than ambiguous delete wording. Require target device, address, and profile confirmation, then overwrite the configured EEPROM range with blank bytes and verify that the target reads back blank. Disable erase during active operations.
9. Build the simple GUI. Main view should include device refresh/selection, firmware file selector, I2C address field defaulting to `0x50`, EEPROM profile/settings area, `Program + Verify`, `Verify Only`, `Erase EEPROM`, progress bar, status summary, and an operation log. Use async commands so the UI remains responsive.
10. Add settings persistence. Store last used I2C address, EEPROM profile, and maybe last firmware directory in a local user settings file. Do not silently reuse a previously selected USB device for destructive operations; require current-session selection.
11. Package and document usage. Add build output instructions, DLL placement expectations, driver/runtime prerequisites, and a short operator workflow for program, verify, and erase.

## Relevant Files

- `CyUSB.dll` - existing Cypress library to reference and wrap.
- `CypressFx3I2cProgrammer.sln` - planned solution file.
- `src/Fx3I2cProgrammer.App/Fx3I2cProgrammer.App.csproj` - planned WPF application project.
- `src/Fx3I2cProgrammer.App/MainWindow.xaml` - planned main GUI shell.
- `src/Fx3I2cProgrammer.App/ViewModels/MainWindowViewModel.cs` - planned MVVM command/state coordination.
- `src/Fx3I2cProgrammer.Core/Firmware/FirmwareImageLoader.cs` - planned `.iic`, `.img`, and `.bin` loading and validation.
- `src/Fx3I2cProgrammer.Core/Eeprom/EepromProfile.cs` - planned EEPROM profile model, including AT24CM01 default.
- `src/Fx3I2cProgrammer.Hardware/CyUsbDeviceEnumerator.cs` - planned CyUSB discovery wrapper.
- `src/Fx3I2cProgrammer.Hardware/Fx3BootloaderClient.cs` - planned standard FX3 bootloader/control-transfer implementation.
- `tests/Fx3I2cProgrammer.Tests/` - planned focused tests for parsers, EEPROM chunking, validation, and workflow state.

## Verification

1. Build the solution in the chosen target framework and confirm `CyUSB.dll` loads successfully on the target machine and CPU architecture.
2. Unit test firmware parsing for valid `.iic`, valid `.bin`, oversized payloads, malformed files, and unsupported `.img` cases.
3. Unit test AT24CM01 chunking across page boundaries and the 64 KiB bank boundary.
4. Unit test I2C address validation, including default `0x50`, valid alternate 7-bit addresses, and invalid out-of-range values.
5. Run a mock hardware workflow: scan, select device, program, verify mismatch, verify success, erase, and device disconnect during operation.
6. Hardware validation with an FX3 target: scan all CyUSB devices, select the intended board, read/probe via the standard bootloader protocol, program a known-good `.iic` image, verify read-back, power-cycle boot behavior, erase EEPROM, verify blank contents, and confirm the erased board no longer boots from that EEPROM image.
7. Repeat hardware validation with a non-default I2C address if the board wiring supports it.

## Decisions

- GUI stack: C#/.NET WPF.
- Hardware path: `CyUSB.dll` over USB to FX3, using standard Cypress FX3 bootloader/control-center protocol.
- Device selection: scan all CyUSB devices and require explicit user selection.
- Firmware formats: `.iic`, `.img`, and `.bin`; `.iic` is the primary programming format.
- EEPROM: default address `0x50`; initial target profile is Microchip AT24CM01; EEPROM size/settings should be adjustable later in the GUI.
- Erase behavior: full EEPROM erase with read-back verification.
- Safeguards: exact device selection, read-back verify after write, and I2C address validation.

## Further Considerations

1. `CyUSB.dll` compatibility should be confirmed before committing to modern .NET versus .NET Framework 4.8. If the DLL is the older CyUSB.NET assembly, .NET Framework 4.8 WPF is the safer first implementation target.
2. Standard FX3 bootloader support should be confirmed with a harmless protocol probe. If the connected FX3 is running custom firmware instead of the Cypress bootloader/control-center-compatible path, the app will need the board's custom vendor command specification.
3. `.img` and `.bin` support needs strict validation because not every raw image is EEPROM-bootable. The app should prevent accidental programming of a file that is loadable but not bootable.