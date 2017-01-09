using System;
using System.Collections.Generic;
using System.Linq;

namespace Clipper
{
    public class Polygon : List<IntPoint>
    {
        public PolygonOrientation Orientation
        {
            get
            {
                return Area >= 0
                    ? PolygonOrientation.CounterClockwise
                    : PolygonOrientation.Clockwise;
            }

            set
            {
                if (Orientation != value)
                {
                    Reverse();
                }
            }
        }

        public Polygon() { }

        public Polygon(IEnumerable<IntPoint> points) : base(points)
        {
        }

        public Polygon(int capacity) : base(capacity) { }

        public double Area
        {
            get
            {
                if (Count < 3)
                {
                    return 0;
                }

                // Calculate the parallelogram area.
                double a = 0;
                for (int i = 0, j = Count - 1; i < Count; ++i)
                {
                    a += ((double)this[j].X + this[i].X) * ((double)this[j].Y - this[i].Y);
                    j = i;
                }

                // Triangle area is half of parallelogram area.
                return -a * 0.5;
            }
        }

        public bool IsClosed { get; set; } = true;

        /// <summary>
        /// Sort points such that the bottom left point is first,
        /// and winding order is maintained.
        /// </summary>
        public void OrderBottomLeftFirst()
        {
            // Find the index of the bottom left point.
            var points = ToArray().OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            var index = IndexOf(points.First());

            // Bottom left already first?
            if (index == 0) return;

            var part1 = this.Skip(index).ToArray();
            var part2 = this.Take(index).ToArray();

            Clear();
            AddRange(part1);
            AddRange(part2);
        }

        public void Simplify(double epsilon = GeometryHelper.PolygonColinearityScaleConstant)
        {
            // Step 1: Remove duplicate consecutive vertices.
            var i = 0;
            while (i < Count && Count >= 2)
            {
                var iPlus1 = (i + 1) % Count;

                if (this[i] == this[iPlus1])
                {
                    RemoveAt(iPlus1);
                    continue;
                }

                i++;
            }

            // Step 2: Remove collinear edges.
            i = 0;
            while (i < Count && Count >= 3)
            {
                var iPlus1 = (i + 1) % Count;
                var iPlus2 = (i + 2) % Count;

                var area = Math.Abs(GeometryHelper.Area(this[i], this[iPlus1], this[iPlus2]));

                if (area < epsilon)
                {
                    RemoveAt(iPlus1);
                    continue;
                }

                i++;
            }
        }
    }
}
