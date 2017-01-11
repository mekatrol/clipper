using System;
using System.Collections.Generic;

namespace Clipper
{
    public static class GeometryHelper
    {
        public const double Tolerance = double.Epsilon;
        public const double PolygonScaleConstant = 1E7;
        public const double PolygonScaleInverseConstant = 1 / PolygonScaleConstant;
        public const double PolygonColinearityScaleConstant = 1000.0 * PolygonScaleConstant;
        public const double PolygonAreaScaleConstant = PolygonScaleConstant * PolygonScaleConstant;
        public const double PolygonAreaScaleInverseConstant = 1.0 / PolygonAreaScaleConstant;
        public const long LoRange = 0x3FFFFFFF;
        public const long HiRange = 0x3FFFFFFFFFFFFFFFL;

        /// <summary>
        /// Calulate the signed area given 3 points.
        /// </summary>
        public static long Area(IntPoint a, IntPoint b, IntPoint c)
        {
            return (c.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (c.Y - b.Y);
        }

        /// <summary>
        /// Calculate the signed area of the simple polygon defined by points.
        /// Self intersecting polygons need to be simplified for this area 
        /// calculation  to work.
        /// </summary>
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

        /// <summary>
        /// Determine the orientation of the polygon defined by points.
        /// </summary>
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

        /// <summary>
        /// Determine if the value is withing +/- Tolerance of zero.
        /// </summary>
        public static bool NearZero(double val)
        {
            return val > -Tolerance && val < Tolerance;
        }

        /// <summary>
        /// Swap the values in val1 and val2.
        /// </summary>
        /// <param name="val1"></param>
        /// <param name="val2"></param>
        public static void Swap(ref long val1, ref long val2)
        {
            var tmp = val1;
            val1 = val2;
            val2 = tmp;
        }

        /// <summary>
        /// Determines if the point lies on the line segment.
        /// </summary>
        /// <param name="point">The point to test if lies on the line segment.</param>
        /// <param name="linePoint1">The line first point.</param>
        /// <param name="linePoint2">The line second point.</param>
        /// <param name="useFullRange">Set to true to use large numbers (Int128) for caclulations.</param>
        /// <returns></returns>
        internal static bool PointOnLineSegment(
            IntPoint point,
            IntPoint linePoint1,
            IntPoint linePoint2,
            bool useFullRange)
        {
            if (useFullRange)
            {
                return
                    point.X == linePoint1.X && point.Y == linePoint1.Y ||
                    point.X == linePoint2.X && point.Y == linePoint2.Y ||
                    point.X > linePoint1.X == point.X < linePoint2.X &&
                    point.Y > linePoint1.Y == point.Y < linePoint2.Y &&
                    Int128.Int128Mul(point.X - linePoint1.X, linePoint2.Y - linePoint1.Y) == Int128.Int128Mul(linePoint2.X - linePoint1.X, point.Y - linePoint1.Y);
            }

            return point.X == linePoint1.X && point.Y == linePoint1.Y ||
                   point.X == linePoint2.X && point.Y == linePoint2.Y ||
                   point.X > linePoint1.X == point.X < linePoint2.X &&
                   point.Y > linePoint1.Y == point.Y < linePoint2.Y &&
                   (point.X - linePoint1.X) * (linePoint2.Y - linePoint1.Y) == (linePoint2.X - linePoint1.X) * (point.Y - linePoint1.Y);
        }

        /// <summary>
        /// Determines if the point is one of the vertices of the output polygon.
        /// </summary>
        internal static bool PointIsVertex(IntPoint point, OutputPoint polygon)
        {
            var polygonPoint = polygon;
            do
            {
                if (polygonPoint.Point == point) return true;
                polygonPoint = polygonPoint.Next;
            }
            while (polygonPoint != polygon);

            return false;
        }

        /// <summary>
        /// Determines if the point lies on the polygon boundary.
        /// </summary>
        internal static bool PointOnPolygon(IntPoint point, OutputPoint polygon, bool useFullRange)
        {
            var polygonPoint = polygon;
            while (true)
            {
                if (PointOnLineSegment(point, polygonPoint.Point, polygonPoint.Next.Point, useFullRange))
                {
                    return true;
                }
                polygonPoint = polygonPoint.Next;
                if (polygonPoint == polygon) break;
            }
            return false;
        }

        /// <summary>
        /// Determines if the two edge slopes are equal.
        /// </summary>
        internal static bool SlopesEqual(Edge edge1, Edge edge2, bool useFullRange)
        {
            if (useFullRange)
            {
                return Int128.Int128Mul(edge1.Delta.Y, edge2.Delta.X) == Int128.Int128Mul(edge1.Delta.X, edge2.Delta.Y);
            }

            return edge1.Delta.Y * edge2.Delta.X == edge1.Delta.X * edge2.Delta.Y;
        }

        /// <summary>
        /// Determines if the joined (at point2) line segments slopes are equal. 
        /// </summary>
        internal static bool SlopesEqual(IntPoint point1, IntPoint point2, IntPoint point3, bool useFullRange)
        {
            if (useFullRange)
                return Int128.Int128Mul(point1.Y - point2.Y, point2.X - point3.X) ==
                       Int128.Int128Mul(point1.X - point2.X, point2.Y - point3.Y);
            return
                (point1.Y - point2.Y) * (point2.X - point3.X) - (point1.X - point2.X) * (point2.Y - point3.Y) == 0;
        }

        /// <summary>
        /// Determines if the line segment slopes are equal. 
        /// </summary>
        internal static bool SlopesEqual(IntPoint point1, IntPoint point2,
            IntPoint point3, IntPoint point4, bool useFullRange)
        {
            if (useFullRange)
                return Int128.Int128Mul(point1.Y - point2.Y, point3.X - point4.X) == Int128.Int128Mul(point1.X - point2.X, point3.Y - point4.Y);
            return
                (point1.Y - point2.Y) * (point3.X - point4.X) - (point1.X - point2.X) * (point3.Y - point4.Y) == 0;
        }

        /// <summary>
        /// Tests the range of the point and determines if 
        /// full range math (Int128) should be used to accommodate 
        /// the point value range.
        /// </summary>
        internal static void RangeTest(IntPoint point, ref bool useFullRange)
        {
            while (true)
            {
                if (useFullRange)
                {
                    if (point.X > HiRange || point.Y > HiRange || -point.X > HiRange || -point.Y > HiRange)
                    {
                        throw new Exception("The polygon vertex values are outside the permitted value range.");
                    }
                }
                else if (point.X > LoRange || point.Y > LoRange || -point.X > LoRange || -point.Y > LoRange)
                {
                    useFullRange = true;
                    continue;
                }
                break;
            }
        }
    }
}
