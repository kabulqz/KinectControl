using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace KinectControl
{
    public partial class MainWindow : Window
    {
        private static void SetSystemCursorTo(string filePath)
        {
            var cursorPath = System.IO.Path.GetFullPath(filePath);
            var hCursor = LoadCursorFromFile(cursorPath);
            if (hCursor == IntPtr.Zero)
            {
                Console.WriteLine($@"LoadCursorFromFile failed. Error: {Marshal.GetLastWin32Error()}");
                return;
            }

            if (!SetSystemCursor(hCursor, OCR_NORMAL))
            {
                Console.WriteLine($@"SetSystemCursor failed. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        private static void RestoreDefaultCursors()
        {
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }
        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int wmCode = wParam.ToInt32();

                if (wmCode == WM_LBUTTONDOWN)
                {
                    // Zmień kursor na DragCur
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SetSystemCursorTo(DragCur);
                    });
                }
                else if (wmCode == WM_LBUTTONUP)
                {
                    // Zmień kursor na HandCur
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SetSystemCursorTo(HandCur);
                    });
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        private const uint OCR_NORMAL = 32512; // Standard arrow cursor
        private const int SPI_SETCURSORS = 0x0057;

        private const string HandCur = @"../../Src/hand.cur";
        private const string DragCur = @"../../Src/drag.cur";
        private Program program;


        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            AllocConsole(); Console.WriteLine(@"Console opened! debugging active");
            Title += " - DEBUG";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            
            Width = SystemParameters.PrimaryScreenWidth / 2;
            Height = SystemParameters.PrimaryScreenHeight / 2;

            var primary = Screen.PrimaryScreen.Bounds;
            Left = primary.Left + (primary.Width - Width) / 2;
            Top = primary.Top + (primary.Height - Height) / 2;

            foreach (var screen in Screen.AllScreens)
            {
                Console.WriteLine($@"Screen: {screen.DeviceName}, Primary: {screen.Primary}, Bounds: {screen.Bounds}");
            }
            Console.WriteLine($@"Window size: {Width}x{Height}");

            Canvas.Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));
            
            MouseLeftButtonDown += (s, e) => DragMove();
            
            PreviewMouseUp += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle) Close();
            };
#else
            //AllocConsole();
            Title += " - RELEASE";
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            AllowsTransparency = true;
            Topmost = true;

            var primary = Screen.PrimaryScreen.Bounds;
            Width = primary.Width;
            Height = primary.Height;
            Left = primary.Left;
            Top = primary.Top;

            Loaded += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            };
            SystemController.QuickKeyCombo(new[]
            {
                App.Flags.Keyboard.Key.LEFT_WINDOWS,
                App.Flags.Keyboard.Key.LEFT_CONTROL,
                App.Flags.Keyboard.Key.O,
            });
#endif
            // Set up the mouse hook
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            Loaded += (s, e) => SetSystemCursorTo(HandCur);
            Closed += (s, e) =>
            {
                RestoreDefaultCursors();
                UnhookWindowsHookEx(_hookID);
            };
            // Initialize Kinect and program
            program = new Program(this);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(int uAction, uint uParam, IntPtr lpvParam, int fuWinIni);
        [DllImport("user32.dll")]
        private static extern bool SetSystemCursor(IntPtr hCursor, uint id);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadCursorFromFile(string lpFileName);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}