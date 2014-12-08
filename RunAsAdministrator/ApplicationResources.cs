// developed by Rui Lopes (ruilopes.com). licensed under GPLv3.

using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RunAsAdministrator
{
    public static class ApplicationResources
    {
        public static Cursor LoadCursor(string name)
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(name, UriKind.Relative));

            if (info == null)
                return null;

            var cursor = new Cursor(info.Stream);

            return cursor;
        }

        public static BitmapImage LoadBitmapImage(string name)
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(name, UriKind.Relative));

            if (info == null)
                return null;

            using (info.Stream)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = info.Stream;
                image.EndInit();
                image.Freeze();

                return image;
            }
        }
    }
}
