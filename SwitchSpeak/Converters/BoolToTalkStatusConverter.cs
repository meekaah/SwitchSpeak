using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SwitchSpeak.Converters
{
    public class BoolToTalkStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is bool?))
            {
                return null;
            }

            bool? b = (bool?)value;
            if (b.HasValue && b.Value)
            {
                return new BitmapImage(new Uri("pack://application:,,,/SwitchSpeak;component/Default/32x32_player_on.png"));
            }
            else
            {
                return new BitmapImage(new Uri("pack://application:,,,/SwitchSpeak;component/Default/32x32_player_off.png"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
