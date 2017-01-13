namespace Clipper
{
    internal class OutputPoint
    {
        internal int Index;
        internal IntPoint Point;
        internal OutputPoint Next;
        internal OutputPoint Prev;

        internal double Area
        {
            get
            {
                var partialPolygon = this;
                var first = partialPolygon;
                var a = 0.0;

                do
                {
                    a = a + (double)(partialPolygon.Prev.Point.X + partialPolygon.Point.X) * (partialPolygon.Prev.Point.Y - partialPolygon.Point.Y);
                    partialPolygon = partialPolygon.Next;
                } while (partialPolygon != first);

                return a * 0.5;
            }
        }
    }
}