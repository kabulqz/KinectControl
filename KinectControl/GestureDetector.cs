using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;

namespace KinectControl
{
    internal class GestureDetector
    {
        private readonly VisualGestureBuilderFrameReader gestureFrameReader;
        private readonly VisualGestureBuilderFrameSource gestureFrameSource;
        private readonly string gestureDatabase = @"Database/Seated.gbd";
        GestureResultView gestureResultView;

        public GestureDetector(KinectSensor kinectSensor, GestureResultView gestureResultView)
        {
            this.gestureResultView = gestureResultView;

            gestureFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            gestureFrameSource.TrackingIdLost += GestureFrameSource_TrackingIdLost;

            gestureFrameReader = gestureFrameSource.OpenReader();
            if(gestureFrameReader != null)
            {
                gestureFrameReader.IsPaused = true;
            }

            using (var database = new VisualGestureBuilderDatabase(gestureDatabase))
            {
                foreach (var gesture in database.AvailableGestures)
                {
                    Console.WriteLine($@"Znaleziono gest: [{(gesture.GestureType == GestureType.Discrete ? "D" : "C")}] {gesture.Name}");
                    gestureFrameSource.AddGesture(gesture);
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

        public void ProcessGestures()
        {
            using (var frame = gestureFrameReader.CalculateAndAcquireLatestFrame())
            {
                if (frame == null) return;

                var discreteResults = frame.DiscreteGestureResults;
                if(discreteResults != null)
                {
                    foreach (var gesture in gestureFrameSource.Gestures)
                    {
                        if (gesture.Name.Equals(@"Seated") && gesture.GestureType == GestureType.Discrete)
                        {
                            DiscreteGestureResult result;
                            discreteResults.TryGetValue(gesture, out result);

                            if(result != null)
                            {
                                gestureResultView.UpdateGestureResult(true, result.Detected, result.Confidence);
                                Console.WriteLine($@"Znaleziono gest: {gesture.Name} [{result.Detected}] {result.Confidence}");
                            }
                        }
                    }
                }
            }
        }

        private void GestureFrameSource_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            gestureResultView.UpdateGestureResult(false, false, 0.0f);
        }
    }
}
