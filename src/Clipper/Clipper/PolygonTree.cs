using System;
using System.Collections.Generic;
using System.Linq;

namespace Clipper
{
    /// <summary>
    /// A polygon tree defines a hierarchy of polygons and holes.
    /// The tree maitains the relationship structure of the nodes within the tree.
    /// For example, a tree may contain two outer nodes (non overlapping sibling nodes)
    /// while then both contain hold nodes as children.
    /// A tree can be multiple levels deep, for example:
    /// Polygon
    ///   |-- Hole
    ///   |
    ///   |-- Hole
    ///        |
    ///        |-- Polygon
    ///        |
    ///        |-- Polygon
    ///              |
    ///              |-- Hole
    /// In this example, the bottom hole in the tree is a hole inside a polygon
    /// inside a hole inside the outer polygon.
    /// </summary>
    public class PolygonTree : PolygonNode, IClipSolution
    {
        public SolutonType SolutionType => SolutonType.Tree;

        /// <summary>
        /// The list containing all polygons contained within the tree.
        /// </summary>
        public List<PolygonNode> AllPolygons { get; } = new List<PolygonNode>();

        public void Clear()
        {
            AllPolygons.Clear();
            Children.Clear();
        }

        /// <summary>
        /// Get the first node within the tree.
        /// </summary>
        /// <returns></returns>
        public PolygonNode GetFirst()
        {
            return Children.Count > 0 ? Children[0] : null;
        }

        public PolygonPath GetPath()
        {
            var path = new PolygonPath { Capacity = Children.Count };
            AddNodeToPath(this, NodeType.Any, path);
            return path;
        }

        public PolygonPath GetOpenPath()
        {
            var path = new PolygonPath { Capacity = Children.Count };
            path.AddRange(from node in Children where node.IsOpen select node.Polygon);
            return path;
        }

        public PolygonPath GetClosedPath()
        {
            var path = new PolygonPath { Capacity = Children.Count };
            AddNodeToPath(this, NodeType.Closed, path);
            return path;
        }

        private static void AddNodeToPath(PolygonNode node, NodeType nodeType, PolygonPath paths)
        {
            var match = true;

            switch (nodeType)
            {
                case NodeType.Open: return;
                case NodeType.Closed: match = !node.IsOpen; break;
                case NodeType.Any:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null);
            }

            if (node.Polygon.Count > 0 && match)
            {
                paths.Add(node.Polygon);
            }

            foreach (var childNode in node.Children)
            {
                AddNodeToPath(childNode, nodeType, paths);
            }
        }
    }
}