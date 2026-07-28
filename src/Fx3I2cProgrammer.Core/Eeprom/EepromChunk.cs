using System.Globalization;

namespace Fx3I2cProgrammer.Core.Eeprom
{
    /// <summary>
    /// A single contiguous slice of an EEPROM operation that is guaranteed not to cross a
    /// page boundary (for writes) nor a bank boundary (for banked addressing modes).
    /// </summary>
    public readonly struct EepromChunk
    {
        public EepromChunk(int offset, int length, int bankIndex, int wordAddress)
        {
            Offset = offset;
            Length = length;
            BankIndex = bankIndex;
            WordAddress = wordAddress;
        }

        /// <summary>Absolute byte offset from the start of the EEPROM.</summary>
        public int Offset { get; }

        /// <summary>Number of bytes in this chunk.</summary>
        public int Length { get; }

        /// <summary>
        /// Zero-based bank index. For non-banked modes this is always 0. For banked modes the
        /// hardware layer typically adds this to the base slave address device-select bits.
        /// </summary>
        public int BankIndex { get; }

        /// <summary>
        /// Word address to send on the wire, i.e. the offset within the current bank.
        /// </summary>
        public int WordAddress { get; }

        public int EndOffsetExclusive => Offset + Length;

        public override string ToString() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "Chunk[off=0x{0:X}, len={1}, bank={2}, word=0x{3:X}]",
                Offset,
                Length,
                BankIndex,
                WordAddress);
    }
}
