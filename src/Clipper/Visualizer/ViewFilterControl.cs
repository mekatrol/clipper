using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Clipper;
using ClipperLib;
using UnitTests;
using IntPoint = ClipperLib.IntPoint;

namespace Visualizer
{
    public partial class ViewFilterControl : UserControl
    {
        private enum SolutionType
        {
            Test,
            NewClipper,
            OriginalClipper
        }

        private Polygon _testSubject;
        private Polygon _testClip;

        private PolygonPath _testSolution;
        private PolygonPath _newClipperSolution;
        private PolygonPath _originalClipperSolution;

        private List<Edge> _testBoundary;
        private List<Edge> _newClipperBoundary;
        private List<Edge> _originalClipperBoundary;

        private SolutionType _solutionType = SolutionType.Test;
        private Dictionary<int, ClipExecutionData> _testData;
        private int _testNumber = 23;

        public ViewFilterControl()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (DesignMode) return;

            // Load unit test data.
            _testData = LoadTestHelper.LoadFromFile("../../../UnitTests/TestData/tests.txt");

            testListComboBox.Items.Clear();

            testListComboBox
                .Items
                    .AddRange(
                        _testData
                            .Select(t => t.Value.TestNumber)
                            .Cast<object>()
                            .ToArray()); 

            SetTest();

            testListComboBox.Text = _testNumber.ToString();
        }

        private void SetTest()
        {
            if (!_testData.ContainsKey(_testNumber)) return;

            // Get test of interest.
            var test = _testData[_testNumber];

            _testSubject = test.Subjects.FirstOrDefault();
            _testClip = test.Clips.FirstOrDefault();
            _testSolution = test.Solution;

            _newClipperSolution = new PolygonPath();

            var clipper = new Clipper.Clipper();

            clipper.AddPath(test.Subjects, PolygonKind.Subject);
            clipper.AddPath(test.Clips, PolygonKind.Clip);
            clipper.Execute(ClipOperation.Union, _newClipperSolution);

            _testBoundary = _testSolution.Any() 
                ? BoundaryBuilder
                    .BuildPolygonBoundary(_testSolution.First(), PolygonKind.Subject)
                    .ToList()
                : null;

            _newClipperBoundary = _newClipperSolution.Any()
                ? BoundaryBuilder
                    .BuildPolygonBoundary(_newClipperSolution.First(), PolygonKind.Subject)
                    .ToList()
                : null;

            var originalClipperSolution = new List<List<IntPoint>>();
            var originalClipper = new ClipperLib.Clipper();
            originalClipper.AddPaths(test.Subjects.ToOriginal(), PolyType.ptSubject, true);
            originalClipper.AddPaths(test.Clips.ToOriginal(), PolyType.ptClip, true);
            originalClipper.Execute(ClipType.ctUnion, originalClipperSolution);
            _originalClipperSolution = originalClipperSolution.ToNew();

            _originalClipperBoundary = _originalClipperSolution.Any()
                ? BoundaryBuilder
                    .BuildPolygonBoundary(_originalClipperSolution.First(), PolygonKind.Subject)
                    .ToList()
                : null;

            Program.VisualizerForm.ClipperViewControl.Subjects = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.LawnGreen,
                    VertexColor = Color.DarkGreen,
                    Items = _testSubject?.ToVertices()
                }
            };

            Program.VisualizerForm.ClipperViewControl.Clips = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = Color.Blue,
                    VertexColor = Color.DarkBlue,
                    Items = _testClip?.ToVertices()
                }
            };

            _solutionType = SolutionType.Test;
            SetSolution();
            solutionComboBox.SelectedIndex = 0;
        }

        private void SetSolution()
        {
            Color boundaryEdgeColor;
            Color boundaryVertexColor;
            IReadOnlyList<Edge> boundaryItems;

            Color fillEdgeColor;
            Color fillVertexColor;
            IReadOnlyList<Point> fillItems;

            switch (_solutionType)
            {
                case SolutionType.Test:
                    boundaryEdgeColor = Color.Yellow;
                    boundaryVertexColor = Color.DarkOrange;
                    boundaryItems = viewBoundaryCheckBox.Checked ? _testBoundary : null;

                    fillEdgeColor = Color.FromArgb(60, Color.White);
                    fillVertexColor = Color.White;
                    fillItems = viewFillCheckBox.Checked ? _testSolution.FirstOrDefault()?.ToVertices() : null;
                    break;

                case SolutionType.NewClipper:
                    boundaryEdgeColor = Color.CornflowerBlue;
                    boundaryVertexColor = Color.DarkSlateBlue;
                    boundaryItems = viewBoundaryCheckBox.Checked ? _newClipperBoundary : null;

                    fillEdgeColor = Color.FromArgb(60, Color.SkyBlue);
                    fillVertexColor = Color.SkyBlue;
                    fillItems = viewFillCheckBox.Checked ? _newClipperSolution.FirstOrDefault()?.ToVertices() : null;
                    break;

                case SolutionType.OriginalClipper:
                    boundaryEdgeColor = Color.LawnGreen;
                    boundaryVertexColor = Color.DarkGreen;
                    boundaryItems = viewBoundaryCheckBox.Checked ? _originalClipperBoundary : null;

                    fillEdgeColor = Color.FromArgb(60, Color.LightGreen);
                    fillVertexColor = Color.LightGreen;
                    fillItems = viewFillCheckBox.Checked ? _originalClipperSolution.FirstOrDefault()?.ToVertices() : null;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_solutionType));
            }


            Program.VisualizerForm.ClipperViewControl.Boundaries = new[]
            {
                new BoundaryViewModel
                {
                    IsOpen = false,
                    EdgeColor = boundaryEdgeColor,
                    VertexColor = boundaryVertexColor,
                    Items = boundaryItems
                }
            };

            Program.VisualizerForm.ClipperViewControl.Fill = new[]
            {
                new PolygonViewModel
                {
                    IsOpen = false,
                    EdgeColor = fillEdgeColor,
                    VertexColor = fillVertexColor,
                    Items = fillItems
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

        private void ViewBoundaryCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution();
        }

        private void ViewFillCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetSolution();
        }

        private void SolutionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Just cast from index.
            _solutionType = (SolutionType) solutionComboBox.SelectedIndex;
            SetSolution();
        }

        private void TestListComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            _testNumber = int.Parse(testListComboBox.Text);
            SetTest();
        }
    }
}
