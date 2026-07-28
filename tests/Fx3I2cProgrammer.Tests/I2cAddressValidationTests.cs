using Fx3I2cProgrammer.Core.Validation;
using Xunit;

namespace Fx3I2cProgrammer.Tests
{
    public class I2cAddressValidationTests
    {
        [Fact]
        public void Default_Is_0x50()
        {
            Assert.Equal(0x50, I2cAddress.Default.Value);
            Assert.Equal("0x50", I2cAddress.Default.ToString());
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x50)]
        [InlineData(0x57)]
        [InlineData(0x7F)]
        public void Validate_Accepts_ValidSevenBitAddresses(int value)
        {
            Assert.True(I2cAddress.Validate(value).IsValid);
        }

        [Theory]
        [InlineData(0x80)]
        [InlineData(0xFF)]
        [InlineData(-1)]
        [InlineData(256)]
        public void Validate_Rejects_OutOfRangeAddresses(int value)
        {
            Assert.False(I2cAddress.Validate(value).IsValid);
        }

        [Theory]
        [InlineData("0x50", 0x50)]
        [InlineData("0X50", 0x50)]
        [InlineData("50", 0x50)]     // bare value parsed as hex by convention
        [InlineData("50h", 0x50)]
        [InlineData("7F", 0x7F)]
        [InlineData("d80", 0x50)]    // explicit decimal 80 == 0x50
        public void TryParse_Parses_ValidInput(string text, int expected)
        {
            Assert.True(I2cAddress.TryParse(text, out I2cAddress address, out _));
            Assert.Equal(expected, address.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0x80")]   // out of 7-bit range
        [InlineData("zz")]
        [InlineData("d200")]   // decimal 200 out of range
        public void TryParse_Rejects_InvalidInput(string text)
        {
            Assert.False(I2cAddress.TryParse(text, out _, out string error));
            Assert.False(string.IsNullOrEmpty(error));
        }
    }
}
