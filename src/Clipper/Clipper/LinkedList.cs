namespace Clipper
{
    public abstract class LinkedList<T> where T : class
    {
        public T Head { get; set; }

        public bool Empty => Head == null;

        internal virtual void Clear()
        {
            Head = null;
        }
    }
}
