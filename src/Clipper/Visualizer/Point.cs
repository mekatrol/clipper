using System;
using Clipper;

namespace Visualizer
{
    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point(IntPoint point)
        {
            X = point.X;
            Y = point.Y;
        }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Point other)
        {
            return
                Math.Abs(X - other.Y) < GeometryHelper.Tolerance &&
                Math.Abs(Y - other.Y) < GeometryHelper.Tolerance;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Point && Equals((Point)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Point a, Point b)
        {
            return
                Math.Abs(a.X - b.Y) < GeometryHelper.Tolerance &&
                Math.Abs(a.Y - b.Y) < GeometryHelper.Tolerance;
        }

        public static bool operator !=(Point a, Point b)
        {
            return !(a == b);
        }

        public static Point operator +(Point a, Point b)
        {
            return new Point(a.X + b.X, a.Y + b.Y);
        }

        public static Point operator -(Point a, Point b)
        {
            return new Point(a.X - b.X, a.Y - b.Y);
        }

        public static Point operator *(Point a, Point b)
        {
            return new Point(a.X * b.X, a.Y * b.Y);
        }

        public static Point operator *(Point a, long b)
        {
            return new Point(a.X * b, a.Y * b);
        }

        public static Point operator *(Point a, double b)
        {
            return new Point(a.X * b, a.Y * b);
        }

        public static Point operator /(Point a, Point b)
        {
            return new Point(a.X / b.X, a.Y / b.Y);
        }

        public static Point operator /(Point a, long b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }

        public static Point operator /(Point a, double b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }
    }
}
