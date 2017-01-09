using System;

namespace Clipper
{
    public struct DoublePoint
    {
        public double X;
        public double Y;

        public double Length => Math.Sqrt(X * X + Y * Y);

        public DoublePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public DoublePoint(DoublePoint point)
        {
            X = point.X;
            Y = point.Y;
        }

        public DoublePoint(IntPoint point)
        {
            X = point.X; Y = point.Y;
        }

        public bool Equals(DoublePoint other)
        {
            return
                Math.Abs(X - other.Y) < GeometryHelper.Tolerance &&
                Math.Abs(Y - other.Y) < GeometryHelper.Tolerance;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is DoublePoint && Equals((DoublePoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(DoublePoint a, DoublePoint b)
        {
            return
                Math.Abs(a.X - b.Y) < GeometryHelper.Tolerance &&
                Math.Abs(a.Y - b.Y) < GeometryHelper.Tolerance;
        }

        public static bool operator !=(DoublePoint a, DoublePoint b)
        {
            return !(a == b);
        }

        public static DoublePoint operator +(DoublePoint a, DoublePoint b)
        {
            return new DoublePoint(a.X + b.X, a.Y + b.Y);
        }

        public static DoublePoint operator -(DoublePoint a, DoublePoint b)
        {
            return new DoublePoint(a.X - b.X, a.Y - b.Y);
        }

        public static DoublePoint operator *(DoublePoint a, DoublePoint b)
        {
            return new DoublePoint(a.X * b.X, a.Y * b.Y);
        }

        public static DoublePoint operator *(DoublePoint a, long b)
        {
            return new DoublePoint(a.X * b, a.Y * b);
        }

        public static DoublePoint operator *(DoublePoint a, double b)
        {
            return new DoublePoint(a.X * b, a.Y * b);
        }

        public static DoublePoint operator /(DoublePoint a, DoublePoint b)
        {
            return new DoublePoint(a.X / b.X, a.Y / b.Y);
        }

        public static DoublePoint operator /(DoublePoint a, long b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }

        public static DoublePoint operator /(DoublePoint a, double b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }
    }
}