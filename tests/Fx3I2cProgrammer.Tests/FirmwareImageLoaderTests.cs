using System;
using Fx3I2cProgrammer.Core.Firmware;
using Xunit;

namespace Fx3I2cProgrammer.Tests
{
    public class FirmwareImageLoaderTests
    {
        [Theory]
        [InlineData("boot.iic", FirmwareFormat.Iic)]
        [InlineData("BOOT.IIC", FirmwareFormat.Iic)]
        [InlineData("fw.img", FirmwareFormat.Img)]
        [InlineData("payload.bin", FirmwareFormat.Bin)]
        [InlineData("notes.txt", FirmwareFormat.Unknown)]
        [InlineData("noext", FirmwareFormat.Unknown)]
        public void DetectFormat_MapsExtensions(string name, FirmwareFormat expected)
        {
            Assert.Equal(expected, FirmwareImageLoader.DetectFormat(name));
        }

        [Fact]
        public void HasCypressSignature_TrueForCyPrefix()
        {
            byte[] withSig = { 0x43, 0x59, 0x00, 0x01 }; // 'C','Y'
            byte[] withoutSig = { 0x00, 0x01, 0x02 };

            Assert.True(FirmwareImageLoader.HasCypressSignature(withSig));
            Assert.False(FirmwareImageLoader.HasCypressSignature(withoutSig));
            Assert.False(FirmwareImageLoader.HasCypressSignature(new byte[] { 0x43 }));
        }

        [Fact]
        public void Analyze_ValidImg_WithSignature_HasNoWarning()
        {
            byte[] payload = { 0x43, 0x59, 0xB0, 0x01, 0xFF, 0xEE };
            FirmwareImage image = FirmwareImageLoader.Analyze("fw.img", payload);

            Assert.Equal(FirmwareFormat.Img, image.Format);
            Assert.True(image.HasCypressSignature);
            Assert.Equal(6, image.Length);
            Assert.DoesNotContain("WARNING", image.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_Img_MissingSignature_Warns()
        {
            byte[] payload = { 0x00, 0x11, 0x22, 0x33 };
            FirmwareImage image = FirmwareImageLoader.Analyze("fw.img", payload);

            Assert.False(image.HasCypressSignature);
            Assert.Contains("WARNING", image.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_Iic_IsPreferredFormat()
        {
            byte[] payload = { 0xB2, 0x00, 0x01, 0x02, 0x03 };
            FirmwareImage image = FirmwareImageLoader.Analyze("boot.iic", payload);

            Assert.Equal(FirmwareFormat.Iic, image.Format);
            Assert.DoesNotContain("WARNING", image.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_Bin_Warns_RawPayload()
        {
            byte[] payload = { 0x01, 0x02, 0x03 };
            FirmwareImage image = FirmwareImageLoader.Analyze("payload.bin", payload);

            Assert.Equal(FirmwareFormat.Bin, image.Format);
            Assert.Contains("verbatim", image.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_UnknownExtension_Warns()
        {
            byte[] payload = { 0x01, 0x02 };
            FirmwareImage image = FirmwareImageLoader.Analyze("mystery.dat", payload);

            Assert.Equal(FirmwareFormat.Unknown, image.Format);
            Assert.Contains("WARNING", image.Notes, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Analyze_EmptyPayload_Throws()
        {
            Assert.Throws<FirmwareLoadException>(() => FirmwareImageLoader.Analyze("empty.bin", Array.Empty<byte>()));
        }
    }
}
