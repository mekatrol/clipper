using Clipper;

namespace Visualizer
{
    internal struct ViewEdge
    {
        public Point Bottom { get; set; }
        public Point Top { get; set; }
        public PolygonKind Kind { get; set; }
        public double Dx { get; set; }
    }
}
