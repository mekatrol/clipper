using System;
using System.Collections.Generic;
using System.Linq;

namespace Clipper
{
    public class PolygonPath : List<Polygon>, IClipSolution
    {
        public SolutonType SolutionType => SolutonType.Path;

        public PolygonPath() { }

        public PolygonPath(Polygon polygon) : base(1)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            Add(polygon);
        }

        public PolygonPath(IEnumerable<Polygon> polygons) : base(polygons)
        {
        }

        public PolygonPath(int capacity) : base(capacity) { }

        public double Area
        {
            get { return this.Sum(polygon => polygon.Area); }
        }

        public static PolygonPath FromTree(PolyTree tree, NodeType nodeType = NodeType.Any)
        {
            return new PolygonPath(tree.Childs.Count) { { tree, nodeType } };
        }

        private void Add(PolyNode treeNode, NodeType nodeType)
        {
            var match = true;

            switch (nodeType)
            {
                case NodeType.Open: return;
                case NodeType.Closed: match = !treeNode.IsOpen; break;
            }

            if (treeNode.Contour.Count > 0 && match)
            {
                Add(treeNode.Contour);
            }

            foreach (var polyNode in treeNode.Childs)
            {
                Add(polyNode, nodeType);
            }
        }
    }
}