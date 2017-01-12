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

            // Load unit test data.
            var testData = LoadTestHelper.LoadFromFile("../../../UnitTests/TestData/tests.txt");

            // Get test of interest.
            var test = testData[23];

            var testSubject = test.Subjects.First();
            var testClip = test.Clips.First();
            var testSolution = test.Solution.First();

            var clipperSolution = new PolygonPath();
            var clipper = new Clipper.Clipper();

            clipper.AddPaths(test.Subjects, PolygonKind.Subject);
            clipper.AddPaths(test.Clips, PolygonKind.Clip);
            clipper.Execute(ClipOperation.Union, clipperSolution);

            VisualizerForm = new VisualizerForm
            {
                ClipperViewControl =
                {
                    Subjects = new[]
                    {
                        new PolygonViewModel
                        {
                            IsOpen = false,
                            EdgeColor = Color.LawnGreen,
                            VertexColor = Color.DarkGreen,
                            Items = testSubject.ToVertices()
                        }
                    },
                                        Clips = new[]
                    {
                        new PolygonViewModel
                        {
                            IsOpen = false,
                            EdgeColor = Color.Blue,
                            VertexColor = Color.DarkBlue,
                            Items = testClip.ToVertices()
                        }
                    },

                    Boundaries = new[]
                    {
                        new BoundaryViewModel
                        {
                            IsOpen = false,
                            EdgeColor = Color.Yellow,
                            VertexColor = Color.DarkOrange,
                            Items = BoundaryBuilder
                                .BuildPolygonBoundary(testSolution, PolygonKind.Subject)
                                .ToList()
                        }
                    },

                    Fill = new[]
                    {
                        new PolygonViewModel
                        {
                            IsOpen = false,
                            EdgeColor = Color.FromArgb(60, Color.White),
                            VertexColor = Color.White,
                            Items = clipperSolution.First().ToVertices()
                        }
                    }

                }
            };

            Application.Run(VisualizerForm);
        }
    }
}
