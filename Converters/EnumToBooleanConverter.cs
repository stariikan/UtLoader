using System;
using System.Globalization;
using System.Windows.Data;

namespace UtLoader.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        // Converts enum to bool for RadioButton
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string param = parameter.ToString()!;
            return value.ToString()!.Equals(param, StringComparison.InvariantCultureIgnoreCase);
        }

        // Converts bool back to enum
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null)
                return null;

            bool useValue = (bool)value;
            if (useValue)
            {
                return Enum.Parse(targetType, parameter.ToString()!);
            }

            return Binding.DoNothing;
        }
    }
}
