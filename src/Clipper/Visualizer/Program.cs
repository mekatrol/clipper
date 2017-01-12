using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clipper;
using UnitTests;

namespace Visualizer
{
    internal static class Program
    {
        internal static VisualizerForm VisualizerForm { get; private set; }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            VisualizerForm = new VisualizerForm();
            Application.Run(VisualizerForm);
        }
    }
}
