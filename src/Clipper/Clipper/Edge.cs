namespace Clipper
{
    public class Edge
    {
        /// <summary>
        /// The edge most bottom point.
        /// </summary>
        internal IntPoint Bottom;

        /// <summary>
        /// The edge top point.
        /// </summary>
        internal IntPoint Top;

        /// <summary>
        /// Delta between top and bottom points.
        /// </summary>
        internal IntPoint Delta;

        /// <summary>
        /// The current working point for the edge, updated for each scanbeam.
        /// </summary>
        internal IntPoint Current;

        internal double Dx;
        internal PolygonKind Kind;
        internal EdgeSide Side; // Side only refers to current side of solution poly.
        internal int WindDelta; // 1 or -1 depending on winding direction
        internal int WindCount;
        internal int WindCount2; // winding count of the opposite kind
        internal int OutIndex;
        internal Edge Next;
        internal Edge Prev;

        // For LML linking
        internal Edge NextInLml;

        // For AEL linking
        internal Edge NextInAel;
        internal Edge PrevInAel;

        // For SEL linking
        internal Edge NextInSel;
        internal Edge PrevInSel;

        internal bool IsHorizontal { get; private set; }

        internal void InitializeEdge(Edge next, Edge prev, IntPoint point)
        {
            Next = next;
            Prev = prev;
            Current = point;
            OutIndex = ClippingHelper.Unassigned;
        }

        internal void InitializeEdge(PolygonKind kind)
        {
            if (Current.Y >= Next.Current.Y)
            {
                Bottom = Current;
                Top = Next.Current;
            }
            else
            {
                Top = Current;
                Bottom = Next.Current;
            }
            SetDx();
            Kind = kind;
        }

        internal void SetDx()
        {
            Delta.X = Top.X - Bottom.X;
            Delta.Y = Top.Y - Bottom.Y;

            IsHorizontal = Delta.Y == 0;

            Dx = IsHorizontal
                ? ClippingHelper.Horizontal
                : (double)Delta.X / Delta.Y;
        }
    };
}