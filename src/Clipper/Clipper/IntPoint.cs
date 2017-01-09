using System;

namespace Clipper
{
    public struct IntPoint
    {
        public long X;
        public long Y;

        public double Length => Math.Sqrt(X * X + Y * Y);

        public IntPoint(long x, long y)
        {
            X = x;
            Y = y;
        }

        public IntPoint(double x, double y)
        {
            X = (long)x;
            Y = (long)y;
        }

        public IntPoint(IntPoint pt)
        {
            X = pt.X;
            Y = pt.Y;
        }

        public IntPoint(DoublePoint point) : this(point.X, point.Y)
        {            
        }

        public bool Equals(IntPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is IntPoint && Equals((IntPoint)obj);
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
            return $"({X}, {Y})";
        }

        public static bool operator ==(IntPoint a, IntPoint b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(IntPoint a, IntPoint b)
        {
            return !(a == b);
        }

        public static DoublePoint operator +(IntPoint a, IntPoint b)
        {
            return new DoublePoint(a.X + b.X, a.Y + b.Y);
        }

        public static IntPoint operator -(IntPoint a, IntPoint b)
        {
            return new IntPoint(a.X - b.X, a.Y - b.Y);
        }

        public static IntPoint operator *(IntPoint a, IntPoint b)
        {
            return new IntPoint(a.X * b.X, a.Y * b.Y);
        }

        public static IntPoint operator *(IntPoint a, long b)
        {
            return new IntPoint(a.X * b, a.Y * b);
        }

        public static IntPoint operator *(IntPoint a, double b)
        {
            return new IntPoint(a.X * b, a.Y * b);
        }

        public static IntPoint operator /(IntPoint a, IntPoint b)
        {
            return new IntPoint(a.X / b.X, a.Y / b.Y);
        }

        public static IntPoint operator /(IntPoint a, long b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }

        public static IntPoint operator /(IntPoint a, double b)
        {
            var scale = 1.0 / b;
            return a * scale;
        }
    }
}