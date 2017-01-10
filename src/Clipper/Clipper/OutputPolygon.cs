namespace Clipper
{
    /// <summary>
    /// OutputPolygon: contains a path in the clipping solution. Edges in the AEL will
    /// carry a pointer to an OutputPolygon when they are part of the clipping solution.
    /// </summary>
    internal class OutputPolygon
    {
        internal int Index;
        internal bool IsHole;
        internal bool IsOpen;
        internal OutputPolygon FirstLeft; //see comments in clipper.pas
        internal OutputPoint Points;
        internal OutputPoint BottomPoint;
        internal PolygonNode PolygonNode;
    };
}