using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

namespace KinectControl
{
    internal class GestureRecognizer
    {
        private readonly VisualGestureBuilderFrameReader gestureFrameReader;
        private readonly VisualGestureBuilderFrameSource gestureFrameSource;
        private readonly string gestureDatabase = @"../../KinectControlGestures.gbd";

        public bool isCalibrating { get; private set; }
        public bool isSeated { get; private set; }

        public GestureRecognizer(KinectSensor kinectSensor)
        {
            gestureFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            gestureFrameSource.TrackingIdLost += (s, e) =>
            {
                isCalibrating = false;
                isSeated = false;
                Console.WriteLine(@"Lost tracking ID - resetting gesture values");
            };

            gestureFrameReader = gestureFrameSource.OpenReader();
            if (gestureFrameReader != null)
            {
                gestureFrameReader.IsPaused = true;
            }

            using (var database = new VisualGestureBuilderDatabase(gestureDatabase))
            {
                Console.WriteLine(@"Found gestures:");
                foreach (var gesture in database.AvailableGestures)
                {
                    Console.WriteLine($@"    [{(gesture.GestureType == GestureType.Discrete ? "D" : "C")}] {gesture.Name}");
                    gestureFrameSource.AddGesture(gesture);
                }
            }
        }

        public void ProcessGestures()
        {
            using (var frame = gestureFrameReader.CalculateAndAcquireLatestFrame())
            {
                if (frame == null) return;

                var discreteResults = frame.DiscreteGestureResults;
                if (discreteResults != null)
                {
                    foreach (var gesture in gestureFrameSource.Gestures)
                    {
                        if (gesture.GestureType == GestureType.Discrete)
                        {
                            if (gesture.Name.Equals(@"Calibration"))
                            {
                                discreteResults.TryGetValue(gesture, out var result);

                                if (result.Detected && result.Confidence >= 0.80f)
                                {
                                    isCalibrating = true;
                                }
                                else isCalibrating = false;
                            }

                            if (gesture.Name.Equals(@"Seated"))
                            {
                                discreteResults.TryGetValue(gesture, out var result);

                                if(result.Detected && result.Confidence >= 0.80f)
                                {
                                    isSeated = true;
                                }
                                else isSeated = false;
                            }
                        }
                    }
                }
            }
        }

        public ulong trackingId
        {
            get => gestureFrameSource.TrackingId;

            set
            {
                if (gestureFrameSource.TrackingId != value)
                {
                    gestureFrameSource.TrackingId = value;
                }
            }
        }

        public bool isPaused
        {
            get => gestureFrameReader.IsPaused;

            set
            {
                if (gestureFrameReader.IsPaused != value)
                {
                    gestureFrameReader.IsPaused = value;
                }
            }
        }
    }
}
