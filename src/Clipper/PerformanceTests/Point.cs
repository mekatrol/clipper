using System;

namespace PerformanceTests
{
    public struct Point
    {
        public double X;
        public double Y;

        public double Length => Math.Sqrt(X * X + Y * Y);

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Point(Point point)
        {
            X = point.X;
            Y = point.Y;
        }

        public bool Equals(Point other)
        {
            return
                Math.Abs(X - other.Y) < double.Epsilon &&
                Math.Abs(Y - other.Y) < double.Epsilon;
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

        public override string ToString()
        {
            return $"({X:00000.00}, {Y:00000.00})";
        }

        public static bool operator ==(Point a, Point b)
        {
            return
                Math.Abs(a.X - b.Y) < double.Epsilon &&
                Math.Abs(a.Y - b.Y) < double.Epsilon;
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