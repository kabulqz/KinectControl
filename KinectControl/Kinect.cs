using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xaml;
using Microsoft.Kinect;
using Application = System.Windows.Application;
using FlowDirection = System.Windows.FlowDirection;

namespace KinectControl
{
    internal class Kinect
    {
        private readonly MainWindow mainWindow;
        public readonly ColorManager colorManager;

        private readonly List<Stopwatch> calibrationStopwatches;
        private readonly List<Stopwatch> endControlStopwatches;
        private readonly List<GestureRecognizer> gestureRecognizers;

        private readonly List<double>[] leftArmLengths;
        private readonly List<double>[] rightArmLengths;

        private double[] calibrationProgress;
        private double[] endControlProgress;

        private readonly Body[] bodies;
        private readonly KinectSensor sensor;
        private readonly BodyFrameReader bodyFrameReader;
        private readonly List<Tuple<JointType, JointType>> bonePairs = new List<Tuple<JointType, JointType>>
        {
            new Tuple<JointType, JointType>(JointType.Head, JointType.Neck),
            new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder),
            new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid),
            new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase),

            new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft),
            new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight),
            new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft),
            new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft),
            new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft),
            new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight),
            new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight),
            new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight),

            new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft),
            new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft),
            new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft),
            new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft),

            new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight),
            new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight),
            new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight),
            new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight),
        };
        private readonly Dictionary<ulong, Dictionary<JointType, Point>> smoothedJoints;
        private readonly List<ulong> currentTrackedIds;
        private List<ulong> previousTrackedIds;

        private Screen[] allScreens;
        private double aspectRatio;
        private double miniWidth;
        private double miniHeight;

        private readonly bool[] prevSwitchLeft;
        private readonly bool[] prevSwitchRight;
        private int currentScreenIndex = 0;

        private Point? smoothedCenterBody = null;
        private Screen controllingScreen;
        public Body controllingPerson;

        public Kinect(MainWindow window)
        {
            mainWindow = window;
            colorManager = new ColorManager();

            sensor = KinectSensor.GetDefault();
            bodies = new Body[sensor.BodyFrameSource.BodyCount];
            smoothedJoints = new Dictionary<ulong, Dictionary<JointType, Point>>();
            currentTrackedIds = new List<ulong>();
            previousTrackedIds = new List<ulong>();
            calibrationProgress = new double[bodies.Length];
            endControlProgress = new double[bodies.Length];
            prevSwitchLeft = new bool[bodies.Length];
            prevSwitchRight = new bool[bodies.Length];

            leftArmLengths = new List<double>[bodies.Length];
            rightArmLengths = new List<double>[bodies.Length];

            sensor.Open();
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();

            calibrationStopwatches = new List<Stopwatch>();
            endControlStopwatches = new List<Stopwatch>();
            gestureRecognizers = new List<GestureRecognizer>();
            for (var i = 0; i < bodies.Length; i++)
            {
                calibrationStopwatches.Add(new Stopwatch());
                endControlStopwatches.Add(new Stopwatch());

                leftArmLengths[i] = new List<double>();
                rightArmLengths[i] = new List<double>();

                var recognizer = new GestureRecognizer(sensor);
                gestureRecognizers.Add(recognizer);
            }

            Console.WriteLine(@"Kinect initialized successfully");
            /*
            // TEST QUICK KEY COMBO
            SystemController.QuickKeyCombo(new []
            {
                App.Flags.Keyboard.Key.LEFT_WINDOWS,
                App.Flags.Keyboard.Key.E
            });
            */
        }

        public void ProcessBodyData(DrawingContext drawingContext)
        {
            var bodyFrame = bodyFrameReader.AcquireLatestFrame();
            if (bodyFrame == null) return;

            bodyFrame.GetAndRefreshBodyData(bodies);
            currentTrackedIds.Clear();

            for(var index = 0; index < sensor.BodyFrameSource.BodyCount; index++)
            {
                var body = bodies[index];
                if (body == null || !body.IsTracked) continue;

                var trackingId = body.TrackingId;
                currentTrackedIds.Add(trackingId);

                gestureRecognizers[index].ProcessGestures();
                if (trackingId != gestureRecognizers[index].trackingId)
                {
                    gestureRecognizers[index].trackingId = trackingId;
                    gestureRecognizers[index].isPaused = trackingId == 0;
                }

                ProcessCalibration(index);
                ProcessEndControl(index); // this method will be responsible for ending control when the user makes a specific gesture
                //ProcessControl(index); // this methd will be responsiible for implementation of controling the PC with gestures such as mouse, virtual keboard, etc.

                DrawBones(index, drawingContext);
                DrawJoints(index, drawingContext);
                DrawScreenSpace(index, drawingContext);
                CheckControllingPersonDistance(index, drawingContext);
            }

            bodyFrame.Dispose();
        }

        private void ProcessCalibration(int index)
        {
            var body = bodies[index];

            if (controllingPerson != null && !body.IsTracked)
            {
                calibrationStopwatches[index].Stop();
                calibrationStopwatches[index].Reset();
                return;
            }

            if (controllingPerson != null) return;
            
            if (gestureRecognizers[index].isCalibrating)
            {
                if (!calibrationStopwatches[index].IsRunning)
                {
                    calibrationStopwatches[index].Restart();
                }

                if (calibrationStopwatches[index].Elapsed.TotalSeconds < App.CalibrationTimeThreshold)
                {
                    var joints = body.Joints;

                    var leftForearm = GetDistance(joints[JointType.WristLeft].Position, joints[JointType.ElbowLeft].Position);
                    var leftUpperArm = GetDistance(joints[JointType.ElbowLeft].Position, joints[JointType.ShoulderLeft].Position);
                    var leftArmLength = leftForearm + leftUpperArm;
                    leftArmLengths[index].Add(leftArmLength);

                    var rightForearm = GetDistance(joints[JointType.WristRight].Position, joints[JointType.ElbowRight].Position);
                    var rightUpperArm = GetDistance(joints[JointType.ElbowRight].Position, joints[JointType.ShoulderRight].Position);
                    var rightArmLength = rightForearm + rightUpperArm;
                    rightArmLengths[index].Add(rightArmLength);
                }
                else
                {
                    calibrationStopwatches[index].Stop();
                    const float scale = 200.0f * App.WindowScaleFactor;

                    var avgLeft = leftArmLengths[index].Average();
                    var avgRight = rightArmLengths[index].Average();
                    leftArmLengths[index].Clear();
                    rightArmLengths[index].Clear();

                    var tempArmLength = avgLeft + avgRight;
                    Console.WriteLine($@"Arm Diagonal on screen (px): {tempArmLength * scale}");

                    var screenWidth = SystemParameters.PrimaryScreenWidth;
                    var screenHeight = SystemParameters.PrimaryScreenHeight;
                    aspectRatio = screenWidth / screenHeight;
                    miniHeight = (tempArmLength * scale) / Math.Sqrt(aspectRatio * aspectRatio + 1);
                    miniWidth = miniHeight * aspectRatio;

                    controllingScreen = Screen.PrimaryScreen;
                    controllingPerson = body;
                    smoothedCenterBody = null;
                    Console.WriteLine(@"New controlling person set");
                }

                calibrationProgress[index] = Math.Max(0, Math.Min(1, calibrationStopwatches[index].Elapsed.TotalSeconds / App.CalibrationTimeThreshold));
            }
            else
            {
                calibrationStopwatches[index].Stop();
                calibrationStopwatches[index].Reset();
                leftArmLengths[index].Clear();
                rightArmLengths[index].Clear();
                calibrationProgress[index] = 0;
            }
        }

        private void ProcessEndControl(int index)
        {
            var body = bodies[index];

            if (controllingPerson == null || controllingPerson.TrackingId != body.TrackingId)
            {
                endControlStopwatches[index].Stop();
                endControlStopwatches[index].Reset();
                endControlProgress[index] = 0;
                return;
            }

            if (gestureRecognizers[index].isEndingControl)
            {
                if (!endControlStopwatches[index].IsRunning)
                {
                    endControlStopwatches[index].Restart();
                }

                if (endControlStopwatches[index].Elapsed.TotalSeconds >= App.EndControlTimeThreshold)
                {
                    // End control and reset the controlling person
                    endControlStopwatches[index].Stop();
                    endControlProgress[index] = 0;
                    controllingPerson = null;
                    Console.WriteLine(@"Control ended, resetting controlling person");
                }
                else
                {
                    endControlProgress[index] = Math.Max(0, Math.Min(1, endControlStopwatches[index].Elapsed.TotalSeconds / App.EndControlTimeThreshold));
                }
            }
            else
            {
                endControlStopwatches[index].Stop();
                endControlStopwatches[index].Reset();
                endControlProgress[index] = 0;
            }
        }

        private void DrawBones(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];

            var trackingId = body.TrackingId;
            var joints = body.Joints;

            var brush = new SolidColorBrush(colorManager.AssignColor(trackingId));
            brush.Freeze();

            foreach (var (firstJoint, secondJoint) in bonePairs)
            {
                if (gestureRecognizers[index].isSeated && (IsLowerLimbJoint(firstJoint) || IsLowerLimbJoint(secondJoint))) continue;

                var point1 = SmoothJointPosition(trackingId, firstJoint, ConvertToScreenSpace(joints[firstJoint].Position));
                var point2 = SmoothJointPosition(trackingId, secondJoint, ConvertToScreenSpace(joints[secondJoint].Position));

                drawingContext.DrawLine(new Pen(brush, 5 * App.WindowScaleFactor), point1, point2);
            }
        }

        private void DrawJoints(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];

            var trackingId = body.TrackingId;
            var joints = body.Joints;

            var brush = new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF));
            brush.Freeze();

            foreach (var joint in joints)
            {
                if (joint.Value.TrackingState == TrackingState.NotTracked) continue;

                if (gestureRecognizers[index].isSeated && IsLowerLimbJoint(joint.Key)) continue;

                var point = SmoothJointPosition(trackingId, joint.Key, ConvertToScreenSpace(joint.Value.Position));

                if (joint.Key == JointType.Head)
                {
                    const double size = 13.0 * App.WindowScaleFactor;
                    var head = new Rect
                    {
                        Height = size * 2,
                        Width = size * 2,
                        X = point.X - size,
                        Y = point.Y - size
                    };
                    drawingContext.DrawRoundedRectangle(brush, null, head, 5.0f * App.WindowScaleFactor, 5.0f * App.WindowScaleFactor);

                    // progress bars
                    if (controllingPerson == null && calibrationProgress[index] > 0)
                    {
                        var barWidth = head.Width * 2.5;
                        const float barHeight = 10 * App.WindowScaleFactor;

                        var barX = head.X + (head.Width - barWidth) / 2;
                        var barY = head.Y - barHeight - 10;

                        var barBackground = new Rect(barX, barY, barWidth, barHeight);
                        drawingContext.DrawRoundedRectangle(brush, null, barBackground, 5.0f, 5.0f);

                        var bodyColor = colorManager.AssignColor(trackingId);
                        var bodyColorBrush = new SolidColorBrush(Color.FromArgb(App.Alpha, bodyColor.R, bodyColor.G, bodyColor.B));
                        bodyColorBrush.Freeze();

                        var filledWidth = barWidth * calibrationProgress[index];
                        var barForeground = new Rect(barX, barY, filledWidth, barHeight);
                        drawingContext.DrawRoundedRectangle(bodyColorBrush, null, barForeground, 5.0f, 5.0f);
                    }
                    else if (controllingPerson == body && endControlProgress[index] > 0)
                    {
                        var barWidth = head.Width * 2.5;
                        const float barHeight = 10 * App.WindowScaleFactor;

                        var barX = head.X + (head.Width - barWidth) / 2;
                        var barY = head.Y - barHeight - 10;

                        var barBackground = new Rect(barX, barY, barWidth, barHeight);
                        drawingContext.DrawRoundedRectangle(brush, null, barBackground, 5.0f, 5.0f);

                        var bodyColor = colorManager.AssignColor(trackingId);
                        var bodyColorBrush = new SolidColorBrush(Color.FromArgb(App.Alpha, bodyColor.R, bodyColor.G, bodyColor.B));
                        bodyColorBrush.Freeze();

                        var filledWidth = barWidth * endControlProgress[index];
                        var barForeground = new Rect(barX, barY, filledWidth, barHeight);
                        drawingContext.DrawRoundedRectangle(bodyColorBrush, null, barForeground, 5.0f, 5.0f);
                    }
                }
                else
                {
                    const double radius = 5 * App.WindowScaleFactor;
                    drawingContext.DrawEllipse(brush, null, point, radius, radius);
                }
            }
        }

        private void DrawScreenSpace(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];
            if (controllingPerson == null || body != controllingPerson) return;

            if (allScreens == null || allScreens.Length == 0)
                allScreens = Screen.AllScreens;

            if (currentScreenIndex < 0 || currentScreenIndex >= allScreens.Length)
                currentScreenIndex = 0;

            var recognizer = gestureRecognizers[index];
            if (!prevSwitchLeft[index] && recognizer.isSwitchingLeft)
            {
                var newIndex = (currentScreenIndex - 1 + allScreens.Length) % allScreens.Length;
                SwitchToScreen(newIndex);
            }
            if (!prevSwitchRight[index] && recognizer.isSwitchingRight)
            {
                var newIndex = (currentScreenIndex + 1) % allScreens.Length;
                SwitchToScreen(newIndex);
            }
            prevSwitchLeft[index] = recognizer.isSwitchingLeft;
            prevSwitchRight[index] = recognizer.isSwitchingRight;

            var joints = body.Joints;
            if (!joints.TryGetValue(JointType.ShoulderRight, out var joint)) return;

            var centerBodyRaw = ConvertToScreenSpace(joint.Position);
            var centerBody = SmoothJointPosition(body.TrackingId, JointType.ShoulderRight, centerBodyRaw);
            if (smoothedCenterBody == null) smoothedCenterBody = centerBody;
            else
            {
                smoothedCenterBody = new Point(
                    smoothedCenterBody.Value.X + (centerBody.X - smoothedCenterBody.Value.X) * 0.1,
                    smoothedCenterBody.Value.Y + (centerBody.Y - smoothedCenterBody.Value.Y) * 0.1
                );
            }

            var reference = controllingScreen ?? Screen.PrimaryScreen;
            var scaleX = miniWidth / reference.Bounds.Width;
            var scaleY = miniHeight / reference.Bounds.Height;
            var scale = Math.Min(scaleX, scaleY);

            var miniTopLeft = new Point(smoothedCenterBody.Value.X - miniWidth / 2, smoothedCenterBody.Value.Y - miniHeight / 2);

            Rect? controllingMonitorMiniRect = null;
            foreach (var screen in allScreens)
            {
                var bounds = screen.Bounds;

                var relativeX = bounds.X - reference.Bounds.X;
                var relativeY = bounds.Y - reference.Bounds.Y;

                var width = bounds.Width * scale;
                var height = bounds.Height * scale;
                var left = miniTopLeft.X + (relativeX * scale);
                var top = miniTopLeft.Y + (relativeY * scale);

                var screenRect = new Rect(left, top, width, height);

                if (Equals(screen, reference))
                    controllingMonitorMiniRect = screenRect;

                var brushColor = Equals(screen, reference)
                    ? Color.FromArgb(App.Alpha, 0x88, 0x88, 0x88)
                    : Color.FromArgb(App.Alpha, 0x66, 0x66, 0x66);

                var brush = new SolidColorBrush(brushColor);
                var pen = new Pen(brush, 4 * App.WindowScaleFactor);

                drawingContext.DrawRectangle(null, pen, screenRect);
            }

            if (controllingMonitorMiniRect.HasValue)
            {
                MoveMouseAccordingToScreenSpace(index, controllingMonitorMiniRect.Value);
            }
        }
        private void SwitchToScreen(int index)
        {
            currentScreenIndex = index;
            controllingScreen = allScreens[currentScreenIndex];
            Console.WriteLine($"Switched to screen {currentScreenIndex}: {controllingScreen.DeviceName}");
#if !DEBUG
            var bounds = controllingScreen.Bounds;
            mainWindow.WindowState = WindowState.Normal; // turn off temporarily to move the application window
            mainWindow.Left = bounds.Left;
            mainWindow.Top = bounds.Top;
            mainWindow.Width = bounds.Width;
            mainWindow.Height = bounds.Height;
            mainWindow.WindowState = WindowState.Maximized; // go back to the old setting
            Console.WriteLine($"App moved to bounds: {bounds.Left}, {bounds.Top}, {bounds.Width}x{bounds.Height}");
#endif
        }

        private bool isDragging;
        private bool isLeftDown;
        private bool isRightDown;
        private bool isMiddleDown;
        private DateTime lastClickTime = DateTime.MinValue;
        private const int DoubleClickThresholdMs = 400;
        private System.Drawing.Point lastMousePos;
        private void MoveMouseAccordingToScreenSpace(int index, Rect miniScreen)
        {
            var body = bodies[index];
            if (controllingPerson == null || body != controllingPerson) return;

            var joints = body.Joints;
            if (!joints.TryGetValue(JointType.HandRight, out var handJoint)) return;

            var handPointRaw = ConvertToScreenSpace(handJoint.Position);
            var handPoint = SmoothJointPosition(body.TrackingId, JointType.HandRight, handPointRaw);

            var reference = controllingScreen ?? Screen.PrimaryScreen;
            var referenceBounds = reference.Bounds;

            var relativeX = (handPoint.X - miniScreen.X) / miniScreen.Width;
            var relativeY = (handPoint.Y - miniScreen.Y) / miniScreen.Height;

            var screenX = (int)Math.Round(referenceBounds.X + relativeX * referenceBounds.Width);
            var screenY = (int)Math.Round(referenceBounds.Y + relativeY * referenceBounds.Height);

            var isLeftHandInMiniScreen = false;
            if (joints.TryGetValue(JointType.HandLeft, out var leftHandJoint))
            {
                var leftHandPoint = ConvertToScreenSpace(leftHandJoint.Position);
                isLeftHandInMiniScreen = miniScreen.Contains(leftHandPoint);
            }

            HandleLeftClick(body, screenX, screenY);
            HandleRightClick(body, isLeftHandInMiniScreen);
            HandleMiddleClick(body, isLeftHandInMiniScreen);

            if (!gestureRecognizers[index].isStoppingCursor)
            {
                if (!isDragging)
                {
                    SystemController.MoveTo(screenX, screenY);
                    lastMousePos = new System.Drawing.Point(screenX, screenY);
                }
                else
                {
                    var dx = screenX - lastMousePos.X;
                    var dy = screenY - lastMousePos.Y;
                    SystemController.MoveBy(dx, dy);
                    lastMousePos.X += dx;
                    lastMousePos.Y += dy;
                }
            }
        }
        private void HandleLeftClick(Body body, int screenX, int screenY)
        {
            var handState = body.HandRightState;

            if (handState == HandState.Closed)
            {
                if (!isLeftDown)
                {
                    var now = DateTime.Now;
                    var diff = (now - lastClickTime).TotalMilliseconds;

                    if (diff < DoubleClickThresholdMs)
                        SystemController.DoubleLeftClick();
                    else
                    {
                        SystemController.LeftDown();
                    }

                    lastClickTime = now;
                        
                    isLeftDown = true;
                    isDragging = false;
                    lastMousePos = new System.Drawing.Point(screenX, screenY);
                }
                else
                {
                    var dx = screenX - lastMousePos.X;
                    var dy = screenY - lastMousePos.Y;

                    if (!isDragging && (Math.Abs(dx) > 2 || Math.Abs(dy) > 2))
                        isDragging = true;
                }
            }
            else if (handState == HandState.Open)
            {
                if (isLeftDown)
                {
                    SystemController.LeftUp();
                    isLeftDown = false;
                    isDragging = false;
                }
            }
        }
        private void HandleRightClick(Body body, bool isLeftHandInMiniScreen)
        {
            if (!isLeftHandInMiniScreen) return;

            var handState = body.HandLeftState;

            if (handState == HandState.Closed)
            {
                if (!isRightDown)
                {
                    SystemController.RightDown();
                    isRightDown = true;
                }
            }
            else if (handState == HandState.Open)
            {
                if (isRightDown)
                {
                    SystemController.RightUp();
                    isRightDown = false;
                }
            }
        }
        private void HandleMiddleClick(Body body, bool isLeftHandInMiniScreen)
        {
            if (!isLeftHandInMiniScreen) return;

            var rightState = body.HandRightState;
            var leftState = body.HandLeftState;

            var bothClosed = rightState == HandState.Closed && leftState == HandState.Closed;

            if (bothClosed)
            {
                if (!isMiddleDown)
                {
                    SystemController.MiddleDown();
                    isMiddleDown = true;
                }
            }
            else
            {
                if (isMiddleDown)
                {
                    SystemController.MiddleUp();
                    isMiddleDown = false;
                }
            }
        }

        private void CheckControllingPersonDistance(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];

            if (body != controllingPerson) return;

            var joints = body.Joints;
            if (joints.ContainsKey(JointType.SpineMid) &&
                joints[JointType.SpineMid].TrackingState == TrackingState.Tracked)
            {
                var spineMid = joints[JointType.SpineMid].Position;
                var distance = spineMid.Z;

                if (distance < App.WarningDistance)
                {
                    const string message = "You're too close to the Kinect sensor";

                    var formattedText = new FormattedText(
                        message,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily(@"Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        40 * App.WindowScaleFactor,
                        new SolidColorBrush(Color.FromArgb(App.Alpha, 0x0B, 0x0B, 0x0B)),
                        VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip
                    );

                    var centerX = mainWindow.Width / 2.0 - formattedText.Width / 2.0;
                    var centerY = mainWindow.Height / 2.0 - formattedText.Height / 2.0;
                    var textPosition = new Point(centerX, centerY);

                    //outline
                    const float thickness = 5 * App.WindowScaleFactor;
                    for (var dx = -thickness; dx <= thickness; dx += thickness / 3.0f)
                    {
                        for (var dy = -thickness; dy <= thickness; dy += thickness / 3.0f)
                        {
                            if(dx * dx + dy * dy > thickness * thickness) continue;
                            if (dx == 0 && dy == 0) continue;
                            drawingContext.DrawText(formattedText, new Point(textPosition.X + dx, textPosition.Y + dy));
                        }
                    }
                    formattedText.SetForegroundBrush(new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF)));
                    drawingContext.DrawText(formattedText, textPosition);
                }
            }
        }

        public void RemoveUntrackedBodies()
        {
            foreach (var oldId in previousTrackedIds.Where(oldId => !currentTrackedIds.Contains(oldId)))
            {
                colorManager.ReleaseColor(oldId);
            }

            if (controllingPerson != null)
            {
                var controllingId = controllingPerson.TrackingId;

                if(!currentTrackedIds.Contains(controllingId))
                {
                    controllingPerson = null;
                }
            }

            previousTrackedIds = new List<ulong>(currentTrackedIds);
        }

        public bool IsAvailable()
        {
            return sensor != null && sensor.IsAvailable;
        }

        public int TrackedBodyCount => currentTrackedIds.Count;

        private double GetDistance(CameraSpacePoint a, CameraSpacePoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private bool IsLowerLimbJoint(JointType joint)
        {
            return joint == JointType.HipLeft ||
                   joint == JointType.KneeLeft ||
                   joint == JointType.AnkleLeft ||
                   joint == JointType.FootLeft ||
                   joint == JointType.HipRight ||
                   joint == JointType.KneeRight ||
                   joint == JointType.AnkleRight ||
                   joint == JointType.FootRight;
        }

        private Point ConvertToScreenSpace(CameraSpacePoint point)
        {
            var centerX = mainWindow.Canvas.ActualWidth / 2;
            var centerY = mainWindow.Canvas.ActualHeight / 2;
            const float scale = 215.0f * App.WindowScaleFactor;

            return new Point(point.X * scale + centerX, point.Y * -scale + centerY);
        }

        private Point SmoothJointPosition(ulong trackingId, JointType jointType, Point newPoint)
        {
            if (!smoothedJoints.ContainsKey(trackingId))
            {
                smoothedJoints[trackingId] = new Dictionary<JointType, Point>();
            }

            var joints = smoothedJoints[trackingId];

            if (!joints.TryGetValue(jointType, out var smoothedPoint))
            {
                joints[jointType] = newPoint;
                return newPoint;
            }

            smoothedPoint.X += (newPoint.X - smoothedPoint.X) * 0.35f;
            smoothedPoint.Y += (newPoint.Y - smoothedPoint.Y) * 0.35f;

            joints[jointType] = smoothedPoint;
            return smoothedPoint;
        }
    }
}
