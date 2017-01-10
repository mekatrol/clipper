using System;
using System.Collections.Generic;

namespace Clipper
{
    public static class GeometryHelper
    {
        public const double Tolerance = Double.Epsilon;
        public const double PolygonScaleConstant = 1E7;
        public const double PolygonScaleInverseConstant = 1 / PolygonScaleConstant;
        public const double PolygonColinearityScaleConstant = 1000.0 * PolygonScaleConstant;
        public const double PolygonAreaScaleConstant = PolygonScaleConstant * PolygonScaleConstant;
        public const double PolygonAreaScaleInverseConstant = 1.0 / PolygonAreaScaleConstant;

        public static long Area(IntPoint a, IntPoint b, IntPoint c)
        {
            return (c.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (c.Y - b.Y);
        }

        public static double Area(IList<DoublePoint> points)
        {
            double area = 0;
            for (var i1 = 0; i1 < points.Count; i1++)
            {
                var i2 = i1 + 1;
                if (i2 == points.Count) { i2 = 0; }
                area += points[i1].X * points[i2].Y - points[i1].Y * points[i2].X;
            }

            return area;
        }

        public static PolygonOrientation GetOrientation(IList<DoublePoint> points)
        {
            if (points.Count < 3) return PolygonOrientation.Unknown;

            var area = Area(points);

            if (area < 0)
            {
                return PolygonOrientation.Clockwise;
            }

            if (area > 0)
            {
                return PolygonOrientation.CounterClockwise;
            }

            // Cannot determine the orientation if the points have a zero area.
            // This can occur for two reasons:
            //  1. All of the points are colinear (straight line)
            //  2. The points form a polygon that self intersects, where the area about each
            //     intersection is equal and opposite, eg a symetric figure 8 shape.
            return PolygonOrientation.Unknown;
        }

        public static bool NearZero(double val)
        {
            return val > -Tolerance && val < Tolerance;
        }

        public static void Swap(ref long val1, ref long val2)
        {
            var tmp = val1;
            val1 = val2;
            val2 = tmp;
        }
    }
}
