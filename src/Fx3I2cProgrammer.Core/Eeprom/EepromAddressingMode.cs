namespace Fx3I2cProgrammer.Core.Eeprom
{
    /// <summary>
    /// Describes how a byte offset is turned into an I2C word-address / slave-address pair
    /// when talking to an EEPROM.
    /// </summary>
    public enum EepromAddressingMode
    {
        /// <summary>
        /// Single 8-bit word address. Suitable for small EEPROMs up to 256 bytes.
        /// </summary>
        SingleByte,

        /// <summary>
        /// Two 8-bit word-address bytes (16-bit address). Suitable for EEPROMs up to 64 KiB.
        /// </summary>
        TwoByte,

        /// <summary>
        /// Two 8-bit word-address bytes plus one or more high address bits folded into the
        /// I2C slave address. Used by 1 Mbit+ parts such as the AT24CM01, where A16 selects
        /// one of two 64 KiB banks via the device-select bits of the slave address.
        /// </summary>
        TwoByteWithBankBits
    }
}
