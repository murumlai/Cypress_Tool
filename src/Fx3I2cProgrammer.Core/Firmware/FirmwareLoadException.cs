using System;
using System.Runtime.Serialization;

namespace Fx3I2cProgrammer.Core.Firmware
{
    /// <summary>
    /// Raised when a firmware file cannot be loaded or fails basic validation.
    /// </summary>
    [Serializable]
    public class FirmwareLoadException : Exception
    {
        public FirmwareLoadException()
        {
        }

        public FirmwareLoadException(string message)
            : base(message)
        {
        }

        public FirmwareLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected FirmwareLoadException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
