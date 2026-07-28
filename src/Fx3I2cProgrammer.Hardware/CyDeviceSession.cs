using System;
using CyUSB;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Hardware
{
    /// <summary>
    /// Owns a live <see cref="USBDeviceList"/> and the resolved target device for the duration of an
    /// operation. Disposing releases the unmanaged device handles.
    /// </summary>
    internal sealed class CyDeviceSession : IDisposable
    {
        private USBDeviceList _list;

        private CyDeviceSession(USBDeviceList list, CyUSBDevice device)
        {
            _list = list;
            Device = device;
            Fx3 = device as CyFX3Device;
        }

        /// <summary>The resolved CyUSB device.</summary>
        public CyUSBDevice Device { get; }

        /// <summary>The device cast to <see cref="CyFX3Device"/>, or null if it is not an FX3.</summary>
        public CyFX3Device Fx3 { get; }

        /// <summary>
        /// Opens a fresh device list and resolves the device that matches the supplied descriptor.
        /// Matching prefers a serial-number match, then falls back to VID/PID + list index.
        /// </summary>
        public static CyDeviceSession Open(UsbDeviceInfo target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var list = new USBDeviceList(CyConst.DEVICES_CYUSB);
            try
            {
                CyUSBDevice match = Resolve(list, target);
                if (match == null)
                {
                    list.Dispose();
                    throw new DeviceNotFoundException(
                        "The selected device is no longer present. Re-scan and select it again.");
                }

                return new CyDeviceSession(list, match);
            }
            catch
            {
                list.Dispose();
                throw;
            }
        }

        private static CyUSBDevice Resolve(USBDeviceList list, UsbDeviceInfo target)
        {
            CyUSBDevice indexFallback = null;

            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is CyUSBDevice dev))
                {
                    continue;
                }

                bool vidPidMatch = dev.VendorID == target.VendorId && dev.ProductID == target.ProductId;

                if (vidPidMatch
                    && !string.IsNullOrEmpty(target.SerialNumber)
                    && string.Equals(dev.SerialNumber, target.SerialNumber, StringComparison.Ordinal))
                {
                    return dev; // strongest match
                }

                if (vidPidMatch && i == target.DeviceIndex)
                {
                    indexFallback = dev;
                }
            }

            return indexFallback;
        }

        public void Dispose()
        {
            _list?.Dispose();
            _list = null;
        }
    }
}
