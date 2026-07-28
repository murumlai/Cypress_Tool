namespace Fx3I2cProgrammer.Core.Models
{
    /// <summary>
    /// The kind of long-running device operation being performed. Used for progress/reporting.
    /// </summary>
    public enum OperationKind
    {
        Idle = 0,
        Scanning,
        Probing,
        Programming,
        Verifying,
        Erasing
    }
}
