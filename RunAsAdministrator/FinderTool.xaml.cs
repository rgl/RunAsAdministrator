// developed by Rui Lopes (ruilopes.com). licensed under GPLv3.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            OnEndSelectWindow(new RoutedWindowEventArgs(EndSelectWindowEvent, window));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            StopSelectWindow(_currentWindow);
            _currentWindow = null;
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
            var pos = new POINT();
            
            if (!GetCursorPos(ref pos))
                return;

            var hwnd = WindowFromPoint(pos);

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

            OnSelectWindow(new RoutedWindowEventArgs(SelectWindowEvent, _selfProcessId != processId ? _currentWindow : null));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        };

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref POINT pos);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pos);

        [DllImport("user32.dll", SetLastError=true)]
        private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);
    }
}
