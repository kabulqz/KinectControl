using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KinectControl
{
    public partial class App : Application
    {
        public const int Alpha =
#if DEBUG
            255;
#else
            (int)(255 * 66.6f) / 100;
#endif
        public const float WindowScaleFactor =
#if DEBUG
            1.0f;
#else
            2.0f;
#endif
        public const float CalibrationTimeThreshold = 3.0f;
        public const float EndControlTimeThreshold = 2.0f;
        public const float WarningDistance = 1.5f;
    }
}
