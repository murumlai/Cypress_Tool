using System;
using System.Threading;
using System.Threading.Tasks;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Core.Abstractions
{
    /// <summary>
    /// Hardware-facing contract for programming, verifying and erasing an FX3 I2C EEPROM.
    /// Implemented over CyUSB.dll in the Hardware layer, and by a mock in tests.
    /// </summary>
    public interface IFx3Programmer
    {
        /// <summary>
        /// Performs a harmless probe of the selected device (the Step-5 protocol spike): opens it and
        /// reads bootloader/status information without modifying anything.
        /// </summary>
        Task<ProgrammingResult> ProbeAsync(
            ProgrammingOptions options,
            IOperationLog log,
            CancellationToken cancellationToken);

        /// <summary>
        /// Writes the firmware image to the EEPROM and, when requested, reads it back to verify.
        /// </summary>
        Task<ProgrammingResult> ProgramAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken);

        /// <summary>
        /// Reads the EEPROM back and compares it against <paramref name="image"/> without writing.
        /// </summary>
        Task<ProgrammingResult> VerifyAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken);

        /// <summary>
        /// Overwrites the configured EEPROM range with the profile's blank byte, then verifies the
        /// range reads back blank.
        /// </summary>
        Task<ProgrammingResult> EraseAsync(
            ProgrammingOptions options,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken);
    }
}
