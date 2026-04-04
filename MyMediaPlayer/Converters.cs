using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyMediaPlayer
{
    /// <summary>
    /// Returns Visible when value is null (or Collapsed when Invert=true, i.e. visible when non-null).
    /// Invert=false (default): null → Visible, non-null → Collapsed  (hides when something is playing)
    /// Invert=true:            null → Collapsed, non-null → Visible   (shows when something is playing)
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            // Default (Invert=false): show placeholder when null
            bool show = Invert ? !isNull : isNull;
            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
