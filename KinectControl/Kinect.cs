using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        private MainWindow mainWindow;
        public ColorManager colorManager;

        private KinectSensor sensor;
        private BodyFrameSource bodyFrameSource;
        private BodyFrameReader bodyFrameReader;
        private readonly Body[] bodies;
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
        private readonly Dictionary<ulong, Dictionary<JointType, System.Windows.Point>> smoothedJoints;
        private readonly List<ulong> currentTrackedIds;
        private List<ulong> previousTrackedIds;
        public Body controllingPerson = null;

        public Kinect(MainWindow window)
        {
            mainWindow = window;
            colorManager = new ColorManager();

            bodies = new Body[6];
            smoothedJoints = new Dictionary<ulong, Dictionary<JointType, System.Windows.Point>>();
            currentTrackedIds = new List<ulong>();
            previousTrackedIds = new List<ulong>();

            if (!InitializeKinect())
            {
                Console.WriteLine(@"Kinect initialization failed");
                return;
            }
            Console.WriteLine(@"Kinect initialized successfully");
        }

        private bool InitializeKinect()
        {
            sensor = KinectSensor.GetDefault();
            if (sensor == null)
            {
                Console.WriteLine(@"No Kinect sensor found");
                return false;
            }

            sensor.Open();
            bodyFrameSource = sensor.BodyFrameSource;
            if (bodyFrameSource == null)
            {
                Console.WriteLine(@"Error: Couldn't get body frame source");
                return false;
            }

            bodyFrameReader = bodyFrameSource.OpenReader();
            if (bodyFrameReader == null)
            {
                Console.WriteLine(@"Error: Couldn't open body frame reader");
                return false;
            }

            return true;
        }

        public void ProcessBodyData()
        {
            var bodyFrame = bodyFrameReader.AcquireLatestFrame();

            if (bodyFrame != null)
            {
                try
                {
                    bodyFrame.GetAndRefreshBodyData(bodies);

                    ulong highestTrackingId = 0;
                    Body newControllingPerson = null;
                    currentTrackedIds.Clear();

                    foreach (var body in bodies)
                    {
                        if (body == null || !body.IsTracked) continue;

                        var trackingId = body.TrackingId;
                        currentTrackedIds.Add(trackingId);

                        if (trackingId > highestTrackingId)
                        {
                            highestTrackingId = trackingId;
                            newControllingPerson = body;
                        }

                        DrawBones(body);
                        DrawJoints(body);
                    }

                    controllingPerson = newControllingPerson;
                }
                finally
                {
                    bodyFrame.Dispose();
                }
            }
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

        private Point ConvertToScreenSpace(CameraSpacePoint point)
        {
            var centerX = mainWindow.Canvas.ActualWidth / 2;
            var centerY = mainWindow.Canvas.ActualHeight / 2;

            var scale = 200.0f * App.WindowScaleFactor;

            return new Point(point.X * scale + centerX, point.Y * -scale + centerY);
        }

        private void DrawBones(Body body)
        {
            if (body == null) return;

            ulong trackingId = body.TrackingId;

            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

            var brush = new SolidColorBrush(colorManager.AssignColor(trackingId));

            foreach (var bonePair in bonePairs)
            {
                JointType firstJoint = bonePair.Item1;
                JointType secondJoint = bonePair.Item2;

                if (joints[firstJoint].TrackingState == TrackingState.NotTracked ||
                    joints[secondJoint].TrackingState == TrackingState.NotTracked) continue;

                var point1 = SmoothJointPosition(trackingId, firstJoint, ConvertToScreenSpace(joints[firstJoint].Position));
                var point2 = SmoothJointPosition(trackingId, secondJoint, ConvertToScreenSpace(joints[secondJoint].Position));

                var line = new Line
                {
                    X1 = point1.X,
                    Y1 = point1.Y,
                    X2 = point2.X,
                    Y2 = point2.Y,
                    Stroke = brush,
                    StrokeThickness = 5 * App.WindowScaleFactor
                };
                mainWindow.Canvas.Children.Add(line);
            }
        }

        private void DrawJoints(Body body)
        {
            if (body == null) return;

            ulong trackingId = body.TrackingId;
            var joints = body.Joints;

            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF));

            foreach (var joint in joints)
            {
                if (joint.Value.TrackingState == TrackingState.NotTracked) continue;

                Point point = SmoothJointPosition(trackingId, joint.Key, ConvertToScreenSpace(joint.Value.Position));

                if (joint.Key == JointType.Head)
                {
                    const double size = 13.0 * App.WindowScaleFactor;
                    var head = new Rectangle
                    {
                        Width = size * 2,
                        Height = size * 2,
                        RadiusX = 7.0f,
                        RadiusY = 7.0f,
                        Fill = brush,
                    };
                    Canvas.SetLeft(head, point.X - size);
                    Canvas.SetTop(head, point.Y - size);
                    mainWindow.Canvas.Children.Add(head);
                }
                else
                {
                    const double radius = 8 * App.WindowScaleFactor;
                    var ellipse = new Ellipse
                    {
                        Width = radius,
                        Height = radius,
                        Fill = brush,
                    };
                    Canvas.SetLeft(ellipse, point.X - radius / 2);
                    Canvas.SetTop(ellipse, point.Y - radius / 2);
                    mainWindow.Canvas.Children.Add(ellipse);
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
    }
}
