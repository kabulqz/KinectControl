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

namespace KinectControl
{
    public partial class MainWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        private Program program;

        public MainWindow()
        {
            InitializeComponent();

#if DEBUG
            AllocConsole();
            Console.WriteLine(@"Console opened! debugging active");

            Title += " - DEBUG";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            
            Width = SystemParameters.PrimaryScreenWidth / 2;
            Height = SystemParameters.PrimaryScreenHeight / 2;
            Console.WriteLine($@"Screen size: {SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}");
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

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            Loaded += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            };
#endif
            program = new Program(this);
        }
    }
}