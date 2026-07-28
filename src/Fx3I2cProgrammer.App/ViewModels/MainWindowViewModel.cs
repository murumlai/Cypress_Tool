using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Fx3I2cProgrammer.App.Services;
using Fx3I2cProgrammer.Core.Abstractions;
using Fx3I2cProgrammer.Core.Eeprom;
using Fx3I2cProgrammer.Core.Firmware;
using Fx3I2cProgrammer.Core.Logging;
using Fx3I2cProgrammer.Core.Models;
using Fx3I2cProgrammer.Core.Validation;
using Fx3I2cProgrammer.Core.Workflow;

namespace Fx3I2cProgrammer.App.ViewModels
{
    /// <summary>
    /// Coordinates the main window: device discovery, firmware loading, EEPROM settings, and the
    /// program / verify / erase commands. All device work runs off the UI thread via the workflow.
    /// </summary>
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly IUsbDeviceEnumerator _enumerator;
        private readonly ProgrammingWorkflow _workflow;
        private readonly UiOperationLog _log;
        private readonly IUserInteraction _ui;
        private readonly AppSettingsStore _settingsStore;
        private readonly AppSettings _settings;
        private readonly IProgress<OperationProgress> _progress;

        private UsbDeviceInfo _selectedDevice;
        private EepromPresetItem _selectedPreset;
        private FirmwareImage _loadedImage;
        private string _addressText = "0x50";
        private string _addressError = string.Empty;
        private string _firmwarePath = string.Empty;
        private string _firmwareSummary = "No firmware loaded.";
        private bool _verifyAfterWrite = true;
        private bool _isBusy;
        private double _progressPercent;
        private string _statusText = "Ready.";
        private int _capacityBytes;
        private int _pageSizeBytes;
        private int _bankSizeBytes;
        private EepromAddressingMode _addressingMode;
        private CancellationTokenSource _cts;

        public MainWindowViewModel(
            IUsbDeviceEnumerator enumerator,
            IFx3Programmer programmer,
            UiOperationLog log,
            IUserInteraction ui,
            AppSettingsStore settingsStore)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _workflow = new ProgrammingWorkflow(enumerator, programmer, log);
            _settings = _settingsStore.Load();

            _progress = new Progress<OperationProgress>(OnProgress);

            Devices = new ObservableCollection<UsbDeviceInfo>();
            AddressingModes = new ReadOnlyCollection<EepromAddressingMode>(
                (EepromAddressingMode[])Enum.GetValues(typeof(EepromAddressingMode)));

            Presets = new ObservableCollection<EepromPresetItem>();
            foreach (EepromProfile profile in EepromProfiles.BuiltIn)
            {
                Presets.Add(new EepromPresetItem(profile.Name, profile));
            }

            Presets.Add(EepromPresetItem.Custom);

            RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync, () => !IsBusy);
            BrowseFirmwareCommand = new RelayCommand(BrowseFirmware, () => !IsBusy);
            ProbeCommand = new AsyncRelayCommand(ProbeAsync, () => !IsBusy && SelectedDevice != null);
            ProgramCommand = new AsyncRelayCommand(ProgramAsync, CanProgramOrVerify);
            VerifyCommand = new AsyncRelayCommand(VerifyAsync, CanProgramOrVerify);
            EraseCommand = new AsyncRelayCommand(EraseAsync, () => !IsBusy && SelectedDevice != null);
            CancelCommand = new RelayCommand(Cancel, () => IsBusy && _cts != null);
            ClearLogCommand = new RelayCommand(() => _log.Clear(), () => !IsBusy);

            ApplySettings();
        }

        // ----- bound collections/state ----------------------------------------------------

        public ObservableCollection<UsbDeviceInfo> Devices { get; }

        public ObservableCollection<EepromPresetItem> Presets { get; }

        public ReadOnlyCollection<EepromAddressingMode> AddressingModes { get; }

        public ObservableCollection<LogEntry> Log => _log.Entries;

        public UsbDeviceInfo SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        public EepromPresetItem SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value) && value?.Profile != null)
                {
                    LoadPresetIntoEditor(value.Profile);
                }
            }
        }

        public string AddressText
        {
            get => _addressText;
            set
            {
                if (SetProperty(ref _addressText, value))
                {
                    ValidateAddress();
                }
            }
        }

        public string AddressError
        {
            get => _addressError;
            private set => SetProperty(ref _addressError, value);
        }

        public int CapacityBytes
        {
            get => _capacityBytes;
            set => SetProperty(ref _capacityBytes, value);
        }

        public int PageSizeBytes
        {
            get => _pageSizeBytes;
            set => SetProperty(ref _pageSizeBytes, value);
        }

        public int BankSizeBytes
        {
            get => _bankSizeBytes;
            set => SetProperty(ref _bankSizeBytes, value);
        }

        public EepromAddressingMode AddressingMode
        {
            get => _addressingMode;
            set => SetProperty(ref _addressingMode, value);
        }

        public string FirmwarePath
        {
            get => _firmwarePath;
            private set => SetProperty(ref _firmwarePath, value);
        }

        public string FirmwareSummary
        {
            get => _firmwareSummary;
            private set => SetProperty(ref _firmwareSummary, value);
        }

        public bool VerifyAfterWrite
        {
            get => _verifyAfterWrite;
            set => SetProperty(ref _verifyAfterWrite, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(IsIdle));
                }
            }
        }

        public bool IsIdle => !_isBusy;

        public double ProgressPercent
        {
            get => _progressPercent;
            private set => SetProperty(ref _progressPercent, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        // ----- commands -------------------------------------------------------------------

        public AsyncRelayCommand RefreshDevicesCommand { get; }

        public RelayCommand BrowseFirmwareCommand { get; }

        public AsyncRelayCommand ProbeCommand { get; }

        public AsyncRelayCommand ProgramCommand { get; }

        public AsyncRelayCommand VerifyCommand { get; }

        public AsyncRelayCommand EraseCommand { get; }

        public RelayCommand CancelCommand { get; }

        public RelayCommand ClearLogCommand { get; }

        // ----- lifecycle ------------------------------------------------------------------

        /// <summary>Persists current preferences. Call on application shutdown.</summary>
        public void SaveSettings()
        {
            _settings.LastAddressText = AddressText;
            _settings.LastProfileName = SelectedPreset?.Name ?? string.Empty;
            _settings.VerifyAfterWrite = VerifyAfterWrite;
            _settingsStore.Save(_settings);
        }

        private void ApplySettings()
        {
            AddressText = string.IsNullOrWhiteSpace(_settings.LastAddressText) ? "0x50" : _settings.LastAddressText;
            VerifyAfterWrite = _settings.VerifyAfterWrite;

            EepromPresetItem preset = null;
            if (!string.IsNullOrEmpty(_settings.LastProfileName))
            {
                preset = FindPreset(_settings.LastProfileName);
            }

            SelectedPreset = preset ?? Presets[0];
            ValidateAddress();
        }

        private EepromPresetItem FindPreset(string name)
        {
            foreach (EepromPresetItem item in Presets)
            {
                if (string.Equals(item.Name, name, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private void LoadPresetIntoEditor(EepromProfile profile)
        {
            CapacityBytes = profile.CapacityBytes;
            PageSizeBytes = profile.PageSizeBytes;
            BankSizeBytes = profile.BankSizeBytes;
            AddressingMode = profile.AddressingMode;
        }

        // ----- command implementations ----------------------------------------------------

        private async Task RefreshDevicesAsync()
        {
            await RunOperationAsync(OperationKind.Scanning, async () =>
            {
                UsbDeviceInfo previous = SelectedDevice;
                IReadOnlyList<UsbDeviceInfo> found = await Task.Run(() => _workflow.Scan()).ConfigureAwait(true);

                Devices.Clear();
                foreach (UsbDeviceInfo device in found)
                {
                    Devices.Add(device);
                }

                // Never silently reuse a device for destructive ops; require re-selection each scan.
                SelectedDevice = null;
                StatusText = string.Format(CultureInfo.InvariantCulture, "Found {0} device(s).", Devices.Count);

                if (previous != null && Devices.Count > 0)
                {
                    _log.Info("Re-select the target device before programming or erasing.");
                }

                return null;
            }).ConfigureAwait(true);
        }

        private void BrowseFirmware()
        {
            string picked = _ui.PickFirmwareFile(_settings.LastFirmwareDirectory);
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            try
            {
                FirmwareImage image = FirmwareImageLoader.Load(picked);
                _loadedImage = image;
                FirmwarePath = picked;
                FirmwareSummary = image.DisplaySummary;
                _settings.LastFirmwareDirectory = System.IO.Path.GetDirectoryName(picked) ?? string.Empty;

                _log.Info("Loaded firmware: " + image.DisplaySummary);
                if (!string.IsNullOrEmpty(image.Notes))
                {
                    LogLevel level = image.Notes.IndexOf("WARNING", StringComparison.OrdinalIgnoreCase) >= 0
                        ? LogLevel.Warning
                        : LogLevel.Info;
                    _log.Log(level, image.Notes);
                }
            }
            catch (FirmwareLoadException ex)
            {
                _loadedImage = null;
                FirmwareSummary = "Failed to load firmware.";
                _log.Error(ex.Message);
                _ui.ShowMessage("Firmware load failed", ex.Message);
            }
        }

        private async Task ProbeAsync()
        {
            ProgrammingOptions options = BuildOptions();
            if (options == null)
            {
                return;
            }

            await RunOperationAsync(OperationKind.Probing, async () =>
                await _workflow.ProbeAsync(options, _cts.Token).ConfigureAwait(true)).ConfigureAwait(true);
        }

        private async Task ProgramAsync()
        {
            ProgrammingOptions options = BuildOptions();
            if (options == null || _loadedImage == null)
            {
                _log.Error("Load a firmware file and select a device before programming.");
                return;
            }

            await RunOperationAsync(OperationKind.Programming, async () =>
                await _workflow.ProgramAsync(options, _loadedImage, _progress, _cts.Token).ConfigureAwait(true))
                .ConfigureAwait(true);
        }

        private async Task VerifyAsync()
        {
            ProgrammingOptions options = BuildOptions();
            if (options == null || _loadedImage == null)
            {
                _log.Error("Load a firmware file and select a device before verifying.");
                return;
            }

            await RunOperationAsync(OperationKind.Verifying, async () =>
                await _workflow.VerifyAsync(options, _loadedImage, _progress, _cts.Token).ConfigureAwait(true))
                .ConfigureAwait(true);
        }

        private async Task EraseAsync()
        {
            ProgrammingOptions options = BuildOptions();
            if (options == null)
            {
                return;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "Erase the ENTIRE {0}-byte EEPROM at {1} on:\n\n{2}\n\nThis overwrites all contents with 0x{3:X2} and cannot be undone.",
                options.Profile.CapacityBytes,
                options.Address,
                options.Device.DisplayName,
                options.Profile.BlankByte);

            if (!_ui.ConfirmDestructive("Confirm EEPROM erase", message))
            {
                _log.Info("Erase cancelled by operator.");
                return;
            }

            await RunOperationAsync(OperationKind.Erasing, async () =>
                await _workflow.EraseAsync(options, _progress, _cts.Token).ConfigureAwait(true)).ConfigureAwait(true);
        }

        private void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _log.Warning("Cancellation requested...");
                _cts.Cancel();
            }
        }

        // ----- helpers --------------------------------------------------------------------

        private bool CanProgramOrVerify() =>
            !IsBusy && SelectedDevice != null && _loadedImage != null && string.IsNullOrEmpty(AddressError);

        private ProgrammingOptions BuildOptions()
        {
            if (SelectedDevice == null)
            {
                _ui.ShowMessage("No device selected", "Select a target CyUSB device first.");
                return null;
            }

            if (!I2cAddress.TryParse(AddressText, out I2cAddress address, out string addrError))
            {
                _log.Error(addrError);
                _ui.ShowMessage("Invalid I2C address", addrError);
                return null;
            }

            EepromProfile profile;
            try
            {
                string name = SelectedPreset?.Profile?.Name ?? "Custom";
                profile = new EepromProfile(name, CapacityBytes, PageSizeBytes, AddressingMode, BankSizeBytes);
            }
            catch (ArgumentException ex)
            {
                _log.Error("EEPROM settings invalid: " + ex.Message);
                _ui.ShowMessage("Invalid EEPROM settings", ex.Message);
                return null;
            }

            ValidationResult profileValid = profile.Validate();
            if (!profileValid.IsValid)
            {
                _log.Error(profileValid.Message);
                _ui.ShowMessage("Invalid EEPROM settings", profileValid.Message);
                return null;
            }

            return new ProgrammingOptions(SelectedDevice, address, profile, VerifyAfterWrite);
        }

        private void ValidateAddress()
        {
            AddressError = I2cAddress.TryParse(AddressText, out _, out string error) ? string.Empty : error;
        }

        private async Task RunOperationAsync(OperationKind kind, Func<Task<ProgrammingResult>> action)
        {
            if (IsBusy)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            IsBusy = true;
            ProgressPercent = 0;
            StatusText = kind + "...";

            try
            {
                ProgrammingResult result = await action().ConfigureAwait(true);
                if (result != null)
                {
                    StatusText = result.Message;
                    _log.Log(result.Success ? LogLevel.Success : LogLevel.Error, result.ToString());
                }
            }
            catch (Exception ex)
            {
                _log.Error(kind + " raised an unexpected error: " + ex.Message);
                StatusText = kind + " failed.";
            }
            finally
            {
                IsBusy = false;
                ProgressPercent = 0;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void OnProgress(OperationProgress p)
        {
            if (p == null)
            {
                return;
            }

            if (p.BytesTotal > 0)
            {
                ProgressPercent = p.Percent;
            }

            StatusText = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1}% {2}",
                p.Kind,
                p.Percent,
                p.Message);
        }
    }

    /// <summary>An EEPROM preset selectable in the UI. A null <see cref="Profile"/> means "Custom".</summary>
    public sealed class EepromPresetItem
    {
        public EepromPresetItem(string name, EepromProfile profile)
        {
            Name = name;
            Profile = profile;
        }

        public string Name { get; }

        public EepromProfile Profile { get; }

        public static EepromPresetItem Custom { get; } = new EepromPresetItem("Custom...", null);

        public override string ToString() => Name;
    }
}
