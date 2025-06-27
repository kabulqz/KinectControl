using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace KinectControl
{
    internal class ColorManager
    {
        private Random random;
        private readonly List<Color> colors = new List<Color>
        {
            Color.FromArgb(App.Alpha, 0xFF, 0x5C, 0x4C),
            Color.FromArgb(App.Alpha, 0xF2, 0x99, 0x4A),
            Color.FromArgb(App.Alpha, 0xF4, 0xD0, 0x3F),
            Color.FromArgb(App.Alpha, 0x4C, 0xAF, 0x50),
            Color.FromArgb(App.Alpha, 0x42, 0xA5, 0xF5),
            Color.FromArgb(App.Alpha, 0xAB, 0x47, 0xBC)
        };

        private readonly List<Tuple<int, Color>> availableColors;
        private readonly List<Tuple<ulong, int, Color>> assignedColors;

        public ColorManager()
        {
            random = new Random();
            availableColors = new List<Tuple<int, Color>>();
            assignedColors = new List<Tuple<ulong, int, Color>>();

            for (var i = 0; i < colors.Count; i++)
            {
                availableColors.Add(new Tuple<int, Color>(i, colors[i]));
            }

            Console.WriteLine(@"Color manager initialized");
        }

        public Color AssignColor(ulong trackedId)
        {
            foreach (var entry in assignedColors.Where(entry => entry.Item1 == trackedId))
            {
                return entry.Item3;
            }

            if (!availableColors.Any())
            {
                return Color.FromArgb(App.Alpha, 0xFF, 0xFF, 0xFF);
            }

            var randomIndex = random.Next(availableColors.Count);
            var colorTuple = availableColors[randomIndex];
            availableColors.RemoveAt(randomIndex);

            assignedColors.Add(new Tuple<ulong, int, Color>(trackedId, colorTuple.Item1, colorTuple.Item2));
            Console.WriteLine($@"Color nr {colorTuple.Item1} is now assigned to tracked id: {trackedId}");

            return colorTuple.Item2;
        }

        public void ReleaseColor(ulong trackedId)
        {
            var toRemove = assignedColors.Where(entry => entry.Item1 == trackedId).ToList();

            foreach (var entry in toRemove)
            {
                availableColors.Add(new Tuple<int, Color>(entry.Item2, entry.Item3));
                Console.WriteLine($@"Color nr {entry.Item2} is now available");
            }

            assignedColors.RemoveAll(entry => entry.Item1 == trackedId);
        }

        public List<Tuple<ulong, int, Color>> GetAssignedColors()
        {
            return assignedColors;
        }
    }
}