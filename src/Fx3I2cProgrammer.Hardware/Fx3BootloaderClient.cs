using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CyUSB;
using Fx3I2cProgrammer.Core.Abstractions;
using Fx3I2cProgrammer.Core.Eeprom;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;

namespace Fx3I2cProgrammer.Hardware
{
    /// <summary>
    /// Standard Cypress FX3 bootloader implementation of <see cref="IFx3Programmer"/> over CyUSB.dll.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Program+Verify path delegates to the Cypress SDK's own <c>CyFX3Device.DownloadFw</c> with
    /// the <c>I2CE2PROM</c> media type. That method performs the control-center-compatible I2C EEPROM
    /// programming (including its own internal verification) and is the safest, most portable path.
    /// </para>
    /// <para>
    /// The Verify-Only and Erase paths use low-level vendor control transfers (see
    /// <see cref="I2cEepromProtocol"/>). These follow the common FX3 I2C-EEPROM convention but must be
    /// validated against your board before being relied upon for destructive operations.
    /// </para>
    /// </remarks>
    public sealed class Fx3BootloaderClient : IFx3Programmer
    {
        private readonly I2cEepromProtocol _protocol;

        public Fx3BootloaderClient(I2cEepromProtocol protocol = null)
        {
            _protocol = protocol ?? I2cEepromProtocol.Default;
        }

