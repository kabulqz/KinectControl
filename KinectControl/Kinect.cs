using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

namespace KinectControl
{
    internal class Kinect
    {
        private readonly MainWindow mainWindow;
        public readonly ColorManager colorManager;

        private readonly List<Stopwatch> calibrationStopwatches;
        private readonly List<GestureRecognizer> gestureRecognizers;

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

            sensor.Open();
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();

            calibrationStopwatches = new List<Stopwatch>();
            gestureRecognizers = new List<GestureRecognizer>();
            for (var i = 0; i < sensor.BodyFrameSource.BodyCount; i++)
            {
                calibrationStopwatches.Add(new Stopwatch());

                var recognizer = new GestureRecognizer(sensor);
                gestureRecognizers.Add(recognizer);
            }

            Console.WriteLine(@"Kinect initialized successfully");
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

                DrawBones(index, drawingContext);
                DrawJoints(index, drawingContext);
            }

            bodyFrame.Dispose();
        }

        private void ProcessCalibration(int index)
        {
            var body = bodies[index];
            if (body == null || !body.IsTracked) return;

            if (controllingPerson != null && !body.IsTracked)
            {
                calibrationStopwatches[index].Stop();
                calibrationStopwatches[index].Reset();
                return;
            }

            if (controllingPerson != null) return;
            
            if (gestureRecognizers[index].isSeating)
            {
                if (!calibrationStopwatches[index].IsRunning)
                {
                    calibrationStopwatches[index].Restart();
                }
                else if (calibrationStopwatches[index].Elapsed.TotalSeconds >= 3)
                {
                    calibrationStopwatches[index].Stop();
                    controllingPerson = body;
                    Console.WriteLine($@"New controlling person set");
                }
            }
            else
            {
                calibrationStopwatches[index].Stop();
                calibrationStopwatches[index].Reset();
            }
        }

        private void DrawBones(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];
            if (body == null || !body.IsTracked) return;

            var trackingId = body.TrackingId;
            var joints = body.Joints;

            var brush = new SolidColorBrush(colorManager.AssignColor(trackingId));
            brush.Freeze();

            foreach (var (firstJoint, secondJoint) in bonePairs)
            {
                if (gestureRecognizers[index].isSeating && (IsLowerLimb(firstJoint) || IsLowerLimb(secondJoint))) continue;

                var point1 = SmoothJointPosition(trackingId, firstJoint, ConvertToScreenSpace(joints[firstJoint].Position));
                var point2 = SmoothJointPosition(trackingId, secondJoint, ConvertToScreenSpace(joints[secondJoint].Position));

                drawingContext.DrawLine(new Pen(brush, 5 * App.WindowScaleFactor), point1, point2);
            }
        }

        private void DrawJoints(int index, DrawingContext drawingContext)
        {
            var body = bodies[index];
            if (body == null || !body.IsTracked) return;

            var trackingId = body.TrackingId;
            var joints = body.Joints;

            var brush = new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF));
            brush.Freeze();

            foreach (var joint in joints)
            {
                if (joint.Value.TrackingState == TrackingState.NotTracked) continue;
                if (gestureRecognizers[index].isSeating && IsLowerLimb(joint.Key)) continue;

                var point = SmoothJointPosition(trackingId, joint.Key, ConvertToScreenSpace(joint.Value.Position));

                if (joint.Key == JointType.Head)
                {
                    const double size = 13.0 * App.WindowScaleFactor;
                    var head = new Rect
                    {
                        Height = size*2,
                        Width = size*2,
                        X = point.X - size,
                        Y = point.Y - size
                    };
                    drawingContext.DrawRoundedRectangle(brush, null, head, 5.0f * App.WindowScaleFactor, 5.0f * App.WindowScaleFactor);

                    // progress bar
                    if (controllingPerson == null && gestureRecognizers[index].isSeating)
                    {
                        var progress = calibrationStopwatches[index].Elapsed.TotalSeconds / 3;
                        progress = Math.Max(0, Math.Min(1, progress));

                        var barWidth = head.Width * 2.5;
                        const float barHeight = 10 * App.WindowScaleFactor;

                        var barX = head.X + (head.Width - barWidth) / 2;
                        var barY = head.Y - barHeight - 10;

                        var barBackground = new Rect(barX, barY, barWidth, barHeight);
                        drawingContext.DrawRoundedRectangle(brush, null, barBackground, 5.0f, 5.0f);

                        var bodyColor = colorManager.AssignColor(trackingId);
                        var bodyColorBrush = new SolidColorBrush(Color.FromArgb(App.Alpha, bodyColor.R, bodyColor.G, bodyColor.B));
                        bodyColorBrush.Freeze();

                        var filledWidth = barWidth * progress;
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

        private bool IsLowerLimb(JointType joint)
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
    }
}
