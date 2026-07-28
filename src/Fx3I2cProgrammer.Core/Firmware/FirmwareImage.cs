using System;
using System.Globalization;

namespace Fx3I2cProgrammer.Core.Firmware
{
    /// <summary>
    /// A loaded firmware image plus the metadata the workflow needs to decide how to program it.
    /// </summary>
    public sealed class FirmwareImage
    {
        public FirmwareImage(
            string filePath,
            FirmwareFormat format,
            byte[] payload,
            bool hasCypressSignature,
            string notes)
        {
            FilePath = filePath ?? string.Empty;
            Format = format;
            Payload = payload ?? Array.Empty<byte>();
            HasCypressSignature = hasCypressSignature;
            Notes = notes ?? string.Empty;
        }

        /// <summary>Full path the image was loaded from.</summary>
        public string FilePath { get; }

        /// <summary>Detected container format.</summary>
        public FirmwareFormat Format { get; }

        /// <summary>Raw file bytes to be written to the EEPROM.</summary>
        public byte[] Payload { get; }

        /// <summary>Total number of bytes to program.</summary>
        public int Length => Payload.Length;

        /// <summary>
        /// True when the payload begins with the Cypress "CY" boot signature. This is a strong
        /// indication that the image is FX3-bootable. Raw <c>.bin</c> payloads without the
        /// signature are programmable but should be treated with caution.
        /// </summary>
        public bool HasCypressSignature { get; }

        /// <summary>Human readable validation notes/warnings gathered while loading.</summary>
        public string Notes { get; }

        public string DisplaySummary => string.Format(
            CultureInfo.InvariantCulture,
            "{0} — {1} bytes ({2}{3})",
            System.IO.Path.GetFileName(FilePath),
            Length,
            Format,
            HasCypressSignature ? ", CY signature" : string.Empty);
    }
}
