using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fx3I2cProgrammer.Core.Eeprom;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;
using Fx3I2cProgrammer.Core.Validation;
using Fx3I2cProgrammer.Core.Workflow;
using Fx3I2cProgrammer.Tests.Doubles;
using Xunit;

namespace Fx3I2cProgrammer.Tests
{
    public class WorkflowTests
    {
        private static UsbDeviceInfo MakeDevice(bool bootloader = true) =>
            new UsbDeviceInfo(0, 0x04B4, 0x00F3, "FX3", "Cypress", "SN123", "Cypress FX3", bootloader);

        private static ProgrammingOptions MakeOptions(EepromProfile profile, bool verify, bool bootloader = true) =>
            new ProgrammingOptions(MakeDevice(bootloader), I2cAddress.Default, profile, verify);

        private static FirmwareImage MakeImage(int length)
        {
            var payload = new byte[length];
            for (int i = 0; i < length; i++)
            {
                payload[i] = (byte)(i & 0xFF);
            }

            // Give it a CY signature so no format warnings interfere with assertions.
            if (length >= 2)
            {
                payload[0] = 0x43;
                payload[1] = 0x59;
            }

            return FirmwareImageLoader.Analyze("test.img", payload);
        }

        [Fact]
        public void Scan_ReturnsDevices_AndLogs()
        {
            var log = new RecordingLog();
            var enumerator = new FakeDeviceEnumerator(MakeDevice());
            var workflow = new ProgrammingWorkflow(enumerator, new MockFx3Programmer(1024), log);

            var devices = workflow.Scan();

            Assert.Single(devices);
            Assert.Equal(1, enumerator.ScanCount);
            Assert.Contains(log.Entries, e => e.Message.Contains("Found 1"));
        }

        [Fact]
        public async Task Program_WithVerify_Succeeds()
        {
            var log = new RecordingLog();
            var eeprom = new MockFx3Programmer(EepromProfiles.At24Cm01.CapacityBytes);
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), eeprom, log);
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: true);
            var image = MakeImage(4096);

            ProgrammingResult result = await workflow.ProgramAsync(options, image, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(OperationKind.Verifying, result.Kind); // program chained into verify
            Assert.True(eeprom.Snapshot().Take(image.Length).SequenceEqual(image.Payload));
        }

        [Fact]
        public async Task Verify_Mismatch_IsReported()
        {
            var log = new RecordingLog();
            var eeprom = new MockFx3Programmer(EepromProfiles.At24Cm01.CapacityBytes) { ForceVerifyMismatch = true };
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), eeprom, log);
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: false);
            var image = MakeImage(256);

            ProgrammingResult result = await workflow.VerifyAsync(options, image, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(OperationKind.Verifying, result.Kind);
            Assert.Equal(0, result.MismatchOffset);
        }

        [Fact]
        public async Task Verify_Success_AfterProgram()
        {
            var log = new RecordingLog();
            var eeprom = new MockFx3Programmer(EepromProfiles.At24Cm01.CapacityBytes);
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), eeprom, log);
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: false);
            var image = MakeImage(512);

            await workflow.ProgramAsync(options, image, null, CancellationToken.None);
            ProgrammingResult verify = await workflow.VerifyAsync(options, image, null, CancellationToken.None);

            Assert.True(verify.Success);
        }

        [Fact]
        public async Task Erase_BlanksEeprom_AndSucceeds()
        {
            var log = new RecordingLog();
            var eeprom = new MockFx3Programmer(EepromProfiles.At24Cm01.CapacityBytes);
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), eeprom, log);
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: false);

            // Dirty the EEPROM first.
            await workflow.ProgramAsync(options, MakeImage(1024), null, CancellationToken.None);

            ProgrammingResult result = await workflow.EraseAsync(options, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.All(eeprom.Snapshot(), b => Assert.Equal(0xFF, b));
        }

        [Fact]
        public async Task Disconnect_DuringOperation_ReturnsFailure_NotThrow()
        {
            var log = new RecordingLog();
            var eeprom = new MockFx3Programmer(EepromProfiles.At24Cm01.CapacityBytes) { SimulateDisconnect = true };
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), eeprom, log);
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: false);

            ProgrammingResult result = await workflow.ProgramAsync(options, MakeImage(256), null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("disconnect", result.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.True(log.Has(LogLevel.Error));
        }

        [Fact]
        public async Task Program_WithoutDevice_BlockedByPreflight()
        {
            var log = new RecordingLog();
            var workflow = new ProgrammingWorkflow(new FakeDeviceEnumerator(), new MockFx3Programmer(1024), log);
            var badOptions = new ProgrammingOptions(
                new UsbDeviceInfo(0, 0, 0, "", "", "", "", true),
                new I2cAddress(0x50),
                EepromProfiles.At24Cm01,
                false);

            // Oversized image (bigger than capacity) should be blocked.
            var oversize = MakeImage(EepromProfiles.At24Cm01.CapacityBytes + 16);
            ProgrammingResult result = await workflow.ProgramAsync(badOptions, oversize, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.True(log.Has(LogLevel.Error));
        }

        [Fact]
        public void Preflight_NonBootloaderDevice_Warns()
        {
            var options = MakeOptions(EepromProfiles.At24Cm01, verify: false, bootloader: false);
            PreflightReport report = OperationPreflight.CheckCommon(options);

            Assert.True(report.CanProceed); // warning, not blocking
            Assert.NotEmpty(report.Warnings);
        }
    }
}
