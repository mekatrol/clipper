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

        public Containment GeometricallyContains(IntPoint point)
        {
            return GeometryHelper.PolygonContainsPoint(this, point);
        }

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

        public Polygon Translated(IntPoint offset)
        {
            var translated = new Polygon(Count);
            for (var i = 0; i < Count; i++)
            {
                translated.Add(new IntPoint(this[i].X + offset.X, this[i].Y + offset.Y));
            }
            return translated;
        }

        public Polygon Cleaned(double distance = 1.415)
        {
            // distance = proximity in units/pixels below which vertices will be stripped. 
            // Default ~= sqrt(2) so when adjacent vertices or semi-adjacent vertices have 
            // both x & y coords within 1 unit, then the second vertex will be stripped.

            var pointCount = Count;

            if (pointCount == 0)
            {
                return new Polygon();
            }

            var outputPoints = new OutputPoint[pointCount];
            for (var i = 0; i < pointCount; ++i)
            {
                outputPoints[i] = new OutputPoint();
            }

            for (var i = 0; i < pointCount; ++i)
            {
                outputPoints[i].Point = this[i];
                outputPoints[i].Next = outputPoints[(i + 1) % pointCount];
                outputPoints[i].Next.Prev = outputPoints[i];
                outputPoints[i].Index = 0;
            }

            var distSqrd = distance * distance;
            var op = outputPoints[0];

            while (op.Index == 0 && op.Next != op.Prev)
            {
                if (GeometryHelper.PointsAreClose(op.Point, op.Prev.Point, distSqrd))
                {
                    op = ExcludeOutputPoint(op);
                    pointCount--;
                }
                else if (GeometryHelper.PointsAreClose(op.Prev.Point, op.Next.Point, distSqrd))
                {
                    ExcludeOutputPoint(op.Next);
                    op = ExcludeOutputPoint(op);
                    pointCount -= 2;
                }
                else if (GeometryHelper.SlopesNearCollinear(op.Prev.Point, op.Point, op.Next.Point, distSqrd))
                {
                    op = ExcludeOutputPoint(op);
                    pointCount--;
                }
                else
                {
                    op.Index = 1;
                    op = op.Next;
                }
            }

            if (pointCount < 3) pointCount = 0;

            var result = new Polygon(pointCount);

            for (var i = 0; i < pointCount; ++i)
            {
                result.Add(op.Point);
                op = op.Next;
            }

            return result;
        }

        private static OutputPoint ExcludeOutputPoint(OutputPoint outputPoint)
        {
            var result = outputPoint.Prev;
            result.Next = outputPoint.Next;
            outputPoint.Next.Prev = result;
            result.Index = 0;
            return result;
        }
    }
}
