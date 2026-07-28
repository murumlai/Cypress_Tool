using System;
using System.Globalization;

namespace Fx3I2cProgrammer.Core.Models
{
    /// <summary>
    /// The outcome of a program / verify / erase operation, including the first byte that
    /// mismatched during verification (if any) to aid diagnosis.
    /// </summary>
    public sealed class ProgrammingResult
    {
        private ProgrammingResult(
            OperationKind kind,
            bool success,
            long bytesProcessed,
            long? mismatchOffset,
            string message,
            TimeSpan elapsed)
        {
            Kind = kind;
            Success = success;
            BytesProcessed = bytesProcessed;
            MismatchOffset = mismatchOffset;
            Message = message ?? string.Empty;
            Elapsed = elapsed;
        }

        public OperationKind Kind { get; }

        public bool Success { get; }

        public long BytesProcessed { get; }

        /// <summary>Absolute EEPROM offset of the first verification mismatch, when applicable.</summary>
        public long? MismatchOffset { get; }

        public string Message { get; }

        public TimeSpan Elapsed { get; }

        public static ProgrammingResult Succeeded(OperationKind kind, long bytesProcessed, TimeSpan elapsed, string message = null) =>
            new ProgrammingResult(
                kind,
                true,
                bytesProcessed,
                null,
                message ?? string.Format(CultureInfo.InvariantCulture, "{0} completed successfully.", kind),
                elapsed);

        public static ProgrammingResult Failed(OperationKind kind, string message, long bytesProcessed = 0, TimeSpan elapsed = default) =>
            new ProgrammingResult(kind, false, bytesProcessed, null, message, elapsed);

        public static ProgrammingResult VerifyMismatch(OperationKind kind, long mismatchOffset, byte expected, byte actual, TimeSpan elapsed) =>
            new ProgrammingResult(
                kind,
                false,
                mismatchOffset,
                mismatchOffset,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Verification failed at offset 0x{0:X}: expected 0x{1:X2}, read 0x{2:X2}.",
                    mismatchOffset,
                    expected,
                    actual),
                elapsed);

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1}: {2} ({3} bytes, {4:0.0}s)",
            Kind,
            Success ? "OK" : "FAILED",
            Message,
            BytesProcessed,
            Elapsed.TotalSeconds);
    }
}
