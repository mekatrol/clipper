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
        /// Computational Geometry (CG) 2nd Edition - Joseph O'Rourke
        /// Code 7.13 pg 244.
        /// </summary>
        public static Containment PolygonContainsPoint(IList<IntPoint> points, IntPoint point)
        {
            var rCross = 0;
            var lCross = 0;

            // Shift so that 'point' is the origin. This destroys the polygon.
            for (var i = 0; i < points.Count; i++)
            {
                points[i] -= point;
            }

            // For each edge e = (i - 1, i), see if crosses rays.
            for (var i = 0; i < points.Count; i++)
            {
                // (0, 0) is a vertex because we shifted to the origin.
                if (Math.Abs(points[i].X) < Tolerance && Math.Abs(points[i].Y) < Tolerance)
                {
                    return Containment.Vertex;
                }

                var i1 = (i + points.Count - 1) % points.Count;

                // Check if e straddles x axis, with bias above/below.
                var rStraddle = points[i].Y > 0 != points[i1].Y > 0;
                var lStraddle = points[i].Y < 0 != points[i1].Y < 0;

                if (!rStraddle && !lStraddle) continue;

                // Compute intersection of e with x axis.
                var x = (points[i].X * points[i1].Y - points[i1].X * points[i].Y) /
                        (points[i1].Y - points[i].Y);

                if (rStraddle && x > 0) { rCross++; }
                if (lStraddle && x < 0) { lCross++; }
            }

            // Point on edge if L/Rcross counts are not same parity.
            if (rCross % 2 != lCross % 2) return Containment.Edge;

            // Point inside iff an odd number of crossings.
            return rCross % 2 == 1 ? Containment.Interior : Containment.Exterior;
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

        /// <summary>
        /// Round the double value to nearest long value.
        /// </summary>
        public static long RoundToLong(this double value)
        {
            return value < 0 ? (long)(value - 0.5) : (long)(value + 0.5);
        }

        internal static double DistanceFromLineSqrd(IntPoint point, IntPoint linePoint1, IntPoint linePoint2)
        {
            //The equation of a line in general form (Ax + By + C = 0)
            //given 2 points (x¹,y¹) & (x²,y²) is ...
            //(y¹ - y²)x + (x² - x¹)y + (y² - y¹)x¹ - (x² - x¹)y¹ = 0
            //A = (y¹ - y²); B = (x² - x¹); C = (y² - y¹)x¹ - (x² - x¹)y¹
            //perpendicular distance of point (x³,y³) = (Ax³ + By³ + C)/Sqrt(A² + B²)
            //see http://en.wikipedia.org/wiki/Perpendicular_distance
            var a = linePoint1.Y - (double)linePoint2.Y;
            var b = linePoint2.X - (double)linePoint1.X;
            var c = a * linePoint1.X + b * linePoint1.Y;
            c = a * point.X + b * point.Y - c;
            return c * c / (a * a + b * b);
        }

        internal static bool SlopesNearCollinear(
            IntPoint point1, IntPoint point2, IntPoint point3, double distanceSquared)
        {
            // this function is more accurate when the point that's GEOMETRICALLY 
            // between the other 2 points is the one that's tested for distance.  
            // nb: with 'spikes', either point1 or point3 is geometrically between the other pts                    
            if (Math.Abs(point1.X - point2.X) > Math.Abs(point1.Y - point2.Y))
            {
                if (point1.X > point2.X == (point1.X < point3.X))
                {
                    return DistanceFromLineSqrd(point1, point2, point3) < distanceSquared;
                }

                return point2.X > point1.X == point2.X < point3.X
                    ? DistanceFromLineSqrd(point2, point1, point3) < distanceSquared
                    : DistanceFromLineSqrd(point3, point1, point2) < distanceSquared;
            }

            if (point1.Y > point2.Y == point1.Y < point3.Y)
            {
                return DistanceFromLineSqrd(point1, point2, point3) < distanceSquared;
            }

            return point2.Y > point1.Y == point2.Y < point3.Y
                ? DistanceFromLineSqrd(point2, point1, point3) < distanceSquared
                : DistanceFromLineSqrd(point3, point1, point2) < distanceSquared;
        }

        internal static bool PointsAreClose(IntPoint point1, IntPoint point2, double distSqrd)
        {
            var dx = (double)point1.X - point2.X;
            var dy = (double)point1.Y - point2.Y;
            return dx * dx + dy * dy <= distSqrd;
        }

        internal static bool Pt2IsBetweenPt1AndPt3(IntPoint point1, IntPoint point2, IntPoint point3)
        {
            if (point1 == point3 || point1 == point2 || point3 == point2)
            {
                return false;
            }

            if (point1.X != point3.X)
            {
                return point2.X > point1.X == point2.X < point3.X;
            }

            return point2.Y > point1.Y == point2.Y < point3.Y;
        }

        public static double GetDx(IntPoint point1, IntPoint point2)
        {
            return point1.Y == point2.Y
                ? GetDxSignedLength(point1, point2)
                : (double)(point2.X - point1.X) / (point2.Y - point1.Y);
        }

        public static double GetDxSignedLength(IntPoint point1, IntPoint point2)
        {
            // The dx field for a horizontal edge is simply the signed
            // length of the edge with the value of dx negative if the edge 
            // is oriented to the left.
            var length = (point2 - point1).Length;
            return point2.X > point1.X
                ? +length
                : -length;
        }
    }
}
