using System.Collections.Generic;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Core.Abstractions
{
    /// <summary>
    /// Scans for CyUSB devices. Abstracted so the UI and tests do not depend on CyUSB.dll directly.
    /// </summary>
    public interface IUsbDeviceEnumerator
    {
        /// <summary>
        /// Enumerates all currently attached CyUSB devices. Each call performs a fresh scan; the
        /// returned <see cref="UsbDeviceInfo.DeviceIndex"/> values are only valid until the next scan.
        /// </summary>
        IReadOnlyList<UsbDeviceInfo> ScanDevices();
    }
}
