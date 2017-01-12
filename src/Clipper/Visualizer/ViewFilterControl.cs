using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Clipper;
using UnitTests;

namespace Visualizer
{
    public partial class ViewFilterControl : UserControl
    {
        private Polygon _testSubject;
        private Polygon _testClip;
        private PolygonPath _testSolution;
        private PolygonPath _clipperSolution;
        private List<Edge> _testBoundary;
        private List<Edge> _clipperBoundary;

        private bool _suppressCheckBoxEvents = false;

        public ViewFilterControl()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Load unit test data.
            var testData = LoadTestHelper.LoadFromFile("../../../UnitTests/TestData/tests.txt");

            // Get test of interest.
            var test = testData[23];

            _testSubject = test.Subjects.First();
            _testClip = test.Clips.First();
            _testSolution = test.Solution;

            _clipperSolution = new PolygonPath();
            var clipper = new Clipper.Clipper();

            clipper.AddPaths(test.Subjects, PolygonKind.Subject);
            clipper.AddPaths(test.Clips, PolygonKind.Clip);
            clipper.Execute(ClipOperation.Union, _clipperSolution);

            _testBoundary = BoundaryBuilder
                .BuildPolygonBoundary(_testSolution.First(), PolygonKind.Subject)
                .ToList();

            _clipperBoundary = BoundaryBuilder
                .BuildPolygonBoundary(_clipperSolution.First(), PolygonKind.Subject)
                .ToList();

            Program.VisualizerForm.ClipperViewControl.Subjects = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.LawnGreen,
                    VertexColor = Color.DarkGreen,
                    Items = _testSubject.ToVertices()
                }
            };

            Program.VisualizerForm.ClipperViewControl.Clips = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.Blue,
                    VertexColor = Color.DarkBlue,
                    Items = _testClip.ToVertices()
                }
            };

            SetSolution(true);

        }

        private void SetSolution(bool useTest)
        {
            if (_suppressCheckBoxEvents) return;
            _suppressCheckBoxEvents = true;
            viewTestBoundaryCheckBox.Checked = useTest & viewTestBoundaryCheckBox.Checked;
            viewTestFillCheckBox.Checked = useTest & viewTestFillCheckBox.Checked;
            viewClipperBoundaryCheckBox.Checked = !useTest & viewClipperBoundaryCheckBox.Checked;
            viewClipperFillCheckBox.Checked = !useTest & viewClipperFillCheckBox.Checked;
            _suppressCheckBoxEvents = false;

            Program.VisualizerForm.ClipperViewControl.Boundaries = new[]
            {
                new BoundaryViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.Yellow,
                    VertexColor = Color.DarkOrange,
                    Items = useTest 
                        ? viewTestBoundaryCheckBox.Checked ? _testBoundary : null
                        : viewClipperBoundaryCheckBox.Checked ? _clipperBoundary : null
                }
            };

            Program.VisualizerForm.ClipperViewControl.Fill = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.FromArgb(60, Color.White),
                    VertexColor = Color.White,
                    Items = useTest 
                    ? viewTestFillCheckBox.Checked ? _testSolution.First().ToVertices() : null 
                    : viewClipperFillCheckBox.Checked ?  _clipperSolution.First().ToVertices() : null
                }
            };
        }

        private void ViewSubjectsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewSubjects = viewSubjectsCheckBox.Checked;
        }

        private void ViewClipsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Program.VisualizerForm.ClipperViewControl.ViewClips = viewClipsCheckBox.Checked;
        }

        private void ViewTestBoundaryCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution(true);
        }

        private void ViewTestFillCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution(true);
        }

        private void ViewClipperBoundaryCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution(false);
        }

        private void ViewClipperFillCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution(false);
        }
    }
}
