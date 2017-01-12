using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Visualizer
{
    public partial class ClipperViewControl : UserControl
    {
        private IReadOnlyList<PolygonViewModel> _subjects = new List<PolygonViewModel>();
        private IReadOnlyList<PolygonViewModel> _clips = new List<PolygonViewModel>();
        private IReadOnlyList<BoundaryViewModel> _boundaries = new List<BoundaryViewModel>();
        private IReadOnlyList<PolygonViewModel> _fill = new List<PolygonViewModel>();

        private double _viewScreenSize;
        private double _offsetX;
        private double _offsetY;
        private double _polygonsMinX;
        private double _polygonsMaxX;
        private double _polygonsMinY;
        private double _polygonsMaxY;
        private double _polygonScale;
        private double _polygonsWidth;
        private double _polygonsHeight;

        private bool _viewSubjects = true;
        private bool _viewClips = true;
        private bool _viewBoundaries = true;
        private bool _viewFill = true;

        public ClipperViewControl()
        {
            InitializeComponent();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ViewSubjects
        {
            get { return _viewSubjects; }
            set
            {
                _viewSubjects = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ViewClips
        {
            get { return _viewClips; }
            set
            {
                _viewClips = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ViewBoundaries
        {
            get { return _viewBoundaries; }
            set
            {
                _viewBoundaries = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ViewFill
        {
            get { return _viewFill; }
            set
            {
                _viewFill = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<PolygonViewModel> Subjects
        {
            get { return _subjects; }
            set
            {
                _subjects = value;
                CalculateSizesScalesAndOffsets();
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<PolygonViewModel> Clips
        {
            get { return _clips; }
            set
            {
                _clips = value;
                CalculateSizesScalesAndOffsets();
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<BoundaryViewModel> Boundaries
        {
            get { return _boundaries; }
            set
            {
                _boundaries = value;
                CalculateSizesScalesAndOffsets();
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<PolygonViewModel> Fill
        {
            get { return _fill; }
            set
            {
                _fill = value;
                CalculateSizesScalesAndOffsets();
                Invalidate();
            }
        }

        public void Repaint()
        {
            CalculateSizesScalesAndOffsets();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CalculateSizesScalesAndOffsets();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            DrawGrid(e.Graphics);

            if (_viewFill && _fill != null)
            {
                foreach (var fill in _fill)
                {
                    DrawFill(e.Graphics, fill);
                }
            }

            if (_viewSubjects && _subjects != null)
            {
                foreach (var subject in _subjects)
                {
                    DrawVertices(e.Graphics, subject);
                }
            }

            if (_viewClips && _clips != null)
            {
                foreach (var clip in _clips)
                {
                    DrawVertices(e.Graphics, clip);
                }
            }

            if (!_viewBoundaries || _boundaries == null) return;

            foreach (var boundary in _boundaries)
            {
                DrawEdges(e.Graphics, boundary);
            }
        }

        private void DrawGrid(Graphics graphics)
        {
            var color = Color.FromArgb(48, Color.LightGray);
            var step = 1;
            var max = (int)Math.Max(_polygonsWidth, _polygonsHeight);

            // Determine step
            while (max / (step * 10) > 0)
            {
                step *= 10;
            }

            using (var pen = new Pen(color, 1))
            {
                for (var y = -step; y <= _polygonsHeight + step; y += step)
                {
                    var yScreen = _offsetY + y * _polygonScale;

                    graphics.DrawLine(pen,
                        (float)(_offsetX - _polygonScale),
                        (float)yScreen,
                        (float)(_offsetX + (_polygonsWidth + 1) * _polygonScale),
                        (float)yScreen);
                }

                for (var x = -step; x <= _polygonsWidth + step; x += step)
                {
                    var xScreen = _offsetX + x * _polygonScale;

                    graphics.DrawLine(pen,
                        (float)xScreen,
                        (float)(_offsetY - _polygonScale),
                        (float)xScreen,
                        (float)(_offsetY + (_polygonsHeight + 1) * _polygonScale));
                }
            }
        }

        private void DrawVertices(Graphics graphics, PolygonViewModel viewModel)
        {
            if (viewModel.Items.Count == 0) return;

            var points = TranslateAndScale(viewModel.Items);

            // Draw edges
            var count = viewModel.IsOpen ? points.Count - 1 : points.Count;

            using (var pen = new Pen(viewModel.EdgeColor))
            {
                for (var i = 0; i < count; i++)
                {
                    var point = points[i];
                    var next = points[(i + 1) % points.Count];

                    graphics.DrawLine(pen, (float)point.X, (float)point.Y, (float)next.X, (float)next.Y);
                }
            }

            // Draw vertex markers
            using (var brush = new SolidBrush(viewModel.VertexColor))
            {
                for (var i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var vertex = viewModel.Items[i];

                    var rect = new RectangleF((float)point.X - 3, (float)point.Y - 3, 6, 6);
                    graphics.FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

                    // Draw vertex label
                    var label = $"({vertex.X:####0.0}, {vertex.Y:####0.0})";

                    var labelSize = graphics.MeasureString(label, Font);

                    graphics.DrawString(label, Font, Brushes.White,
                        (float)point.X - labelSize.Width / 2,
                        (float)point.Y + labelSize.Height / 2);
                }
            }
        }

        private void DrawEdges(Graphics graphics, BoundaryViewModel viewModel)
        {
            if (viewModel.Items.Count == 0) return;

            var edges = TranslateAndScale(viewModel.Items);

            // Draw edges
            var count = viewModel.IsOpen ? edges.Count - 1 : edges.Count;

            var tags = new Dictionary<string, string>();

            using (var pen = new Pen(viewModel.EdgeColor))
            {
                for (var i = 0; i < count; i++)
                {
                    var edge = edges[i];
                    var original = viewModel.Items[i];

                    var bottom = new Point(original.Bottom);
                    var top = new Point(original.Top);

                    graphics.DrawLine(pen, (float)edge.Bottom.X, (float)edge.Bottom.Y, (float)edge.Top.X, (float)edge.Top.Y);

                    // Draw tag label
                    var tag = GenerateTag(i);
                    var tagLabel = $"{tag}";
                    var tagSize = graphics.MeasureString(tagLabel, Font);
                    var valueLabel = $":{ original.Dx:+0.000;-0.000}";
                    var valueSize = graphics.MeasureString(valueLabel, Font);
                    var totalSize = new SizeF(tagSize.Width + valueSize.Width, tagSize.Height);

                    var drawX = (float)(edge.Bottom.X + (edge.Top.X - edge.Bottom.X) / 2.0) - totalSize.Width / 2;
                    var drawY = (float)(edge.Bottom.Y + (edge.Top.Y - edge.Bottom.Y) / 2.0) - totalSize.Height / 2;

                    graphics.DrawString(tagLabel, Font, Brushes.YellowGreen, drawX, drawY);
                    graphics.DrawString(valueLabel, Font, Brushes.White, drawX + tagSize.Width, drawY);

                    tags.Add(tag, $"({bottom.X:00.00}, {bottom.Y:00.00}):({top.X:00.00}, {top.Y:00.00})");
                }
            }

            var maxTagLength = tags.Max(t => t.Key.Length);
            const int padding = 5;
            var y = 10.0f;
            foreach (var tag in tags)
            {
                var label = tag.Key.PadLeft(maxTagLength, ' ');
                var labelSize = graphics.MeasureString(label, Font);

                graphics.DrawString(label, Font, Brushes.YellowGreen, 10.0f, y);
                graphics.DrawString(tag.Value, Font, Brushes.White, 10.0f + labelSize.Width + padding, y);

                y += (labelSize.Height + padding);
            }

            // Draw vertex markers and labels
            using (var brush = new SolidBrush(viewModel.VertexColor))
            {
                foreach (var edge in edges)
                {
                    var rect = new RectangleF((float)edge.Bottom.X - 3, (float)edge.Bottom.Y - 3, 6, 6);
                    graphics.FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

                    rect = new RectangleF((float)edge.Top.X - 3, (float)edge.Top.Y - 3, 6, 6);
                    graphics.FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        private void DrawFill(Graphics graphics, PolygonViewModel viewModel)
        {
            if (viewModel.Items.Count == 0) return;

            var points = TranslateAndScale(viewModel.Items);

            var path = new GraphicsPath(FillMode.Winding);

            for (var i = 0; i < points.Count; i++)
            {
                var p1 = points[i % points.Count];
                var p2 = points[(i + 1) % points.Count];

                path.AddLine(
                    new PointF((float)p1.X, (float)p1.Y),
                    new PointF((float)p2.X, (float)p2.Y));
            }

            using (var brush = new SolidBrush(viewModel.EdgeColor))
            {
                graphics.FillPath(brush, path);
            }
        }

        private void CalculateSizesScalesAndOffsets()
        {
            var allVertices = new List<Point>();
            if (_subjects != null)
            {
                foreach (var subject in _subjects)
                {
                    allVertices.AddRange(subject.Items);
                }
            }

            if (_clips != null)
            {
                foreach (var clip in _clips)
                {
                    allVertices.AddRange(clip.Items);
                }
            }

            if (_boundaries != null)
            {
                foreach (var boundary in _boundaries)
                {
                    foreach (var edge in boundary.Items)
                    {
                        allVertices.Add(new Point(edge.Bottom));
                        allVertices.Add(new Point(edge.Top));
                    }
                }
            }

            if (_fill != null)
            {
                foreach (var fill in _fill)
                {
                    allVertices.AddRange(fill.Items);
                }
            }

            // Only proceed if there are vertices.
            if (allVertices.Count == 0)
            {
                return;
            }

            // Get the min / max of all vertices.
            _polygonsMinX = allVertices.Min(p => p.X);
            _polygonsMaxX = allVertices.Max(p => p.X);
            _polygonsMinY = allVertices.Min(p => p.Y);
            _polygonsMaxY = allVertices.Max(p => p.Y);

            // Calculate the total width and height of all polygons.
            _polygonsWidth = _polygonsMaxX - _polygonsMinX;
            _polygonsHeight = _polygonsMaxY - _polygonsMinY;

            // Calculate the ratio between polygon and screen sizes.
            var scaleRatioX = ClientRectangle.Width / _polygonsWidth;
            var scaleRatioY = ClientRectangle.Height / _polygonsHeight;

            // Factor to set the view size as a percentage of screen size.
            const double viewFactor = 0.8;

            // Set scale based on polygon being wider 
            if (scaleRatioX <= scaleRatioY)
            {
                _viewScreenSize = ClientRectangle.Width * viewFactor;
                _polygonScale = _viewScreenSize / _polygonsWidth;
            }
            // Set scale based on polygon being taller
            else
            {
                _viewScreenSize = ClientRectangle.Height * viewFactor;
                _polygonScale = _viewScreenSize / _polygonsHeight;
            }

            // Set offsets to center view in screen.
            _offsetX = (ClientRectangle.Width - _polygonsWidth * _polygonScale) / 2.0;
            _offsetY = (ClientRectangle.Height - _polygonsHeight * _polygonScale) / 2.0;
        }

        private IReadOnlyList<Point> TranslateAndScale(IReadOnlyList<Point> vertices)
        {
            if (vertices == null) { return new Point[0]; }

            /*
             * Steps to convert to screen coordinates:
             *   1. Translating the vertices to the subject and clip polygon sets to min (0, 0).
             *   2. Invert Y so that polygon lower values are toward the bottom of the screen.
             *   3. Scale the polygon to screen size.
             *   4. Offset polygon to center view.
             */
            return vertices
                // Steps 1 & 2
                .Select(v => new Point(v.X - _polygonsMinX, _polygonsMaxY - v.Y))

                // Step 3
                .Select(v => v * _polygonScale)

                // Step 4
                .Select(v => new Point(v.X + _offsetX, v.Y + _offsetY))
                .ToArray();
        }

        private IReadOnlyList<ViewEdge> TranslateAndScale(IReadOnlyList<Edge> edges)
        {
            if (edges == null) { return new ViewEdge[0]; }

            /*
             * Steps to convert to screen coordinates:
             *   1. Translating the vertices to the subject and clip polygon sets to min (0, 0).
             *   2. Invert Y so that polygon lower values are toward the bottom of the screen.
             *   3. Scale the polygon to screen size.
             *   4. Offset polygon to center view.
             */
            return edges
                .Select(e => new ViewEdge
                {
                    Bottom =
                        new Point(
                            e.Bottom.X - _polygonsMinX,
                            _polygonsMaxY - e.Bottom.Y) *

                        _polygonScale +

                        new Point(_offsetX, _offsetY),

                    Top =
                        new Point(
                            e.Top.X - _polygonsMinX,
                            _polygonsMaxY - e.Top.Y) *

                        _polygonScale +

                        new Point(_offsetX, _offsetY),

                    Kind = e.Kind,
                    Dx = e.Dx
                })
                .ToArray();
        }

        private void ClipperViewControl_DoubleClick(object sender, EventArgs e)
        {
            /*
             * Just to allow debugging at a certain screen size (triggers Scale and Offset calculations).
             */
            CalculateSizesScalesAndOffsets();
            Invalidate();
        }

        private static string GenerateTag(int index)
        {
            var builder = new StringBuilder();

            do
            {
                builder.Insert(0, (char)('A' + index % 26));
                index /= 26;
            } while (index > 0);

            return builder.ToString();
        }
    }
}
