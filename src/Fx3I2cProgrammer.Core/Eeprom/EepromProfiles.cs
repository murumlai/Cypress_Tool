using System.Collections.Generic;

namespace Fx3I2cProgrammer.Core.Eeprom
{
    /// <summary>
    /// Built-in catalog of EEPROM profiles. The AT24CM01 is the default target; additional
    /// parts can be added here, and the UI can create fully custom profiles at runtime.
    /// </summary>
    public static class EepromProfiles
    {
        /// <summary>
        /// Microchip AT24CM01: 1 Mbit (128 KiB) I2C EEPROM, 256-byte page write buffer.
        /// The 17th address bit (A16) is folded into the slave address, giving two 64 KiB banks.
        /// </summary>
        public static EepromProfile At24Cm01 { get; } = new EepromProfile(
            name: "Microchip AT24CM01 (1 Mbit)",
            capacityBytes: 128 * 1024,
            pageSizeBytes: 256,
            addressingMode: EepromAddressingMode.TwoByteWithBankBits,
            bankSizeBytes: 64 * 1024,
            blankByte: 0xFF);

        /// <summary>Microchip AT24C512 / 24LC512: 512 Kbit (64 KiB), 128-byte page, 16-bit address.</summary>
        public static EepromProfile At24C512 { get; } = new EepromProfile(
            name: "Microchip AT24C512 (512 Kbit)",
            capacityBytes: 64 * 1024,
            pageSizeBytes: 128,
            addressingMode: EepromAddressingMode.TwoByte,
            blankByte: 0xFF);

        /// <summary>Microchip AT24C256: 256 Kbit (32 KiB), 64-byte page, 16-bit address.</summary>
        public static EepromProfile At24C256 { get; } = new EepromProfile(
            name: "Microchip AT24C256 (256 Kbit)",
            capacityBytes: 32 * 1024,
            pageSizeBytes: 64,
            addressingMode: EepromAddressingMode.TwoByte,
            blankByte: 0xFF);

        /// <summary>Microchip AT24C128: 128 Kbit (16 KiB), 64-byte page, 16-bit address.</summary>
        public static EepromProfile At24C128 { get; } = new EepromProfile(
            name: "Microchip AT24C128 (128 Kbit)",
            capacityBytes: 16 * 1024,
            pageSizeBytes: 64,
            addressingMode: EepromAddressingMode.TwoByte,
            blankByte: 0xFF);

        /// <summary>The profile selected by default when the application starts.</summary>
        public static EepromProfile Default => At24Cm01;

        /// <summary>All built-in profiles, in a sensible display order.</summary>
        public static IReadOnlyList<EepromProfile> BuiltIn { get; } = new List<EepromProfile>
        {
            At24Cm01,
            At24C512,
            At24C256,
            At24C128
        };
    }
}
