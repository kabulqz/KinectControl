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
        private readonly string gestureDatabase = @"../../Src/KinectControlGestures.gbd";

        public bool isCalibrating { get; private set; }
        public bool isEndingControl { get; private set; }
        public bool isSeated { get; private set; }
        public bool isStoppingCursor { get; private set; }

        public GestureRecognizer(KinectSensor kinectSensor)
        {
            gestureFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            gestureFrameSource.TrackingIdLost += (s, e) =>
            {
                isCalibrating = false;
                isEndingControl = false;
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
                Console.Write(@"Found gestures:");
                foreach (var gesture in database.AvailableGestures)
                {
                    Console.Write($@"    [{(gesture.GestureType == GestureType.Discrete ? "D" : "C")}] {gesture.Name}");
                    gestureFrameSource.AddGesture(gesture);
                }
                Console.WriteLine();
            }
        }

        public void ProcessGestures()
        {
            using (var frame = gestureFrameReader.CalculateAndAcquireLatestFrame())
            {
                var discreteResults = frame?.DiscreteGestureResults;
                if (discreteResults == null) return;
                foreach (var gesture in gestureFrameSource.Gestures)
                {
                    if (gesture.GestureType == GestureType.Discrete)
                    {
                        switch (gesture.Name)
                        {
                            case @"Calibration":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.80f)
                                {
                                    isCalibrating = true;
                                }
                                else isCalibrating = false;
                                break;
                            }
                            case @"EndControl":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.80f)
                                {
                                    isEndingControl = true;
                                }
                                else isEndingControl = false;
                                break;
                            }
                            case @"Seated":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.80f)
                                {
                                    isSeated = true;
                                }
                                else isSeated = false;
                                break;
                            }
                            case @"StopCursor":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.025f)
                                {
                                    isStoppingCursor = true;
                                }
                                else isStoppingCursor = false;
                                break;
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
