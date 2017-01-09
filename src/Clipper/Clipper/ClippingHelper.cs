using System;

namespace Clipper
{
    public class ClippingHelper
    {
        public static bool Execute<T>(
            ClipOperation operation,
            PolygonPath subject,
            PolygonPath clip,
            T solution,
            bool simplify = false) where T : IClipSolution
        {
            var clipper = new Clipper { StrictlySimple = simplify };

            if (subject != null) { clipper.AddPaths(subject, PolyType.Subject); }
            if (clip != null) { clipper.AddPaths(clip, PolyType.Clip); }

            switch (solution.SolutionType)
            {
                case SolutonType.Path:
                    return clipper.Execute(operation, solution as PolygonPath);

                case SolutonType.Tree:
                    return clipper.Execute(operation, solution as PolyTree);

                default:
                    throw new ArgumentOutOfRangeException(nameof(solution.SolutionType));
            }
        }

        public static bool Contains(PolygonPath subject, PolygonPath clip)
        {
            // The subjectPath polygon path contains the clipPath polygon path if:
            // 1. The union operation must result in one polygon result
            // 2. The area of the union equals subjectPath area.
            // 3. The area of the clipPath must be <= than the area of subjectPath.

            var solution = new PolygonPath();
            if (!Execute(ClipOperation.Union, subject, clip, solution))
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
            return Execute(ClipOperation.Union, paths, null, solution, true);
        }
    }
}
