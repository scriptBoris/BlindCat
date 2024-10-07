using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.SDcontrols.Converters;

public class IsEmpty : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        switch (value)
        {
            case string str:
                return string.IsNullOrWhiteSpace(str);
            case IList list:
                return list.Count == 0;
            case int int32:
                return int32 == 0;
            default:
                break;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}