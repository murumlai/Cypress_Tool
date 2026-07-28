using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Models;
using Fx3I2cProgrammer.Core.Validation;

namespace Fx3I2cProgrammer.Core.Workflow
{
    /// <summary>
    /// Aggregated result of running preflight checks. Blocking errors prevent the operation from
    /// starting; warnings are surfaced but do not block.
    /// </summary>
    public sealed class PreflightReport
    {
        public PreflightReport(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = new ReadOnlyCollection<string>((errors ?? Enumerable.Empty<string>()).ToList());
            Warnings = new ReadOnlyCollection<string>((warnings ?? Enumerable.Empty<string>()).ToList());
        }

        public ReadOnlyCollection<string> Errors { get; }

        public ReadOnlyCollection<string> Warnings { get; }

        /// <summary>True when there are no blocking errors.</summary>
        public bool CanProceed => Errors.Count == 0;
    }

    /// <summary>
    /// Validates that a program / verify / erase operation is safe to start. Pure logic so it can be
    /// unit tested without hardware.
    /// </summary>
    public static class OperationPreflight
    {
        /// <summary>
        /// Checks common preconditions shared by all operations: a device must be selected, the I2C
        /// address must be valid, and the EEPROM profile must be internally consistent.
        /// </summary>
        public static PreflightReport CheckCommon(ProgrammingOptions options)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (options == null)
            {
                errors.Add("No operation options were provided.");
                return new PreflightReport(errors, warnings);
            }

            if (options.Device == null)
            {
                errors.Add("Select a target CyUSB device before starting.");
            }
            else if (!options.Device.IsBootloaderRunning)
            {
                warnings.Add(
                    "The selected device did not report the standard Cypress bootloader. " +
                    "If it is running custom firmware, EEPROM programming may require board-specific commands.");
            }

            ValidationResult addr = I2cAddress.Validate(options.Address.Value);
            if (!addr.IsValid)
            {
                errors.Add(addr.Message);
            }

            if (options.Profile == null)
            {
                errors.Add("Select an EEPROM profile before starting.");
            }
            else
            {
                ValidationResult profile = options.Profile.Validate();
                if (!profile.IsValid)
                {
                    errors.Add(profile.Message);
                }
            }

            return new PreflightReport(errors, warnings);
        }

        /// <summary>
        /// Checks preconditions for a program or verify operation, including that the firmware image
        /// fits within the EEPROM capacity and warning on unsigned/raw payloads.
        /// </summary>
        public static PreflightReport CheckProgramOrVerify(ProgrammingOptions options, FirmwareImage image)
        {
            PreflightReport common = CheckCommon(options);
            var errors = new List<string>(common.Errors);
            var warnings = new List<string>(common.Warnings);

            if (image == null)
            {
                errors.Add("Load a firmware file before programming or verifying.");
                return new PreflightReport(errors, warnings);
            }

            if (image.Length == 0)
            {
                errors.Add("The loaded firmware image is empty.");
            }
            else if (options?.Profile != null && image.Length > options.Profile.CapacityBytes)
            {
                errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Firmware is {0} bytes but the EEPROM only holds {1} bytes.",
                    image.Length,
                    options.Profile.CapacityBytes));
            }

            switch (image.Format)
            {
                case FirmwareFormat.Bin:
                    warnings.Add("Raw .bin payload will be written verbatim with no boot-header validation.");
                    break;

                case FirmwareFormat.Img when !image.HasCypressSignature:
                    warnings.Add(".img image is missing the 'CY' signature and may not be FX3-bootable.");
                    break;

                case FirmwareFormat.Unknown:
                    warnings.Add("Unrecognised firmware format; verify the file is EEPROM-bootable.");
                    break;
            }

            return new PreflightReport(errors, warnings);
        }

        /// <summary>
        /// Checks preconditions for an erase operation. Erase shares the common checks and requires a
        /// valid profile capacity to know how much to blank.
        /// </summary>
        public static PreflightReport CheckErase(ProgrammingOptions options)
        {
            PreflightReport common = CheckCommon(options);
            var errors = new List<string>(common.Errors);
            var warnings = new List<string>(common.Warnings);

            if (options?.Profile != null && options.Profile.CapacityBytes <= 0)
            {
                errors.Add("EEPROM capacity must be greater than zero to erase.");
            }

            warnings.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Erase will overwrite the entire {0}-byte EEPROM range with 0x{1:X2}.",
                options?.Profile?.CapacityBytes ?? 0,
                options?.Profile?.BlankByte ?? 0xFF));

            return new PreflightReport(errors, warnings);
        }
    }
}
