using System.Windows;
using System.Windows.Media;

namespace SwitchSpeak
{
    public static class DependencyObjectExtension
    {
        public static T FindParent<T>(this DependencyObject from) where T : class
        {
            T result = null;
            DependencyObject parent = VisualTreeHelper.GetParent(from);

            if (parent is T)
                result = parent as T;
            else if (parent != null)
                result = FindParent<T>(parent);

            return result;
        }
    }
}
