using System;

namespace Clipper
{
    public static class ClippingHelper
    {
        public const double Horizontal = -3.4E+38;
        public const int Skip = -2;
        public const int Unassigned = -1;
        public const double Tolerance = 1.0E-20;

        public static bool Contains(PolygonPath subject, PolygonPath clip)
        {
            // The subjectPath polygon path contains the clipPath polygon path if:
            // 1. The union operation must result in one polygon result
            // 2. The area of the union equals subjectPath area.
            // 3. The area of the clipPath must be <= than the area of subjectPath.

            var solution = new PolygonPath();
            var clipper = new Clipper();
            if (!clipper.Execute(ClipOperation.Union, subject, clip, solution))
            {
                return false;
            }

            if (solution.Count != 1) return false;
            if (!GeometryHelper.NearZero(subject.Area - solution.Area)) return false;
            return clip.Area <= subject.Area;
        }

        public static bool SimplifyPolygon<T>(Polygon polygon, T solution) where T : IClipSolution
        {
            return SimplifyPolygon(new PolygonPath(polygon), solution);
        }

        public static bool SimplifyPolygon<T>(PolygonPath paths, T solution) where T : IClipSolution
        {
            var clipper = new Clipper();
            return clipper.Execute(ClipOperation.Union, paths, null, solution, true);
        }
    }
}
