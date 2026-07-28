using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Fx3I2cProgrammer.Core.Firmware
{
    /// <summary>
    /// Loads and lightly validates FX3 firmware images. Format detection and header analysis are
    /// pure functions (no file I/O) so they can be unit tested directly.
    /// </summary>
    public static class FirmwareImageLoader
    {
        /// <summary>The Cypress boot signature that prefixes <c>.img</c> images ("CY").</summary>
        private static readonly byte[] CypressSignature = { 0x43, 0x59 };

        /// <summary>
        /// Maps a file name's extension to a <see cref="FirmwareFormat"/>.
        /// </summary>
        public static FirmwareFormat DetectFormat(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return FirmwareFormat.Unknown;
            }

            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
            {
                return FirmwareFormat.Unknown;
            }

            switch (ext.ToLowerInvariant())
            {
                case ".iic":
                    return FirmwareFormat.Iic;
                case ".img":
                    return FirmwareFormat.Img;
                case ".bin":
                    return FirmwareFormat.Bin;
                default:
                    return FirmwareFormat.Unknown;
            }
        }

        /// <summary>
        /// Returns true when the payload starts with the Cypress "CY" signature.
        /// </summary>
        public static bool HasCypressSignature(byte[] payload)
        {
            if (payload == null || payload.Length < CypressSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < CypressSignature.Length; i++)
            {
                if (payload[i] != CypressSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Analyses raw bytes for a given file name, producing a <see cref="FirmwareImage"/> with
        /// format-appropriate validation notes. Throws <see cref="FirmwareLoadException"/> when the
        /// payload is unusable (e.g. empty).
        /// </summary>
        public static FirmwareImage Analyze(string fileName, byte[] payload)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length == 0)
            {
                throw new FirmwareLoadException("The firmware file is empty; nothing to program.");
            }

            FirmwareFormat format = DetectFormat(fileName);
            bool hasSignature = HasCypressSignature(payload);
            var notes = new StringBuilder();

            switch (format)
            {
                case FirmwareFormat.Iic:
                    notes.Append("Cypress FX3 I2C EEPROM image (.iic) — the preferred EEPROM boot format.");
                    break;

                case FirmwareFormat.Img:
                    if (hasSignature)
                    {
                        notes.Append("FX3 firmware image (.img) with valid 'CY' signature.");
                    }
                    else
                    {
                        notes.Append("WARNING: .img file does not begin with the 'CY' signature; ");
                        notes.Append("it may not be an FX3-bootable image. Verify before programming.");
                    }
                    break;

                case FirmwareFormat.Bin:
                    notes.Append("Raw binary (.bin) — programmed verbatim with no header validation. ");
                    notes.Append("Ensure this payload is actually EEPROM-bootable for your board.");
                    break;

                default:
                    notes.Append("WARNING: Unrecognised file extension. Supported types are .iic, .img and .bin. ");
                    notes.Append("The file will be treated as a raw payload.");
                    break;
            }

            return new FirmwareImage(fileName, format, payload, hasSignature, notes.ToString());
        }

        /// <summary>
        /// Reads a firmware file from disk and analyses it.
        /// </summary>
        public static FirmwareImage Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A firmware file path is required.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FirmwareLoadException(
                    string.Format(CultureInfo.InvariantCulture, "Firmware file not found: {0}", path));
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new FirmwareLoadException(
                    string.Format(CultureInfo.InvariantCulture, "Could not read firmware file: {0}", ex.Message),
                    ex);
            }

            return Analyze(path, bytes);
        }
    }
}
