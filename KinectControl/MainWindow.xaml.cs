using System;
using System.Collections.Generic;
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

namespace KinectControl
{
    public partial class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
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
        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll")]
        private static extern bool DestroyCursor(IntPtr hCursor);
        private const uint OCR_NORMAL = 32512; // Standard arrow cursor
        private const uint OCR_HAND = 32649; // Hand cursor
        private const int SPI_SETCURSORS = 0x0057;
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
                SetSystemCursorToHand();
            };

            Closed += (s, e) =>
            {
                RestoreDefaultCursors();
            };
#endif
            program = new Program(this);
        }

        private static void SetSystemCursorToHand()
        {
            var hHand = LoadCursor(IntPtr.Zero, (int)OCR_HAND);
            if (hHand != IntPtr.Zero)
            {
                SetSystemCursor(hHand, OCR_NORMAL); // Set the system cursor to hand
            }
        }

        private static void RestoreDefaultCursors()
        {
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }
    }
}