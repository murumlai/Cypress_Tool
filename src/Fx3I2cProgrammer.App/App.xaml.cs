using System.Windows;
using Fx3I2cProgrammer.App.Services;
using Fx3I2cProgrammer.App.ViewModels;
using Fx3I2cProgrammer.Hardware;

namespace Fx3I2cProgrammer.App
{
    /// <summary>
    /// Application entry point. Wires up the hardware services, view model and main window.
    /// </summary>
    public partial class App : Application
    {
        private MainWindowViewModel _viewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = new MainWindow();
            var log = new UiOperationLog(window.Dispatcher);
            var interaction = new WpfUserInteraction(window);
            var enumerator = new CyUsbDeviceEnumerator();
            var programmer = new Fx3BootloaderClient();
            var settingsStore = new AppSettingsStore();

            _viewModel = new MainWindowViewModel(enumerator, programmer, log, interaction, settingsStore);
            window.DataContext = _viewModel;

            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _viewModel?.SaveSettings();
            base.OnExit(e);
        }
    }
}