        /// <inheritdoc />
        public Task<ProgrammingResult> ProbeAsync(ProgrammingOptions options, IOperationLog log, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using (CyDeviceSession session = CyDeviceSession.Open(options.Device))
                {
                    CyUSBDevice dev = session.Device;
                    log.Info(string.Format(
                        CultureInfo.InvariantCulture,
                        "Opened {0} (VID:PID {1:X4}:{2:X4}).",
                        string.IsNullOrEmpty(dev.FriendlyName) ? dev.Product : dev.FriendlyName,
                        dev.VendorID,
                        dev.ProductID));

                    if (session.Fx3 == null)
                    {
                        log.Warning("Device is not recognised as an FX3 by CyUSB; DownloadFw will be unavailable.");
                        return ProgrammingResult.Succeeded(OperationKind.Probing, 0, sw.Elapsed,
                            "Probe completed, but this is not an FX3 device.");
                    }

                    bool bootloader = session.Fx3.IsBootLoaderRunning();
                    log.Info("Cypress bootloader running: " + bootloader);
                    log.Info(string.Format(CultureInfo.InvariantCulture,
                        "USB speed: {0}", dev.bSuperSpeed ? "SuperSpeed" : dev.bHighSpeed ? "HighSpeed" : "Full/Low"));

                    return ProgrammingResult.Succeeded(OperationKind.Probing, 0, sw.Elapsed,
                        bootloader
                            ? "Probe OK — standard Cypress bootloader detected."
                            : "Probe OK — bootloader not reported (device may run custom firmware).");
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProgrammingResult> ProgramAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                cancellationToken.ThrowIfCancellationRequested();

                using (CyDeviceSession session = CyDeviceSession.Open(options.Device))
                {
                    if (session.Fx3 == null)
                    {
                        return ProgrammingResult.Failed(OperationKind.Programming,
                            "Selected device is not an FX3; cannot use the Cypress DownloadFw path.");
                    }

                    string imagePath = ResolveImageFile(image, out bool isTemp);
                    try
                    {
                        progress?.Report(OperationProgress.Status(OperationKind.Programming,
                            "Writing image to I2C EEPROM via Cypress DownloadFw..."));
                        log.Info(string.Format(CultureInfo.InvariantCulture,
                            "Programming {0} bytes to I2C EEPROM at {1}...", image.Length, options.Address));

                        FX3_FWDWNLOAD_ERROR_CODE code =
                            session.Fx3.DownloadFw(imagePath, FX3_FWDWNLOAD_MEDIA_TYPE.I2CE2PROM);

                        if (code != FX3_FWDWNLOAD_ERROR_CODE.SUCCESS)
                        {
                            string detail = session.Fx3.GetFwErrorString(code);
                            log.Error("DownloadFw failed: " + detail);
                            return ProgrammingResult.Failed(OperationKind.Programming,
                                "EEPROM programming failed: " + detail, 0, sw.Elapsed);
                        }

                        progress?.Report(new OperationProgress(OperationKind.Programming, image.Length, image.Length,
                            "Image written."));
                        log.Success("EEPROM programming reported success.");
                    }
                    finally
                    {
                        if (isTemp)
                        {
                            TryDelete(imagePath);
                        }
                    }

                    if (!options.VerifyAfterWrite)
                    {
                        return ProgrammingResult.Succeeded(OperationKind.Programming, image.Length, sw.Elapsed);
                    }

                    log.Info("Verifying EEPROM contents against the image...");
                    return VerifyCore(session, options, image, progress, log, cancellationToken, sw, OperationKind.Programming);
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProgrammingResult> VerifyAsync(
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using (CyDeviceSession session = CyDeviceSession.Open(options.Device))
                {
                    return VerifyCore(session, options, image, progress, log, cancellationToken, sw, OperationKind.Verifying);
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ProgrammingResult> EraseAsync(
            ProgrammingOptions options,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                EepromProfile profile = options.Profile;
                byte blank = profile.BlankByte;
                int capacity = profile.CapacityBytes;

                using (CyDeviceSession session = CyDeviceSession.Open(options.Device))
                {
                    CyControlEndPoint ctl = RequireControlEndpoint(session);

                    log.Warning(string.Format(CultureInfo.InvariantCulture,
                        "Erasing entire {0}-byte EEPROM at {1} with 0x{2:X2} (low-level vendor cmd 0x{3:X2}).",
                        capacity, options.Address, blank, _protocol.VendorRequest));

                    var blankPage = new byte[profile.PageSizeBytes];
                    for (int i = 0; i < blankPage.Length; i++)
                    {
                        blankPage[i] = blank;
                    }

                    long written = 0;
                    foreach (EepromChunk chunk in profile.EnumerateChunks(0, capacity))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        WriteChunk(ctl, options.Address.Value, chunk, blankPage, 0);
                        written += chunk.Length;
                        progress?.Report(new OperationProgress(OperationKind.Erasing, written, capacity, "erasing"));
                    }

                    log.Info("Erase writes complete; verifying blank...");

                    ProgrammingResult blankCheck = VerifyBlank(ctl, options, profile, progress, log, cancellationToken);
                    if (!blankCheck.Success)
                    {
                        return ProgrammingResult.Failed(OperationKind.Erasing, blankCheck.Message, written, sw.Elapsed);
                    }

                    log.Success("Erase verified: EEPROM reads back blank.");
                    return ProgrammingResult.Succeeded(OperationKind.Erasing, capacity, sw.Elapsed,
                        "Erase complete and verified blank.");
                }
            }, cancellationToken);
        }

        // ----- internals -------------------------------------------------------------------

        private ProgrammingResult VerifyCore(
            CyDeviceSession session,
            ProgrammingOptions options,
            FirmwareImage image,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken,
            Stopwatch sw,
            OperationKind reportKind)
        {
            CyControlEndPoint ctl = RequireControlEndpoint(session);
            EepromProfile profile = options.Profile;
            byte[] expected = image.Payload;
            int length = expected.Length;

            var buffer = new byte[Math.Min(_protocol.MaxReadChunkBytes, profile.PageSizeBytes)];
            long done = 0;

            foreach (EepromChunk chunk in EnumerateReadChunks(profile, length, buffer.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read = ReadChunk(ctl, options.Address.Value, chunk, buffer);
                if (read != chunk.Length)
                {
                    return ProgrammingResult.Failed(reportKind,
                        string.Format(CultureInfo.InvariantCulture,
                            "Read-back short at offset 0x{0:X}: expected {1} bytes, got {2}.",
                            chunk.Offset, chunk.Length, read),
                        done, sw.Elapsed);
                }

                for (int i = 0; i < chunk.Length; i++)
                {
                    int abs = chunk.Offset + i;
                    if (buffer[i] != expected[abs])
                    {
                        log.Error(string.Format(CultureInfo.InvariantCulture,
                            "Verify mismatch at 0x{0:X}: expected 0x{1:X2}, read 0x{2:X2}.",
                            abs, expected[abs], buffer[i]));
                        return ProgrammingResult.VerifyMismatch(reportKind, abs, expected[abs], buffer[i], sw.Elapsed);
                    }
                }

                done += chunk.Length;
                progress?.Report(new OperationProgress(OperationKind.Verifying, done, length, "verifying"));
            }

            log.Success("Verification succeeded: EEPROM matches the image.");
            return ProgrammingResult.Succeeded(reportKind, length, sw.Elapsed, "Program + verify succeeded.");
        }

        private ProgrammingResult VerifyBlank(
            CyControlEndPoint ctl,
            ProgrammingOptions options,
            EepromProfile profile,
            IProgress<OperationProgress> progress,
            IOperationLog log,
            CancellationToken cancellationToken)
        {
            int length = profile.CapacityBytes;
            byte blank = profile.BlankByte;
            var buffer = new byte[Math.Min(_protocol.MaxReadChunkBytes, profile.PageSizeBytes)];
            long done = 0;

            foreach (EepromChunk chunk in EnumerateReadChunks(profile, length, buffer.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read = ReadChunk(ctl, options.Address.Value, chunk, buffer);
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] != blank)
                    {
                        int abs = chunk.Offset + i;
                        return ProgrammingResult.Failed(OperationKind.Erasing,
                            string.Format(CultureInfo.InvariantCulture,
                                "EEPROM not blank at 0x{0:X}: read 0x{1:X2}, expected 0x{2:X2}.",
                                abs, buffer[i], blank));
                    }
                }

                done += chunk.Length;
                progress?.Report(new OperationProgress(OperationKind.Verifying, done, length, "verifying blank"));
            }

            return ProgrammingResult.Succeeded(OperationKind.Erasing, length, TimeSpan.Zero);
        }

        /// <summary>
        /// Splits a linear read into chunks that respect page/bank boundaries and the read buffer size.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<EepromChunk> EnumerateReadChunks(
            EepromProfile profile, int length, int maxChunk)
        {
            foreach (EepromChunk chunk in profile.EnumerateChunks(0, length))
            {
                int offset = chunk.Offset;
                int remaining = chunk.Length;
                int word = chunk.WordAddress;

                while (remaining > 0)
                {
                    int take = Math.Min(remaining, maxChunk);
                    yield return new EepromChunk(offset, take, chunk.BankIndex, word);
                    offset += take;
                    word += take;
                    remaining -= take;
                }
            }
        }

