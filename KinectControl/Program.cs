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
        private const int FPS = 55; // has to be this value to avoid flickering
        private readonly DrawingGroup drawingGroup;

        private readonly DispatcherTimer timer;
        private readonly MainWindow mainWindow;
        private readonly Kinect kinect;

        public Program(MainWindow window)
        {
            mainWindow = window;
            kinect = new Kinect(mainWindow);

            drawingGroup = new DrawingGroup();
            var drawingImage = new DrawingImage(drawingGroup);
            var renderedImage = new Image
            {
                Width = mainWindow.Width,
                Height = mainWindow.Height,
                Source = drawingImage
            };
            mainWindow.Canvas.Children.Add(renderedImage);

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds((int)(1000 / FPS))
            };
            timer.Tick += Render;
            timer.Start();
        }

        private void Render(object sender, EventArgs e)
        {
            using (var dc = drawingGroup.Open())
            {
                kinect.ProcessBodyData(dc);

                if (kinect.TrackedBodyCount >= 2)
                {
                    var controllingBrush = new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF));
                    if (kinect.controllingPerson != null)
                    {
                        controllingBrush = new SolidColorBrush(kinect.colorManager.AssignColor(kinect.controllingPerson.TrackingId));
                    }
                    var controllingRect = new Rect
                    {
                        Width = 120,
                        Height = 12,
                        X = mainWindow.Width / 2 - 60,
                        Y = 10
                    };
                    dc.DrawRoundedRectangle(controllingBrush, null, controllingRect, 5.0f, 5.0f);
                }

                var statusBrush = new SolidColorBrush(kinect.IsAvailable()
                    ? Color.FromArgb(App.Alpha, 0x60, 0xD3, 0x94)
                    : Color.FromArgb(App.Alpha, 0xAF, 0x1B, 0x3F));
                var statusRect = new Rect
                {
                    Width = mainWindow.Width - 10,
                    Height = mainWindow.Height - 10,
                    X = 5,
                    Y = 5
                };
                dc.DrawRoundedRectangle(null, new Pen(statusBrush, 5), statusRect, 5.0f, 5.0f);
                kinect.RemoveUntrackedBodies();

                drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, mainWindow.Width, mainWindow.Height));
            }
        }
    }
}