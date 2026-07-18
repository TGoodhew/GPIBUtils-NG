using System;
using System.Globalization;
using System.Windows.Data;

namespace GpibUtils.Wpf.Mvvm
{
    /// <summary>Returns the logical negation of a bound <see cref="bool"/> — used to disable inputs while a
    /// background operation (e.g. the DMM monitor) is running.</summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : (object)true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : (object)false;
    }
}
