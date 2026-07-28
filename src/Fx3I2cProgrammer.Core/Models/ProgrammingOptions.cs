using System;
using Fx3I2cProgrammer.Core.Eeprom;
using Fx3I2cProgrammer.Core.Validation;

namespace Fx3I2cProgrammer.Core.Models
{
    /// <summary>
    /// All parameters required to run a program / verify / erase operation. Immutable so a running
    /// operation cannot have its target changed underneath it.
    /// </summary>
    public sealed class ProgrammingOptions
    {
        public ProgrammingOptions(
            UsbDeviceInfo device,
            I2cAddress address,
            EepromProfile profile,
            bool verifyAfterWrite)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Address = address;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            VerifyAfterWrite = verifyAfterWrite;
        }

        /// <summary>The explicitly selected target device from the current session's scan.</summary>
        public UsbDeviceInfo Device { get; }

        /// <summary>7-bit I2C slave address of the EEPROM (default 0x50).</summary>
        public I2cAddress Address { get; }

        /// <summary>EEPROM characteristics used for chunking and blank-verification.</summary>
        public EepromProfile Profile { get; }

        /// <summary>When true, the write is followed by a full read-back comparison.</summary>
        public bool VerifyAfterWrite { get; }
    }
}
