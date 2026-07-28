using System;
using System.Collections.Generic;
using System.Linq;
using Fx3I2cProgrammer.Core.Eeprom;
using Xunit;

namespace Fx3I2cProgrammer.Tests
{
    public class EepromChunkingTests
    {
        private static readonly EepromProfile At24Cm01 = EepromProfiles.At24Cm01;

        [Fact]
        public void At24Cm01_Profile_IsValid()
        {
            Assert.True(At24Cm01.Validate().IsValid);
            Assert.Equal(128 * 1024, At24Cm01.CapacityBytes);
            Assert.Equal(256, At24Cm01.PageSizeBytes);
            Assert.Equal(64 * 1024, At24Cm01.BankSizeBytes);
            Assert.Equal(EepromAddressingMode.TwoByteWithBankBits, At24Cm01.AddressingMode);
        }

        [Fact]
        public void Chunks_NeverCrossPageBoundary()
        {
            List<EepromChunk> chunks = At24Cm01.EnumerateChunks(0, At24Cm01.CapacityBytes).ToList();

            foreach (EepromChunk chunk in chunks)
            {
                int pageStart = chunk.Offset / At24Cm01.PageSizeBytes;
                int pageEnd = (chunk.EndOffsetExclusive - 1) / At24Cm01.PageSizeBytes;
                Assert.Equal(pageStart, pageEnd);
                Assert.True(chunk.Length <= At24Cm01.PageSizeBytes);
            }
        }

        [Fact]
        public void Chunks_AreContiguous_AndCoverWholeRange()
        {
            const int start = 0;
            int length = At24Cm01.CapacityBytes;
            List<EepromChunk> chunks = At24Cm01.EnumerateChunks(start, length).ToList();

            int cursor = start;
            foreach (EepromChunk chunk in chunks)
            {
                Assert.Equal(cursor, chunk.Offset);
                cursor += chunk.Length;
            }

            Assert.Equal(start + length, cursor);
            Assert.Equal(length, chunks.Sum(c => c.Length));
        }

        [Fact]
        public void FullDevice_ProducesExpectedChunkCount()
        {
            // 128 KiB / 256-byte pages = 512 page-aligned chunks.
            List<EepromChunk> chunks = At24Cm01.EnumerateChunks(0, At24Cm01.CapacityBytes).ToList();
            Assert.Equal(512, chunks.Count);
        }

        [Fact]
        public void PageUnalignedWrite_SplitsAtPageBoundary()
        {
            // Start 128 bytes into the first page, write 256 bytes => split 128 + 128.
            List<EepromChunk> chunks = At24Cm01.EnumerateChunks(128, 256).ToList();

            Assert.Equal(2, chunks.Count);
            Assert.Equal(128, chunks[0].Offset);
            Assert.Equal(128, chunks[0].Length);
            Assert.Equal(256, chunks[1].Offset);
            Assert.Equal(128, chunks[1].Length);
        }

        [Fact]
        public void WriteAcrossBankBoundary_SplitsBanks()
        {
            // 128 bytes below the 64 KiB bank boundary, 256 bytes long.
            int start = (64 * 1024) - 128;
            List<EepromChunk> chunks = At24Cm01.EnumerateChunks(start, 256).ToList();

            Assert.Equal(2, chunks.Count);

            Assert.Equal(0, chunks[0].BankIndex);
            Assert.Equal(start, chunks[0].Offset);
            Assert.Equal(128, chunks[0].Length);

            Assert.Equal(1, chunks[1].BankIndex);
            Assert.Equal(64 * 1024, chunks[1].Offset);
            Assert.Equal(0, chunks[1].WordAddress); // first word of bank 1
            Assert.Equal(128, chunks[1].Length);
        }

        [Fact]
        public void WordAddress_IsOffsetWithinBank()
        {
            // Offset in the second bank should report a word address relative to that bank.
            int offset = (64 * 1024) + 300; // bank 1, 300 bytes in
            EepromChunk first = At24Cm01.EnumerateChunks(offset, 16).First();

            Assert.Equal(1, first.BankIndex);
            Assert.Equal(300, first.WordAddress);
        }

        [Fact]
        public void TwoByteMode_OnlySplitsOnPages()
        {
            EepromProfile at24c512 = EepromProfiles.At24C512; // 64 KiB, single bank, 128-byte page
            List<EepromChunk> chunks = at24c512.EnumerateChunks(0, at24c512.CapacityBytes).ToList();

            Assert.All(chunks, c => Assert.Equal(0, c.BankIndex));
            Assert.All(chunks, c => Assert.True(c.Length <= at24c512.PageSizeBytes));
            Assert.Equal(at24c512.CapacityBytes / at24c512.PageSizeBytes, chunks.Count);
        }

        [Fact]
        public void RangeBeyondCapacity_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                At24Cm01.EnumerateChunks(At24Cm01.CapacityBytes - 10, 20).ToList());
        }

        [Fact]
        public void NegativeArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => At24Cm01.EnumerateChunks(-1, 10).ToList());
            Assert.Throws<ArgumentOutOfRangeException>(() => At24Cm01.EnumerateChunks(0, -10).ToList());
        }
    }
}
