using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Fx3I2cProgrammer.Core.Logging;

namespace Fx3I2cProgrammer.App.Converters
{
    /// <summary>Maps a <see cref="LogLevel"/> to a text brush for the log list.</summary>
    public sealed class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly Brush Info = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        private static readonly Brush Success = new SolidColorBrush(Color.FromRgb(0x1B, 0x7F, 0x37));
        private static readonly Brush Warning = new SolidColorBrush(Color.FromRgb(0xB0, 0x6A, 0x00));
        private static readonly Brush Error = new SolidColorBrush(Color.FromRgb(0xC0, 0x28, 0x28));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case LogLevel.Success:
                    return Success;
                case LogLevel.Warning:
                    return Warning;
                case LogLevel.Error:
                    return Error;
                default:
                    return Info;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Converts a boolean to <see cref="Visibility"/> (true =&gt; Visible).</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            if (parameter is string s && string.Equals(s, "invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }
}