        private void WriteChunk(CyControlEndPoint ctl, byte baseSlaveAddress, EepromChunk chunk, byte[] source, int sourceOffset)
        {
            ConfigureControl(ctl, CyConst.DIR_TO_DEVICE, baseSlaveAddress, chunk);

            var buf = new byte[chunk.Length];
            Array.Copy(source, sourceOffset, buf, 0, chunk.Length);
            int len = buf.Length;

            if (!ctl.XferData(ref buf, ref len) || len != chunk.Length)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "I2C write failed at offset 0x{0:X} (transferred {1}/{2} bytes).",
                    chunk.Offset, len, chunk.Length));
            }

            // Wait for the EEPROM internal write cycle before the next page.
            if (_protocol.PageWriteDelayMs > 0)
            {
                Thread.Sleep(_protocol.PageWriteDelayMs);
            }
        }

        private int ReadChunk(CyControlEndPoint ctl, byte baseSlaveAddress, EepromChunk chunk, byte[] buffer)
        {
            ConfigureControl(ctl, CyConst.DIR_FROM_DEVICE, baseSlaveAddress, chunk);

            var buf = new byte[chunk.Length];
            int len = buf.Length;

            if (!ctl.XferData(ref buf, ref len))
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "I2C read failed at offset 0x{0:X}.", chunk.Offset));
            }

            Array.Copy(buf, 0, buffer, 0, len);
            return len;
        }

        private void ConfigureControl(CyControlEndPoint ctl, byte direction, byte baseSlaveAddress, EepromChunk chunk)
        {
            ctl.Target = CyConst.TGT_DEVICE;
            ctl.ReqType = CyConst.REQ_VENDOR;
            ctl.Direction = direction;
            ctl.ReqCode = _protocol.VendorRequest;
            ctl.Value = (ushort)chunk.WordAddress;                       // EEPROM byte offset within the bank
            ctl.Index = (ushort)(baseSlaveAddress + chunk.BankIndex);    // I2C slave address (+ bank select)
            ctl.TimeOut = (uint)_protocol.TimeoutMs;
        }

        private static CyControlEndPoint RequireControlEndpoint(CyDeviceSession session)
        {
            CyControlEndPoint ctl = session.Device.ControlEndPt;
            if (ctl == null)
            {
                throw new IOException("The device does not expose a control endpoint.");
            }

            return ctl;
        }

        private static string ResolveImageFile(FirmwareImage image, out bool isTemp)
        {
            if (!string.IsNullOrEmpty(image.FilePath) && File.Exists(image.FilePath))
            {
                isTemp = false;
                return image.FilePath;
            }

            // The image was analysed from bytes; DownloadFw needs a file, so stage a temp copy.
            string temp = Path.Combine(Path.GetTempPath(),
                "fx3fw_" + Guid.NewGuid().ToString("N") + ".img");
            File.WriteAllBytes(temp, image.Payload);
            isTemp = true;
            return temp;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best effort only.
            }
        }
    }
}
