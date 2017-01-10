using System.Collections.Generic;

namespace Clipper
{
    public class PolygonNode
    {
        /// <summary>
        /// The polygon points for this node in the tree.
        /// </summary>
        internal Polygon Polygon { get; private set; } = new Polygon();

        /// <summary>
        /// The node parent within the tree, or null if is the root node.
        /// </summary>
        internal PolygonNode Parent { get; set; }

        /// <summary>
        /// The index of this node within the parent children list.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// This children polygons contained within this node polygon.
        /// </summary>
        internal List<PolygonNode> Children { get; set; } = new List<PolygonNode>();

        /// <summary>
        /// Returns true if this node is a hole.
        /// </summary>
        internal bool IsHole => IsHoleNode();

        /// <summary>
        /// True is this node polygon is an open (not closed) polygon.
        /// </summary>
        internal bool IsOpen { get; set; }

        internal JoinType JoinType { get; set; }

        internal EndType EndType { get; set; }

        internal void AddChild(PolygonNode child)
        {
            child.Parent = this;
            child.Index = Children.Count;
            Children.Add(child);
        }

        internal PolygonNode GetNext()
        {
            return Children.Count > 0
                ? Children[0]
                : GetNextSiblingUp();
        }

        private bool IsHoleNode()
        {
            var result = true;
            var node = Parent;

            while (node != null)
            {
                result = !result;
                node = node.Parent;
            }

            return result;
        }

        private PolygonNode GetNextSiblingUp()
        {
            if (Parent == null)
            {
                return null;
            }

            return Index == Parent.Children.Count - 1
                ? Parent.GetNextSiblingUp()
                : Parent.Children[Index + 1];
        }
    }
}