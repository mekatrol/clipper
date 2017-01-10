using System.Collections.Generic;

namespace Clipper
{
    internal class IntersectionList : List<IntersectNode>
    {
        private readonly Sorter _sorter = new Sorter();

        public new void Sort()
        {
            Sort(_sorter);
        }

        private class Sorter : IComparer<IntersectNode>
        {
            public int Compare(IntersectNode node1, IntersectNode node2)
            {
                var i = node2.Point.Y - node1.Point.Y;
                if (i > 0) return 1;
                if (i < 0) return -1;
                return 0;
            }
        }
    }
}
