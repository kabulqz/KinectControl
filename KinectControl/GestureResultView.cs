using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;


namespace KinectControl
{
    internal class GestureResultView
    {
        private int bodyIndex;
        private bool isTracked;
        private bool detected;
        private float confidence;

        public GestureResultView(int bodyIndex, bool isTracked, bool detected, float confidence)
        {
            this.bodyIndex = bodyIndex;
            this.isTracked = isTracked;
            this.detected = detected;
            this.confidence = confidence;
        }

        public void UpdateGestureResult(bool isBodyTrackingIdValid, bool isGestureDetected, float detectionConfidence)
        {
            isTracked = isBodyTrackingIdValid;
            confidence = 0.0f;

            if (!isTracked)
            {
                detected = false;
            }
            else
            {
                detected = isGestureDetected;
                if (detected)
                {
                    confidence = detectionConfidence;
                }
            }
        }
    }
}
