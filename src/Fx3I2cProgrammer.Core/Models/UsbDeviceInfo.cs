using System;
using System.Globalization;

namespace Fx3I2cProgrammer.Core.Models
{
    /// <summary>
    /// Immutable snapshot of a discovered CyUSB device. The <see cref="DeviceIndex"/> is the
    /// index within the current <c>USBDeviceList</c> and is only valid for the current session's
    /// enumeration; destructive operations must re-select from a fresh scan.
    /// </summary>
    public sealed class UsbDeviceInfo
    {
        public UsbDeviceInfo(
            int deviceIndex,
            ushort vendorId,
            ushort productId,
            string product,
            string manufacturer,
            string serialNumber,
            string friendlyName,
            bool isBootloaderRunning)
        {
            DeviceIndex = deviceIndex;
            VendorId = vendorId;
            ProductId = productId;
            Product = product ?? string.Empty;
            Manufacturer = manufacturer ?? string.Empty;
            SerialNumber = serialNumber ?? string.Empty;
            FriendlyName = friendlyName ?? string.Empty;
            IsBootloaderRunning = isBootloaderRunning;
        }

        /// <summary>Index into the current CyUSB device list.</summary>
        public int DeviceIndex { get; }

        public ushort VendorId { get; }

        public ushort ProductId { get; }

        public string Product { get; }

        public string Manufacturer { get; }

        public string SerialNumber { get; }

        public string FriendlyName { get; }

        /// <summary>True when the FX3 reports the standard Cypress bootloader is active.</summary>
        public bool IsBootloaderRunning { get; }

        /// <summary>VID:PID formatted as hex, e.g. "04B4:00F3".</summary>
        public string VidPid => string.Format(
            CultureInfo.InvariantCulture,
            "{0:X4}:{1:X4}",
            VendorId,
            ProductId);

        /// <summary>A friendly one-line description for list display.</summary>
        public string DisplayName
        {
            get
            {
                string label = !string.IsNullOrWhiteSpace(FriendlyName)
                    ? FriendlyName
                    : !string.IsNullOrWhiteSpace(Product)
                        ? Product
                        : "CyUSB Device";

                string serial = string.IsNullOrWhiteSpace(SerialNumber)
                    ? string.Empty
                    : " SN:" + SerialNumber;

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}] {1} ({2}){3}",
                    DeviceIndex,
                    label,
                    VidPid,
                    serial);
            }
        }

        public override string ToString() => DisplayName;
    }
}
