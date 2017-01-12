using Clipper;

namespace Visualizer
{
    public class Edge
    {
        public IntPoint Bottom;
        public IntPoint Top;
        public IntPoint Current;
        public IntPoint Delta;

        public double Dx;

        public Edge Next;
        public Edge Prev;

        public Edge LmlNext;

        public Edge AelPrev;
        public Edge AelNext;

        public Edge SelPrev;
        public Edge SelNext;

        public PolygonKind Kind;
        public EdgeSide Side;

        public long Parity;
        public bool Contributing;

        public bool IsHorizontal;
        public bool IsVertical;

        public bool EndIsLocalMinima;
        public bool EndIsLocalMaxima;
        public bool IsIntermediate;

        public int OutputIndex;

        public long TopX(long y)
        {
            // If at top of edge then just return top.
            if (y == Top.Y)
            {
                return Top.X;
            }

            return Bottom.X + (Dx * (y - Bottom.Y)).RoundToLong();
        }

        internal Edge GetMaximaPair()
        {
            if (Next.Top == Top && Next.LmlNext == null)
            {
                return Next;
            }

            if (Prev.Top == Top && Prev.LmlNext == null)
            {
                return Prev;
            }

            return null;
        }

        internal Edge GetMaximaPairCheckAel()
        {
            // Same as GetMaximaPair but returns null if 
            // MaxPair isn't in AEL (unless it's horizontal)
            var pair = GetMaximaPair();

            if (pair == null ||
                pair.OutputIndex == ClippingHelper.Skip ||
                pair.AelNext == pair.AelPrev &&
                !pair.IsHorizontal)
            {
                return null;
            }

            return pair;
        }

        public EdgeDirection GetHorizontalDirection(out long left, out long right)
        {
            if (Bottom.X < Top.X)
            {
                left = Bottom.X;
                right = Top.X;
                return EdgeDirection.LeftToRight;
            }

            left = Top.X;
            right = Bottom.X;
            return EdgeDirection.RightToLeft;
        }

        public Edge GetNextInAel(EdgeDirection direction)
        {
            return direction == EdgeDirection.LeftToRight
                ? AelNext
                : AelPrev;
        }

        public bool IsLmlMinima()
        {
            return Prev.LmlNext != this && Next.LmlNext != this;
        }

        public bool IsLmlMaxima(double y)
        {
            return GeometryHelper.NearZero(Top.Y - y) && LmlNext == null;
        }

        public bool IsLmlIntermediate(double y)
        {
            return GeometryHelper.NearZero(Top.Y - y) && LmlNext != null;
        }

        public static void SwapSides(Edge edge1, Edge edge2)
        {
            var side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }

        public static void SwapPolygonIndexes(Edge edge1, Edge edge2)
        {
            var outIdx = edge1.OutputIndex;
            edge1.OutputIndex = edge2.OutputIndex;
            edge2.OutputIndex = outIdx;
        }

        public override string ToString()
        {
            return $"{new Point(Bottom)}:{new Point(Top)}";
        }
    }

}
