using System;
using System.Runtime.Serialization;

namespace Fx3I2cProgrammer.Hardware
{
    /// <summary>Raised when the previously selected device can no longer be opened.</summary>
    [Serializable]
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException()
        {
        }

        public DeviceNotFoundException(string message)
            : base(message)
        {
        }

        public DeviceNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DeviceNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
