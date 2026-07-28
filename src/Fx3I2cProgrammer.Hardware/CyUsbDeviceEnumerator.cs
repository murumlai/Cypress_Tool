using System;
using System.Collections.Generic;
using CyUSB;
using Fx3I2cProgrammer.Core.Abstractions;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Hardware
{
    /// <summary>
    /// Enumerates attached CyUSB devices using CyUSB.dll. Each call performs a fresh scan of the
    /// <c>DEVICES_CYUSB</c> class so that indices reflect the current bus state.
    /// </summary>
    public sealed class CyUsbDeviceEnumerator : IUsbDeviceEnumerator
    {
        /// <inheritdoc />
        public IReadOnlyList<UsbDeviceInfo> ScanDevices()
        {
            var results = new List<UsbDeviceInfo>();

            // The device list owns unmanaged handles; dispose it as soon as we have snapshotted
            // the immutable details we care about.
            using (var list = new USBDeviceList(CyConst.DEVICES_CYUSB))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!(list[i] is CyUSBDevice dev))
                    {
                        continue;
                    }

                    bool bootloaderRunning = false;
                    if (dev is CyFX3Device fx3)
                    {
                        try
                        {
                            bootloaderRunning = fx3.IsBootLoaderRunning();
                        }
                        catch (Exception)
                        {
                            // A device that refuses the probe is reported as "not bootloader".
                            bootloaderRunning = false;
                        }
                    }

                    results.Add(new UsbDeviceInfo(
                        deviceIndex: i,
                        vendorId: dev.VendorID,
                        productId: dev.ProductID,
                        product: dev.Product,
                        manufacturer: dev.Manufacturer,
                        serialNumber: dev.SerialNumber,
                        friendlyName: dev.FriendlyName,
                        isBootloaderRunning: bootloaderRunning));
                }
            }

            return results;
        }
    }
}
