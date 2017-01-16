using System.Collections.Generic;

namespace Clipper
{
    internal class IntersectionList : List<IntersectNode>
    {
        private readonly Sorter _sorter = new Sorter();

        public new void Sort()
        {
            // NOTE: I had an issue with the default Sort (eg Sort(_sorter)) with some
            //       .NET versions when there were only two items in the list
            //       where they had the same Y value (Compare only returned zero for all items),
            //       So I implemented this simple bubble sort.

            // Bubble sort
            var bubbled = true;

            var i = 0;

            // Loop until have worked through all indexes 
            // we use ' < Count - 1' because we compare the second last to last.
            while (i < Count - 1 && bubbled)
            {
                // Assume we are not going to bubble (swap) any.
                bubbled = false;

                // Start at last item.
                var j = Count - 1;

                // Work backwards towards i.
                while (j > i)
                {
                    // Incorrect sort order?
                    if (_sorter.Compare(this[i], this[j]) > 0)
                    {
                        // Swap order of two items.
                        var tmp = this[i];
                        this[i] = this[j];
                        this[j] = tmp;

                        // Flag that two items were swapped (bubbled).
                        bubbled = true;
                    }

                    j--;
                }

                // Set next i value based on bubbled flag.
                i = bubbled
                    ? 0      // Start again
                    : i + 1; // Move to next index
            }
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
