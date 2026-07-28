using System.Globalization;

namespace Fx3I2cProgrammer.Core.Models
{
    /// <summary>
    /// A progress update raised during a long-running operation. Reported through
    /// <see cref="System.IProgress{T}"/> so the UI can update a progress bar and status line.
    /// </summary>
    public sealed class OperationProgress
    {
        public OperationProgress(OperationKind kind, long bytesDone, long bytesTotal, string message)
        {
            Kind = kind;
            BytesDone = bytesDone;
            BytesTotal = bytesTotal;
            Message = message ?? string.Empty;
        }

        public OperationKind Kind { get; }

        public long BytesDone { get; }

        public long BytesTotal { get; }

        public string Message { get; }

        /// <summary>Completion in the range 0..1, or 0 when the total is unknown.</summary>
        public double Fraction => BytesTotal > 0
            ? (double)BytesDone / BytesTotal
            : 0d;

        /// <summary>Completion as a whole percentage 0..100.</summary>
        public int Percent => BytesTotal > 0
            ? (int)(100L * BytesDone / BytesTotal)
            : 0;

        public static OperationProgress Status(OperationKind kind, string message) =>
            new OperationProgress(kind, 0, 0, message);

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1}% ({2}/{3}) {4}",
            Kind,
            Percent,
            BytesDone,
            BytesTotal,
            Message);
    }
}
