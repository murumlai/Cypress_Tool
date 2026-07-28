using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fx3I2cProgrammer.Core.Abstractions;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Core.Workflow
{
    /// <summary>
    /// Coordinates device scanning and the program / verify / erase operations, applying the
    /// preflight safety checks before delegating to the hardware layer. This orchestration lives in
    /// Core (not the UI) so it can be unit tested against a mock <see cref="IFx3Programmer"/>.
    /// </summary>
    public sealed class ProgrammingWorkflow
    {
        private readonly IUsbDeviceEnumerator _enumerator;
        private readonly IFx3Programmer _programmer;
        private readonly IOperationLog _log;

        public ProgrammingWorkflow(
            IUsbDeviceEnumerator enumerator,
            IFx3Programmer programmer,
            IOperationLog log)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _programmer = programmer ?? throw new ArgumentNullException(nameof(programmer));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Performs a fresh device scan.</summary>
        public IReadOnlyList<UsbDeviceInfo> Scan()
        {
            _log.Info("Scanning for CyUSB devices...");
            IReadOnlyList<UsbDeviceInfo> devices = _enumerator.ScanDevices();
            _log.Info(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Found {0} CyUSB device(s).",
                devices.Count));
            return devices;
        }

        /// <summary>Harmless bootloader/status probe of the selected device.</summary>
        public Task<ProgrammingResult> ProbeAsync(ProgrammingOptions options, CancellationToken cancellationToken)
        {
            PreflightReport preflight = OperationPreflight.CheckCommon(options);
            ProgrammingResult blocked = ReportPreflight(OperationKind.Probing, preflight);
            if (blocked != null)
            {
                return Task.FromResult(blocked);
            }

            return RunGuardedAsync(
                OperationKind.Probing,
                () => _programmer.ProbeAsync(options, _log, cancellationToken));
        }

        /// <summary>Programs the image and optionally verifies, after preflight checks.</summary>
        public Task<ProgrammingResult> ProgramAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            PreflightReport preflight = OperationPreflight.CheckProgramOrVerify(options, image);
            ProgrammingResult blocked = ReportPreflight(OperationKind.Programming, preflight);
            if (blocked != null)
            {
                return Task.FromResult(blocked);
            }

            return RunGuardedAsync(
                OperationKind.Programming,
                () => _programmer.ProgramAsync(options, image, progress, _log, cancellationToken));
        }

        /// <summary>Reads back and compares against the image, after preflight checks.</summary>
        public Task<ProgrammingResult> VerifyAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            PreflightReport preflight = OperationPreflight.CheckProgramOrVerify(options, image);
            ProgrammingResult blocked = ReportPreflight(OperationKind.Verifying, preflight);
            if (blocked != null)
            {
                return Task.FromResult(blocked);
            }

            return RunGuardedAsync(
                OperationKind.Verifying,
                () => _programmer.VerifyAsync(options, image, progress, _log, cancellationToken));
        }

        /// <summary>Blanks the EEPROM and verifies it reads back blank, after preflight checks.</summary>
        public Task<ProgrammingResult> EraseAsync(
            ProgrammingOptions options,
            IProgress<OperationProgress> progress,
            CancellationToken cancellationToken)
        {
            PreflightReport preflight = OperationPreflight.CheckErase(options);
            ProgrammingResult blocked = ReportPreflight(OperationKind.Erasing, preflight);
            if (blocked != null)
            {
                return Task.FromResult(blocked);
            }

            return RunGuardedAsync(
                OperationKind.Erasing,
                () => _programmer.EraseAsync(options, progress, _log, cancellationToken));
        }

        /// <summary>
        /// Logs preflight warnings, and returns a failed result when there are blocking errors
        /// (or null when the operation may proceed).
        /// </summary>
        private ProgrammingResult ReportPreflight(OperationKind kind, PreflightReport preflight)
        {
            foreach (string warning in preflight.Warnings)
            {
                _log.Warning(warning);
            }

            if (preflight.CanProceed)
            {
                return null;
            }

            foreach (string error in preflight.Errors)
            {
                _log.Error(error);
            }

            return ProgrammingResult.Failed(kind, "Preflight checks failed. Resolve the errors above and retry.");
        }

        /// <summary>
        /// Executes a hardware operation, converting cancellation and unexpected exceptions
        /// (e.g. a device disconnect mid-operation) into a failed <see cref="ProgrammingResult"/>.
        /// </summary>
        private async Task<ProgrammingResult> RunGuardedAsync(OperationKind kind, Func<Task<ProgrammingResult>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _log.Warning(kind + " was cancelled.");
                return ProgrammingResult.Failed(kind, kind + " was cancelled by the operator.");
            }
            catch (Exception ex)
            {
                _log.Error(kind + " failed: " + ex.Message);
                return ProgrammingResult.Failed(kind, ex.Message);
            }
        }
    }
}
