using System;
using System.Globalization;

namespace Fx3I2cProgrammer.Core.Validation
{
    /// <summary>
    /// Represents a validated 7-bit I2C slave address and provides parsing/validation
    /// for operator input. EEPROM devices such as the AT24CM01 default to <c>0x50</c>.
    /// </summary>
    public readonly struct I2cAddress : IEquatable<I2cAddress>
    {
        /// <summary>Lowest legal 7-bit address that is usable for an EEPROM (0x08..0x77 general call range excluded).</summary>
        public const byte MinValue = 0x00;

        /// <summary>Highest legal 7-bit address.</summary>
        public const byte MaxValue = 0x7F;

        /// <summary>Default EEPROM address used by AT24Cxx parts on most FX3 boards.</summary>
        public static readonly I2cAddress Default = new I2cAddress(0x50);

        public I2cAddress(byte value)
        {
            if (value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "A 7-bit I2C address must be in the range 0x00..0x7F.");
            }

            Value = value;
        }

        /// <summary>The raw 7-bit address value.</summary>
        public byte Value { get; }

        /// <summary>
        /// Validates a raw integer without throwing. Accepts the full 7-bit range 0x00..0x7F.
        /// </summary>
        public static ValidationResult Validate(int value)
        {
            if (value < MinValue || value > MaxValue)
            {
                return ValidationResult.Fail(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "I2C address 0x{0:X2} is out of range. Use a 7-bit address 0x00..0x7F.",
                        value & 0xFFFF));
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Parses operator text such as <c>0x50</c>, <c>50h</c>, <c>50</c> (hex) or a decimal value.
        /// Hexadecimal is assumed when the text has a <c>0x</c> prefix or an <c>h</c> suffix; a bare
        /// number is interpreted as hexadecimal because I2C addresses are conventionally written in hex.
        /// </summary>
        public static bool TryParse(string text, out I2cAddress address, out string error)
        {
            address = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Enter an I2C address, e.g. 0x50.";
                return false;
            }

            string trimmed = text.Trim();
            bool isHex = true;

            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(2);
            }
            else if (trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }
            else if (trimmed.StartsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                // Explicit decimal escape hatch, e.g. "d80".
                trimmed = trimmed.Substring(1);
                isHex = false;
            }

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (!int.TryParse(trimmed, style, CultureInfo.InvariantCulture, out int value))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is not a valid I2C address. Use hex like 0x50.",
                    text);
                return false;
            }

            ValidationResult range = Validate(value);
            if (!range.IsValid)
            {
                error = range.Message;
                return false;
            }

            address = new I2cAddress((byte)value);
            return true;
        }

        public bool Equals(I2cAddress other) => Value == other.Value;

        public override bool Equals(object obj) => obj is I2cAddress other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() =>
            "0x" + Value.ToString("X2", CultureInfo.InvariantCulture);
    }
}
