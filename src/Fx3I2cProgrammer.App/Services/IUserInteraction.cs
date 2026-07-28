namespace Fx3I2cProgrammer.App.Services
{
    /// <summary>
    /// Abstraction over the user-facing dialogs the view model needs, so the view model does not
    /// depend on WPF dialog types directly.
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>
        /// Shows a firmware file picker. Returns the selected path, or null if cancelled.
        /// </summary>
        string PickFirmwareFile(string initialDirectory);

        /// <summary>
        /// Asks the operator to confirm a destructive action. Returns true to proceed.
        /// </summary>
        bool ConfirmDestructive(string title, string message);

        /// <summary>Shows an informational/error message.</summary>
        void ShowMessage(string title, string message);
    }
}
