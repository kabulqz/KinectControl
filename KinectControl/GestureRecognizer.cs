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
        private readonly string gestureDatabase = @"vgbtechs/KinectControlGestures.gbd";

        public bool isCalibrating { get; private set; }
        public bool isEndingControl { get; private set; }
        public bool isSeated { get; private set; }
        public bool isStoppingCursor { get; private set; }
        public bool isSwitchingLeft { get; private set; }
        private bool isSwitchingLeft_internal;
        public bool isSwitchingRight { get; private set; }
        private bool isSwitchingRight_internal;

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
                var continuousResults = frame?.ContinuousGestureResults;
                if (discreteResults == null || continuousResults == null) return;
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
                            case @"SwitchMonitor_Left":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.1f)
                                {
                                    isSwitchingLeft_internal = true;
                                }
                                else isSwitchingLeft_internal = false;
                                break;
                            }
                            case @"SwitchMonitor_Right":
                            {
                                discreteResults.TryGetValue(gesture, out var result);
                                if (result != null && result.Detected && result.Confidence >= 0.1f)
                                {
                                    isSwitchingRight_internal = true;
                                }
                                else isSwitchingRight_internal = false;
                                break;
                            }
                        }
                    }
                    if (gesture.GestureType == GestureType.Continuous)
                    {
                        switch (gesture.Name)
                        {
                            case @"SwitchMonitorProgress_Left":
                            {
                                continuousResults.TryGetValue(gesture, out var result);
                                if(isSwitchingLeft_internal && result != null && result.Progress >= 0.75f)
                                {
                                    isSwitchingLeft = true;
                                }
                                else
                                {
                                    isSwitchingLeft = false;
                                }
                                isSwitchingRight = false;
                                break;
                            }
                            case @"SwitchMonitorProgress_Right":
                            {
                                continuousResults.TryGetValue(gesture, out var result);
                                if(isSwitchingRight_internal && result != null && result.Progress >= 0.75f)
                                {
                                    isSwitchingRight = true;
                                }
                                else
                                {
                                    isSwitchingRight = false;
                                }
                                isSwitchingLeft = false;
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
