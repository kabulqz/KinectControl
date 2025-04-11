using System;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows;

namespace KinectControl
{
    internal class Program
    {
        private const int FPS = 58; // has to be this value to avoid flickering
        private readonly DispatcherTimer timer;
        private readonly MainWindow mainWindow;
        private readonly Kinect kinect;

        public Program(MainWindow window)
        {
            mainWindow = window;
            kinect = new Kinect(mainWindow);

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds((int)(1000 / FPS))
            };
            timer.Tick += Render;
            timer.Start();

        }

        private void Render(object sender, EventArgs e)
        {
            mainWindow.Canvas.Children.Clear();
            kinect.ProcessBodyData();

#if DEBUG
            mainWindow.Canvas.Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));
#endif
            SolidColorBrush controllingBrush = new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF));
            if (kinect.controllingPerson != null)
            {
                controllingBrush = new SolidColorBrush(kinect.colorManager.AssignColor(kinect.controllingPerson.TrackingId));
            }
            var controllingRect = new Rectangle
            {
                Width = 120,
                Height = 12,
                RadiusX = 5,
                RadiusY = 5,
                Fill = controllingBrush
            };
            Canvas.SetLeft(controllingRect, (mainWindow.Width - controllingRect.Width) / 2);
            Canvas.SetTop(controllingRect, 10);
            mainWindow.Canvas.Children.Add(controllingRect);

            var statusRect = new Rectangle
            {
                Width = mainWindow.Width - 6,
                Height = mainWindow.Height - 6,
                RadiusX = 5,
                RadiusY = 5,
                Stroke = new SolidColorBrush(kinect.IsAvailable() ? 
                    Color.FromArgb(App.Alpha, 0x60, 0xD3, 0x94) : 
                    Color.FromArgb(App.Alpha, 0xAF, 0x1B, 0x3F)),
                StrokeThickness = 5,
                Fill = Brushes.Transparent,
            };
            Canvas.SetLeft(statusRect, 3);
            Canvas.SetTop(statusRect, 3);
            mainWindow.Canvas.Children.Add(statusRect);

            kinect.RemoveUntrackedBodies();
        }
    }
}