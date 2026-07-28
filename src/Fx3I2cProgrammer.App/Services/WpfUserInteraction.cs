using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Fx3I2cProgrammer.App.Services
{
    /// <summary>
    /// WPF implementation of <see cref="IUserInteraction"/> using the standard file dialog and message box.
    /// </summary>
    public sealed class WpfUserInteraction : IUserInteraction
    {
        private readonly Window _owner;

        public WpfUserInteraction(Window owner)
        {
            _owner = owner;
        }

        public string PickFirmwareFile(string initialDirectory)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select FX3 firmware image",
                Filter =
                    "FX3 I2C EEPROM image (*.iic)|*.iic|" +
                    "FX3 firmware image (*.img)|*.img|" +
                    "Raw binary (*.bin)|*.bin|" +
                    "All firmware (*.iic;*.img;*.bin)|*.iic;*.img;*.bin|" +
                    "All files (*.*)|*.*",
                FilterIndex = 4,
                CheckFileExists = true
            };

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            bool? result = _owner != null ? dialog.ShowDialog(_owner) : dialog.ShowDialog();
            return result == true ? dialog.FileName : null;
        }

        public bool ConfirmDestructive(string title, string message)
        {
            MessageBoxResult result = MessageBox.Show(
                _owner,
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }

        public void ShowMessage(string title, string message)
        {
            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
