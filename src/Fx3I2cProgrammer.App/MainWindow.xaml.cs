using System.Windows;

namespace Fx3I2cProgrammer.App
{
    /// <summary>
    /// Main window. All behaviour lives in <see cref="ViewModels.MainWindowViewModel"/>; the
    /// data context is assigned by <see cref="App"/> at startup.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
