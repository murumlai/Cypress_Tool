using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fx3I2cProgrammer.Core.Abstractions;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Tests.Doubles
{
    /// <summary>In-memory operation log that records entries for assertions.</summary>
    public sealed class RecordingLog : IOperationLog
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        public void Log(LogLevel level, string message) => Entries.Add((level, message));

        public bool Has(LogLevel level) => Entries.Exists(e => e.Level == level);
    }

    /// <summary>Returns a preset list of devices from a "scan".</summary>
    public sealed class FakeDeviceEnumerator : IUsbDeviceEnumerator
    {
        private readonly IReadOnlyList<UsbDeviceInfo> _devices;

        public FakeDeviceEnumerator(params UsbDeviceInfo[] devices) => _devices = devices;

        public int ScanCount { get; private set; }

        public IReadOnlyList<UsbDeviceInfo> ScanDevices()
        {
            ScanCount++;
            return _devices;
        }
    }

    /// <summary>
    /// A software-only <see cref="IFx3Programmer"/> backed by a byte array standing in for the
    /// EEPROM. Supports simulating a verify mismatch and a mid-operation device disconnect.
    /// </summary>
    public sealed class MockFx3Programmer : IFx3Programmer
    {
        private byte[] _eeprom;

        public MockFx3Programmer(int capacityBytes, byte blankByte = 0xFF)
        {
            _eeprom = new byte[capacityBytes];
            for (int i = 0; i < _eeprom.Length; i++)
            {
                _eeprom[i] = blankByte;
            }
        }

        /// <summary>When set, the next operation throws to simulate a disconnect.</summary>
        public bool SimulateDisconnect { get; set; }

        /// <summary>When true, verify reports a mismatch even if contents match.</summary>
        public bool ForceVerifyMismatch { get; set; }

        public byte[] Snapshot() => (byte[])_eeprom.Clone();

        public Task<ProgrammingResult> ProbeAsync(ProgrammingOptions options, IOperationLog log, CancellationToken cancellationToken)
        {
            ThrowIfDisconnected();
            log.Info("Probe: bootloader running = " + options.Device.IsBootloaderRunning);
            return Task.FromResult(ProgrammingResult.Succeeded(OperationKind.Probing, 0, TimeSpan.Zero, "Probe OK"));
        }

        public Task<ProgrammingResult> ProgramAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            ThrowIfDisconnected();
            Array.Copy(image.Payload, _eeprom, image.Length);
            progress?.Report(new OperationProgress(OperationKind.Programming, image.Length, image.Length, "written"));

            if (options.VerifyAfterWrite)
            {
                return VerifyAsync(options, image, progress, log, cancellationToken);
            }

            return Task.FromResult(ProgrammingResult.Succeeded(OperationKind.Programming, image.Length, TimeSpan.Zero));
        }

        public Task<ProgrammingResult> VerifyAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            ThrowIfDisconnected();

            for (int i = 0; i < image.Length; i++)
            {
                byte expected = image.Payload[i];
                byte actual = ForceVerifyMismatch && i == 0 ? (byte)(expected ^ 0xFF) : _eeprom[i];
                if (actual != expected)
                {
                    return Task.FromResult(ProgrammingResult.VerifyMismatch(OperationKind.Verifying, i, expected, actual, TimeSpan.Zero));
                }
            }

            progress?.Report(new OperationProgress(OperationKind.Verifying, image.Length, image.Length, "verified"));
            return Task.FromResult(ProgrammingResult.Succeeded(OperationKind.Verifying, image.Length, TimeSpan.Zero));
        }

        public Task<ProgrammingResult> EraseAsync(
            ProgrammingOptions options,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            ThrowIfDisconnected();

            byte blank = options.Profile.BlankByte;
            for (int i = 0; i < _eeprom.Length; i++)
            {
                _eeprom[i] = blank;
            }

            progress?.Report(new OperationProgress(OperationKind.Erasing, _eeprom.Length, _eeprom.Length, "erased"));
            return Task.FromResult(ProgrammingResult.Succeeded(OperationKind.Erasing, _eeprom.Length, TimeSpan.Zero));
        }

        private void ThrowIfDisconnected()
        {
            if (SimulateDisconnect)
            {
                throw new InvalidOperationException("Device disconnected during operation.");
            }
        }
    }
}
