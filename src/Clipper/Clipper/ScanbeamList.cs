namespace Clipper
{
    public class ScanbeamList : LinkedList<Scanbeam>
    {
        public void Add(long y)
        {
            // First entry?
            if (Head == null)
            {
                Head = new Scanbeam { Y = y };
                return;
            }

            // Insert at head?
            if (y < Head.Y)
            {
                var scanbeam = new Scanbeam
                {
                    Y = y,
                    Next = Head
                };

                Head = scanbeam;
                return;
            }

            // Find insert point.
            var sb = Head;
            while (sb.Next != null && y > sb.Next.Y)
            {
                sb = sb.Next;
            }

            // Duplicate Y value?
            if (y == sb.Y)
            {
                // Ignore duplicates
                return;
            }

            // Insert at ordered location.
            var inserted = new Scanbeam
            {
                Y = y,
                Next = sb.Next
            };

            sb.Next = inserted;
        }

        public bool Pop(out long y)
        {
            if (Head == null)
            {
                y = 0;
                return false;
            }

            y = Head.Y;
            Head = Head.Next;
            return true;
        }
    }

}
