namespace Clipper
{
    public class LocalMinimaList : LinkedList<LocalMinima>
    {
        public void Add(LocalMinima localMinima)
        {
            // First entry?
            if (Head == null)
            {
                Head = localMinima;
                return;
            }

            // Insert at head?
            if (localMinima.Y < Head.Y)
            {
                localMinima.Next = Head;
                Head = localMinima;
                return;
            }

            // Find insert point.
            var lm = Head;
            while (lm.Next != null && localMinima.Y > lm.Next.Y)
            {
                lm = lm.Next;
            }

            // Insert at ordered location.
            localMinima.Next = lm.Next;
            lm.Next = localMinima;
        }

        public LocalMinima Pop(long y)
        {
            // Pop the next local minima with matching Y or return null if
            // there are no more minimas or no local minimas with a 
            // matching Y.
            var current = Head;
            if (Head == null || Head.Y != y)
            {
                return null;
            }

            Head = Head.Next;

            return current;
        }

        public void Initialise(ScanbeamList scanbeamList)
        {
            if (Head == null)
            {
                return;
            }

            // Initialise scanbeam list from LML, and reset LML links.
            var localMinima = Head;

            while (localMinima != null)
            {
                // Add local minima Y to scanbeam list
                scanbeamList.Add(localMinima.Y);

                if (localMinima.LeftBound != null)
                {
                    // Reset the local minima vertex point to the bottom point.
                    localMinima.LeftBound.Current = localMinima.LeftBound.Bottom;
                }

                if (localMinima.RightBound != null)
                {
                    // Reset the local minima vertex point to the bottom point.
                    localMinima.RightBound.Current = localMinima.RightBound.Bottom;
                }

                // Move to next local minima
                localMinima = localMinima.Next;
            }
        }

        public Edge GetPreviousEdgeWithDifferentClippingType(Edge edge)
        {
            // Find the previous edge of the different clipping polygonKind or null if none found.
            var prev = edge.PrevInAel;

            while (
                prev != null &&
                prev.Kind != edge.Kind) // Not same clipping polygonKind
            {
                prev = prev.PrevInAel;
            }

            return prev;
        }
    }
}
