using System;
using System.IO;
using System.Xml.Serialization;

namespace Fx3I2cProgrammer.App.Services
{
    /// <summary>
    /// Persisted user preferences. Deliberately does NOT store a selected USB device: destructive
    /// operations must always target a device chosen in the current session.
    /// </summary>
    [Serializable]
    [XmlRoot("Fx3I2cProgrammerSettings")]
    public sealed class AppSettings
    {
        /// <summary>Last I2C address text, e.g. "0x50".</summary>
        public string LastAddressText { get; set; } = "0x50";

        /// <summary>Name of the last selected EEPROM profile.</summary>
        public string LastProfileName { get; set; } = string.Empty;

        /// <summary>Directory the firmware picker last opened.</summary>
        public string LastFirmwareDirectory { get; set; } = string.Empty;

        /// <summary>Whether "verify after write" was enabled.</summary>
        public bool VerifyAfterWrite { get; set; } = true;
    }

    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as XML under the user's roaming AppData folder.
    /// </summary>
    public sealed class AppSettingsStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(AppSettings));

        private readonly string _path;

        public AppSettingsStore()
            : this(DefaultPath())
        {
        }

        public AppSettingsStore(string path)
        {
            _path = path;
        }

        public static string DefaultPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Fx3I2cProgrammer");
            return Path.Combine(dir, "settings.xml");
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    using (var stream = File.OpenRead(_path))
                    {
                        if (Serializer.Deserialize(stream) is AppSettings settings)
                        {
                            return settings;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                // Corrupt or unreadable settings fall back to defaults.
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var stream = File.Create(_path))
                {
                    Serializer.Serialize(stream, settings);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Persisting settings is best-effort; ignore failures.
            }
        }
    }
}
