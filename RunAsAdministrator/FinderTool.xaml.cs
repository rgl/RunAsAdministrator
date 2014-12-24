// developed by Rui Lopes (ruilopes.com). licensed under GPLv3.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace RunAsAdministrator
{
    public partial class FinderTool : UserControl
    {
        private readonly Cursor _finderToolCursor;
        private readonly ImageSource _findingImageSource;
        private readonly ImageSource _defaultImageSource;
        private readonly int _selfProcessId;
        private IntPtr _currentWindowHwnd;
        private Window _currentWindow;
        private bool _canSelectCurrentWindow;
        private FrameWindow _currentFrameWindow;

        public class Window
        {
            public IntPtr Hwnd { get; internal set; }
            public Process Process { get; internal set;}
        }

        public class RoutedWindowEventArgs : RoutedEventArgs
        {
            private readonly Window _window;

            public RoutedWindowEventArgs(RoutedEvent routedEvent, Window window)
                : base(routedEvent)
            {
                _window = window;
            }

            public Window Window { get { return _window; } }
        }

        public delegate void RoutedWindowEventHandler(object sender, RoutedWindowEventArgs e);

        private static readonly RoutedEvent BeginSelectWindowEvent = EventManager.RegisterRoutedEvent(
            "BeginSelectWindow",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(FinderTool)
        );

        private static readonly RoutedEvent SelectWindowEvent = EventManager.RegisterRoutedEvent(
            "SelectWindow",
            RoutingStrategy.Bubble,
            typeof(RoutedWindowEventHandler),
            typeof(FinderTool)
        );

        private static readonly RoutedEvent EndSelectWindowEvent = EventManager.RegisterRoutedEvent(
            "EndSelectWindow",
            RoutingStrategy.Bubble,
            typeof(RoutedWindowEventHandler),
            typeof(FinderTool)
        );

        public event RoutedEventHandler BeginSelectWindow
        {
            add { AddHandler(BeginSelectWindowEvent, value); }
            remove { RemoveHandler(BeginSelectWindowEvent, value); }
        }

        public event RoutedWindowEventHandler SelectWindow
        {
            add { AddHandler(SelectWindowEvent, value); }
            remove { RemoveHandler(SelectWindowEvent, value); }
        }

        public event RoutedWindowEventHandler EndSelectWindow
        {
            add { AddHandler(EndSelectWindowEvent, value); }
            remove { RemoveHandler(EndSelectWindowEvent, value); }
        }

        protected virtual void OnBeginSelectWindow(RoutedEventArgs e)
        {
            RaiseEvent(e);
        }

        protected virtual void OnSelectWindow(RoutedWindowEventArgs e)
        {
            RaiseEvent(e);
        }

        protected virtual void OnEndSelectWindow(RoutedWindowEventArgs e)
        {
            RaiseEvent(e);
        }

        public FinderTool()
        {
            InitializeComponent();

            _selfProcessId = Process.GetCurrentProcess().Id;
            _finderToolCursor = ApplicationResources.LoadCursor("FinderTool.cur");
            _findingImageSource = ApplicationResources.LoadBitmapImage("FinderToolFinding.bmp");
            _defaultImageSource = Image.Source;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Focusable = true;
            Focus();
            Image.Source = _findingImageSource;
            Mouse.OverrideCursor = _finderToolCursor;
            CaptureMouse();
            OnBeginSelectWindow(new RoutedEventArgs(BeginSelectWindowEvent));
        }

        private void StopSelectWindow(Window window)
        {
            Focusable = false;
            ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
            Image.Source = _defaultImageSource;

            _currentWindowHwnd = IntPtr.Zero;

            _currentWindow = null;

            if (_currentFrameWindow != null)
            {
                _currentFrameWindow.Hide();
                _currentFrameWindow.Close();
                _currentFrameWindow = null;
            }

            OnEndSelectWindow(new RoutedWindowEventArgs(EndSelectWindowEvent, window));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            StopSelectWindow(_canSelectCurrentWindow ? _currentWindow : null);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                StopSelectWindow(null);
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // do not run unless we are actually selecting a window.
            if (!Focusable)
                return;

            var pos = new POINT();
            
            if (!GetCursorPos(ref pos))
                return;

            var hwnd = WindowFromPoint(pos);

            if (_currentFrameWindow != null && hwnd == _currentFrameWindow.Hwnd)
                return;

            // select the main window.
            while (true)
            {
                var parentHwnd = GetParent(hwnd);

                if (parentHwnd == IntPtr.Zero)
                    break;

                hwnd = parentHwnd;
            }

            if (hwnd == _currentWindowHwnd)
                return;

            int processId;
            var threadId = GetWindowThreadProcessId(hwnd, out processId);

            var process = Process.GetProcessById(processId);

            _currentWindow = new Window
            {
                Hwnd = hwnd,
                Process = process,
            };

            _currentWindowHwnd = hwnd;

            _canSelectCurrentWindow = _selfProcessId != processId;

            if (_canSelectCurrentWindow)
            {
                try
                {
                    var path = _currentWindow.Process.MainModule.FileName;

                    _canSelectCurrentWindow = Path.GetFileName(path) != "explorer.exe";
                }
                catch (Win32Exception)
                {
                    // NB when this application is not running in 64-bit we'll get a:
                    //     System.ComponentModel.Win32Exception
                    //     A 32 bit processes cannot access modules of a 64 bit process.

                    _canSelectCurrentWindow = false;
                }
            }

            if (_canSelectCurrentWindow)
            {
                RECT rect;

                if (GetWindowRect(hwnd, out rect))
                {
                    if (_currentFrameWindow == null)
                    {
                        _currentFrameWindow = new FrameWindow();
                    }

                    _currentFrameWindow.Hide();
                    _currentFrameWindow.Resize(rect);
                }
                else
                {
                    _canSelectCurrentWindow = false;
                }
            }

            if (_currentFrameWindow != null)
            {
                if (_canSelectCurrentWindow)
                {
                    _currentFrameWindow.Show();
                }
                else
                {
                    _currentFrameWindow.Hide();
                }
            }

            OnSelectWindow(new RoutedWindowEventArgs(SelectWindowEvent, _canSelectCurrentWindow ? _currentWindow : null));
        }

        private class FrameWindow : System.Windows.Window
        {
            private const int BorderSize = 4;

            public FrameWindow()
            {
                IsEnabled = false;
                Focusable = false;
                ShowActivated = false;
                ShowInTaskbar = false;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                Background = Brushes.DodgerBlue;
            }

            public IntPtr Hwnd { get; private set; }

            public void Resize(RECT rect)
            {
                var width = rect.Right - rect.Left + 1;
                var height = rect.Bottom - rect.Top + 1;

                BeginInit();
                Left = rect.Left;
                Top = rect.Top;
                Width = width;
                Height = height;
                EndInit();

                Hwnd = new WindowInteropHelper(this).EnsureHandle();

                var region = CreateRectRgn(0, 0, width - 1, height - 1);
                var holeRegion = CreateRectRgn(BorderSize, BorderSize, width - BorderSize - 1, height - BorderSize - 1);
                CombineRgn(region, region, holeRegion, RGN_DIFF);
                DeleteObject(holeRegion);

                SetWindowRgn(Hwnd, region, true);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int RGN_AND  = 1;
        private const int RGN_OR   = 2;
        private const int RGN_XOR  = 3;
        private const int RGN_DIFF = 4;
        private const int RGN_COPY = 5;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref POINT pos);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pos);

        [DllImport("user32.dll", SetLastError=true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        [DllImport("user32.dll")]
        private static extern UInt16 SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr @object);

        [DllImport("user32.dll", SetLastError=true)]
        private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);
    }
}
