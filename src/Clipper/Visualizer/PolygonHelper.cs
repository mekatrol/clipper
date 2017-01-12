using System.Collections.Generic;
using System.Linq;
using Clipper;

namespace Visualizer
{
    public static class PolygonHelper
    {
        public static IReadOnlyList<Point> ToVertices(this Polygon polygon)
        {
            return polygon
                .Select(p => new Point(
                    p.X,
                    p.Y))
                .ToArray();
        }
    }
}
