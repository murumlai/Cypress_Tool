using System;
using System.Collections.Generic;
using System.Globalization;
using Fx3I2cProgrammer.Core.Validation;

namespace Fx3I2cProgrammer.Core.Eeprom
{
    /// <summary>
    /// Describes the electrical/organisational characteristics of an I2C EEPROM so that the
    /// programming and erase workflows can chunk transfers correctly. Every setting is a plain
    /// value so profiles can be created/edited from the UI without code changes.
    /// </summary>
    public sealed class EepromProfile
    {
        public EepromProfile(
            string name,
            int capacityBytes,
            int pageSizeBytes,
            EepromAddressingMode addressingMode,
            int bankSizeBytes = 0,
            byte blankByte = 0xFF)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Custom" : name.Trim();
            CapacityBytes = capacityBytes;
            PageSizeBytes = pageSizeBytes;
            AddressingMode = addressingMode;
            // For non-banked modes the whole device is a single bank.
            BankSizeBytes = addressingMode == EepromAddressingMode.TwoByteWithBankBits
                ? (bankSizeBytes > 0 ? bankSizeBytes : capacityBytes)
                : capacityBytes;
            BlankByte = blankByte;
        }

        /// <summary>Human readable profile name, e.g. "Microchip AT24CM01".</summary>
        public string Name { get; }

        /// <summary>Total addressable size in bytes.</summary>
        public int CapacityBytes { get; }

        /// <summary>Maximum number of bytes writable in a single page-write burst.</summary>
        public int PageSizeBytes { get; }

        /// <summary>How offsets are encoded on the wire.</summary>
        public EepromAddressingMode AddressingMode { get; }

        /// <summary>
        /// Number of bytes addressable before the high address bit(s) roll into the slave address.
        /// Equal to <see cref="CapacityBytes"/> for non-banked modes.
        /// </summary>
        public int BankSizeBytes { get; }

        /// <summary>The value an erased/blank cell reads back as. Almost always 0xFF.</summary>
        public byte BlankByte { get; }

        /// <summary>
        /// Validates that the profile numbers are internally consistent.
        /// </summary>
        public ValidationResult Validate()
        {
            if (CapacityBytes <= 0)
            {
                return ValidationResult.Fail("EEPROM capacity must be greater than zero.");
            }

            if (PageSizeBytes <= 0)
            {
                return ValidationResult.Fail("EEPROM page size must be greater than zero.");
            }

            if (PageSizeBytes > CapacityBytes)
            {
                return ValidationResult.Fail("Page size cannot exceed the EEPROM capacity.");
            }

            if (CapacityBytes % PageSizeBytes != 0)
            {
                return ValidationResult.Fail(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "EEPROM capacity ({0}) must be a whole number of pages of {1} bytes.",
                        CapacityBytes,
                        PageSizeBytes));
            }

            if (BankSizeBytes <= 0 || BankSizeBytes > CapacityBytes)
            {
                return ValidationResult.Fail("Bank size must be greater than zero and no larger than the capacity.");
            }

            if (CapacityBytes % BankSizeBytes != 0)
            {
                return ValidationResult.Fail("EEPROM capacity must be a whole number of banks.");
            }

            if (BankSizeBytes % PageSizeBytes != 0)
            {
                return ValidationResult.Fail("Bank size must be a whole number of pages.");
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Splits a byte range into transfer chunks that never cross a page boundary or a bank
        /// boundary. This is the core rule for both programming (page writes) and read-back.
        /// </summary>
        /// <param name="startOffset">Absolute start offset within the EEPROM.</param>
        /// <param name="length">Number of bytes to cover.</param>
        public IEnumerable<EepromChunk> EnumerateChunks(int startOffset, int length)
        {
            if (startOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Start offset cannot be negative.");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length cannot be negative.");
            }

            if (checked(startOffset + length) > CapacityBytes)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Range [0x{0:X}, 0x{1:X}) exceeds EEPROM capacity of {2} bytes.",
                        startOffset,
                        startOffset + length,
                        CapacityBytes),
                    nameof(length));
            }

            int offset = startOffset;
            int remaining = length;

            while (remaining > 0)
            {
                int bankIndex = offset / BankSizeBytes;
                int wordAddress = offset % BankSizeBytes;

                int bankBoundary = (bankIndex + 1) * BankSizeBytes;
                int pageBoundary = ((offset / PageSizeBytes) + 1) * PageSizeBytes;

                int nextBoundary = Math.Min(bankBoundary, pageBoundary);
                int chunkLength = Math.Min(remaining, nextBoundary - offset);

                yield return new EepromChunk(offset, chunkLength, bankIndex, wordAddress);

                offset += chunkLength;
                remaining -= chunkLength;
            }
        }

        public override string ToString() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1} bytes, page {2}, {3})",
                Name,
                CapacityBytes,
                PageSizeBytes,
                AddressingMode);
    }
}
