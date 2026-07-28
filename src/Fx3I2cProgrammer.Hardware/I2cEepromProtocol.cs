using System;

namespace Fx3I2cProgrammer.Hardware
{
    /// <summary>
    /// Configuration for the low-level FX3 bootloader I2C EEPROM vendor command used by the
    /// Verify-Only and Erase paths.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: These values follow the common Cypress FX3 I2C-EEPROM programming convention
    /// (vendor request 0xBA, EEPROM byte offset in wValue, I2C slave address in wIndex). They MUST
    /// be confirmed against your board/firmware before trusting the Erase path on production
    /// hardware — an incorrect command can leave the EEPROM in an unexpected state. The high-level
    /// Program+Verify path does NOT use these values; it uses the Cypress SDK's own
    /// <c>DownloadFw</c> implementation.
    /// </remarks>
    public sealed class I2cEepromProtocol
    {
        /// <summary>Vendor bRequest used to read/write the I2C EEPROM. Default 0xBA.</summary>
        public byte VendorRequest { get; set; } = 0xBA;

        /// <summary>
        /// Maximum number of bytes to move per control transfer during read-back. Writes are always
        /// limited to the EEPROM page size regardless of this value.
        /// </summary>
        public int MaxReadChunkBytes { get; set; } = 64;

        /// <summary>
        /// Milliseconds to wait after each page write for the EEPROM's internal write cycle (tWR).
        /// AT24Cxx parts specify up to 5 ms; 6 ms is a safe default.
        /// </summary>
        public int PageWriteDelayMs { get; set; } = 6;

        /// <summary>Control-transfer timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 5000;

        public static I2cEepromProtocol Default => new I2cEepromProtocol();
    }
}
