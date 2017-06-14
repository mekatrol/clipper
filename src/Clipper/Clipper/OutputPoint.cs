namespace Clipper
{
    public class OutputPoint
    {
        public int Index;
        public IntPoint Point;
        public OutputPoint Next;
        public OutputPoint Prev;

        public double Area
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