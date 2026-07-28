namespace Fx3I2cProgrammer.Core.Firmware
{
    /// <summary>
    /// Recognised firmware container formats.
    /// </summary>
    public enum FirmwareFormat
    {
        /// <summary>Unknown / unsupported extension.</summary>
        Unknown = 0,

        /// <summary>
        /// Cypress FX3 I2C EEPROM image (<c>.iic</c>). The preferred programming format; it
        /// carries the boot header and checksums that the FX3 ROM expects when booting from I2C.
        /// </summary>
        Iic,

        /// <summary>
        /// Cypress FX3 firmware image (<c>.img</c>). Begins with the ASCII "CY" signature.
        /// Bootable from RAM/EEPROM/SPI depending on the header's boot options.
        /// </summary>
        Img,

        /// <summary>
        /// Raw binary payload (<c>.bin</c>). No header/verification; programmed verbatim.
        /// </summary>
        Bin
    }
}
