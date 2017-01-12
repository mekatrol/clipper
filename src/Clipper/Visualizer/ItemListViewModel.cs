using System.Collections.Generic;
using System.Drawing;

namespace Visualizer
{
    public class ItemListViewModel<T>
    {
        public IReadOnlyList<T> Items { get; set; }
        public bool IsOpen { get; set; }
        public Color VertexColor { get; set; }
        public Color EdgeColor { get; set; }
    }
}