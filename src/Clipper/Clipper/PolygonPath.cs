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

        public void ReversePolygonOrientations()
        {
            foreach (var polygon in this)
            {
                polygon.Reverse();
            }
        }

        public PolygonPath Cleaned(double distance = 1.415)
        {
            var cleaned = new PolygonPath(Count);
            cleaned.AddRange(this.Select(polygon => polygon.Cleaned(distance)));
            return cleaned;
        }

        public static PolygonPath FromTree(PolygonTree tree, NodeType nodeType = NodeType.Any)
        {
            return new PolygonPath(tree.Children.Count) { { tree, nodeType } };
        }

        private void Add(PolygonNode treeNode, NodeType nodeType)
        {
            var match = true;

            switch (nodeType)
            {
                case NodeType.Open: return;
                case NodeType.Closed: match = !treeNode.IsOpen; break;
            }

            if (treeNode.Polygon.Count > 0 && match)
            {
                Add(treeNode.Polygon);
            }

            foreach (var polyNode in treeNode.Children)
            {
                Add(polyNode, nodeType);
            }
        }
    }
}