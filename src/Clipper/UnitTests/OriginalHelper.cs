using System.Collections.Generic;
using System.Linq;
using Clipper;

namespace UnitTests
{
    public static class OriginalHelper
    {
        public static List<List<ClipperLib.IntPoint>> ToOriginal(this PolygonPath path)
        {
            return path
                .Select(subject => 
                    subject
                        .Select(s => new ClipperLib.IntPoint(s.X, s.Y))
                        .ToList())
                .ToList();
        }

        public static PolygonPath ToNew(this List<List<ClipperLib.IntPoint>> path)
        {
            return new PolygonPath(path.Select(poly => 
                new Polygon(poly.Select(point => new IntPoint(point.X, point.Y)))));
        }
    }
}
