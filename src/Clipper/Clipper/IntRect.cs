namespace Clipper
{
    public struct IntRect
    {
        public long Left;
        public long Top;
        public long Right;
        public long Bottom;

        public IntRect(long left, long top, long right, long bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public IntRect(IntRect rect)
        {
            Left = rect.Left;
            Top = rect.Top;
            Right = rect.Right;
            Bottom = rect.Bottom;
        }
    }
}