// developed by Rui Lopes (ruilopes.com). licensed under GPLv3.

using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RunAsAdministrator
{
    public partial class MainWindow : Window
    {
        const string AppCompatFlagsLayersSubKeyName = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        public ObservableCollection<Application> Applications { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            Applications = new ObservableCollection<Application>(LoadCurrentUserApplications());

            ApplicationListBox.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            DataContext = this;
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files == null || files.Length == 0)
                return;

            AddApplications(files);
        }

        private void FinderTool_OnSelectWindow(object sender, FinderTool.RoutedWindowEventArgs e)
        {
            var window = e.Window;

            if (window != null)
            {
                try
                {
                    var path = window.Process.MainModule.FileName;

                    var application = LoadApplication(path);

                    SelectedApplicationPanel.Visibility = Visibility.Visible;
                    SelectedApplicationPanel.DataContext = application;

                    return;
                }
                catch (Win32Exception)
                {
                    // NB when this application is not running in 64-bit we'll get a:
                    //     System.ComponentModel.Win32Exception
                    //     A 32 bit processes cannot access modules of a 64 bit process.
                }
            }

            SelectedApplicationPanel.Visibility = Visibility.Collapsed;
            SelectedApplicationPanel.DataContext = null;
        }

        private void FinderTool_OnEndSelectWindow(object sender, FinderTool.RoutedWindowEventArgs e)
        {
            SelectedApplicationPanel.Visibility = Visibility.Collapsed;
            SelectedApplicationPanel.DataContext = null;

            if (e.Window != null)
            {
                string path;

                try
                {
                    path = e.Window.Process.MainModule.FileName;
                }
                catch (Win32Exception)
                {
                    // NB when this application is not running in 64-bit we'll get a:
                    //     System.ComponentModel.Win32Exception
                    //     A 32 bit processes cannot access modules of a 64 bit process.
                    return;
                }

                AddApplications(path);
            }
        }
        
        private void AddApplications(params string[] paths)
        {
            var applications = paths
                .Select(LoadApplication)
                .Where(a => a != null)
                .ToList();

            // TODO remove duplicates?

            foreach (var application in applications)
            {
                Applications.Add(application);
            }

            SaveCurrentUserApplications();
        }

        private static List<Application> LoadCurrentUserApplications()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppCompatFlagsLayersSubKeyName))
            {
                if (key == null)
                    return new List<Application>();

                return key
                    .GetValueNames()
                    .Select(LoadApplication)
                    .OrderBy(a => a.Name)
                    .ToList();
            }
        }
        
        private void SaveCurrentUserApplications()
        {
            Registry.CurrentUser.DeleteSubKeyTree(AppCompatFlagsLayersSubKeyName, false);

            if (Applications.Count == 0)
                return;

            using (var key = Registry.CurrentUser.CreateSubKey(AppCompatFlagsLayersSubKeyName))
            {
                foreach (var application in Applications)
                {
                    key.SetValue(application.Path, "RUNASADMIN", RegistryValueKind.String);
                }
            }
        }

        private static Application LoadApplication(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(path);

            BitmapSource icon = null;

            using (var associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(path))
            {
                if (associatedIcon != null)
                {
                    using (var imageStream = new MemoryStream())
                    {
                        associatedIcon.ToBitmap().Save(imageStream, ImageFormat.Png);

                        imageStream.Position = 0;;

                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = imageStream;
                        bi.EndInit();
                        bi.Freeze();

                        icon = bi;
                    }
                }
            }

            return new Application
                {
                    Name = !string.IsNullOrEmpty(versionInfo.FileDescription) ? versionInfo.FileDescription : Path.GetFileNameWithoutExtension(path),
                    Path = path,
                    Icon = icon,
                };
        }

        private void ApplicationListBox_MenuItem_Delete_OnClick(object sender, RoutedEventArgs e)
        {
            var application = (Application)ApplicationListBox.SelectedItem;

            if (MessageBox.Show(
                    this,
                    string.Format("Stop running {0} as Administrator?", application.Name),
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No
                ) != MessageBoxResult.Yes)
                return;

            Applications.Remove(application);

            SaveCurrentUserApplications();
        }

        private void ApplicationListBox_MenuItem_OpenFileLocation_OnClick(object sender, RoutedEventArgs e)
        {
            var application = (Application)ApplicationListBox.SelectedItem;

            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", application.Path));
        }
    }

    public class Application
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public BitmapSource Icon { get; set; }
    }
}
