# Cypress FX3 I2C EEPROM Programmer

A Windows WPF utility that uses the Cypress `CyUSB.dll` to discover FX3 USB devices,
program FX3 boot firmware into an I2C EEPROM, verify the write, and safely erase the
EEPROM. The default EEPROM target is the Microchip **AT24CM01**, and the I2C address,
EEPROM size, page size, and addressing mode are all editable in the UI.

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/Fx3I2cProgrammer.Core` | Pure logic: models, EEPROM profiles + chunking, firmware loading/validation, I2C address validation, preflight checks, and the programming workflow. No hardware or UI dependencies. |
| `src/Fx3I2cProgrammer.Hardware` | CyUSB wrapper: device enumeration and the FX3 bootloader client (program/verify/erase) over `CyUSB.dll`. Builds **x86**. |
| `src/Fx3I2cProgrammer.App` | WPF (MVVM) desktop UI. Builds **x86**, output `Fx3I2cProgrammer.exe`. |
| `tests/Fx3I2cProgrammer.Tests` | xUnit unit tests for parsing, chunking, validation, and workflow state (mock hardware). |
| `tools/` | Reflection helper scripts used to inspect `CyUSB.dll`. |

## Prerequisites

- **Windows 10 or 11.**
- **.NET Framework 4.8 runtime** (already present on current Windows).
- **.NET SDK** (for building) — or Visual Studio 2022/newer with the *.NET desktop development* workload.
- **Cypress USB driver** bound to the FX3 device (the CyUSB3 / CyUSB driver from the
  EZ-USB FX3 SDK / Control Center). Without the driver, no devices will be enumerated.
- **`CyUSB.dll`** — the Cypress .NET wrapper assembly. It ships at the repository root and
  is a .NET 2.0, **x86** assembly, which is why the Hardware and App projects build x86.

### DLL placement

- For building, the Hardware project references `..\..\CyUSB.dll` (repo root).
- At runtime, `CyUSB.dll` is copied next to `Fx3I2cProgrammer.exe` automatically by the build.
- If you relocate the executable, keep `CyUSB.dll` in the **same folder** as the `.exe`.

## Building

```powershell
# From the repository root
dotnet build CypressFx3I2cProgrammer.slnx -c Release
```

Output: `src/Fx3I2cProgrammer.App/bin/Release/Fx3I2cProgrammer.exe` (32-bit), with
`CyUSB.dll` and the supporting assemblies alongside it.

To build only the app, or with Visual Studio's MSBuild:

```powershell
dotnet build src\Fx3I2cProgrammer.App\Fx3I2cProgrammer.App.csproj -c Release
```

## Running the tests

```powershell
dotnet test tests\Fx3I2cProgrammer.Tests\Fx3I2cProgrammer.Tests.csproj -c Debug
```

The tests cover firmware format detection/validation, AT24CM01 page/bank chunking,
I2C address validation, and a mock program → verify (mismatch and success) → erase →
disconnect workflow.

## Operator workflow

1. **Scan & select the device.** Click **Refresh** to enumerate all CyUSB devices, then
   pick the intended board. A device must be selected in the current session before any
   write or erase; the selection is intentionally cleared on every re-scan.
2. **Probe (recommended).** Click **Probe** to open the device and confirm the standard
   Cypress bootloader is running. This is a harmless read-only check.
3. **Load firmware.** Click **Browse...** and choose a `.iic`, `.img`, or `.bin` file.
   The summary line and log report the detected format and any warnings.
4. **Set the EEPROM parameters.** Choose a profile (AT24CM01 by default) or `Custom...`,
   and adjust the I2C address (default `0x50`), capacity, page size, addressing mode, and
   bank size as needed.
5. **Program + Verify.** Writes the image to the EEPROM using the Cypress `DownloadFw`
   I2C-EEPROM path, then (when *Verify after write* is checked) reads it back and compares.
6. **Verify Only.** Reads the EEPROM back and compares against the loaded image without
   writing.
7. **Erase EEPROM.** After a confirmation prompt, overwrites the whole configured range
   with the profile's blank byte (`0xFF`) and verifies it reads back blank.
8. **Cancel** stops a running operation at the next safe boundary.

Last used I2C address, EEPROM profile, firmware directory, and the *verify* preference are
persisted to `%AppData%\Fx3I2cProgrammer\settings.xml`. The selected USB device is **never**
persisted.

## Firmware formats

- **`.iic`** — Cypress FX3 I2C EEPROM image; the preferred EEPROM boot format.
- **`.img`** — FX3 firmware image; expected to start with the `CY` boot signature.
- **`.bin`** — raw payload, written verbatim with no header validation (use with care).

## Safety notes

- **Program + Verify** uses the Cypress SDK's own `CyFX3Device.DownloadFw(..., I2CE2PROM)`
  implementation — the standard, control-center-compatible path.
- **Verify Only** and **Erase** use low-level vendor control transfers (default vendor
  request `0xBA`, EEPROM offset in `wValue`, I2C slave address in `wIndex`). These follow
  the common FX3 I2C-EEPROM convention but **must be validated against your board** before
  being relied upon for destructive operations. See
  [`I2cEepromProtocol`](src/Fx3I2cProgrammer.Hardware/I2cEepromProtocol.cs).
- Destructive actions require an explicit current-session device selection, a valid 7-bit
  I2C address, and (for erase) a confirmation dialog.

## Hardware validation checklist

1. Scan all CyUSB devices and select the intended FX3 board.
2. Probe and confirm the bootloader is detected.
3. Program a known-good `.iic` image and confirm read-back verification passes.
4. Power-cycle and confirm the board boots from the EEPROM image.
5. Erase the EEPROM and confirm it reads back blank and no longer boots that image.
6. Repeat with a non-default I2C address if the board wiring supports it.
