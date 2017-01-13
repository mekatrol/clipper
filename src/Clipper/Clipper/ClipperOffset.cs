using System;
using System.Collections.Generic;

namespace Clipper
{
    public class ClipperOffset
    {
        private PolygonPath _destinationPolygons;
        private Polygon _sourcePolygon;
        private Polygon _destinationPolygon;
        private readonly List<DoublePoint> _normals = new List<DoublePoint>();
        private double _delta, _sinA, _sin, _cos;
        private double _miterLim, _stepsPerRad;

        private IntPoint _lowest;
        private readonly PolygonNode _polygonNodes = new PolygonNode();

        public double ArcTolerance { get; set; }
        public double MiterLimit { get; set; }

        private const double TwpPi = Math.PI * 2;
        private const double DefaulArcTolerance = 0.25;

        public ClipperOffset(double miterLimit = 2.0, double arcTolerance = DefaulArcTolerance)
        {
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
            _lowest.X = -1;
        }

        public void Clear()
        {
            _polygonNodes.Children.Clear();
            _lowest.X = -1;
        }

        public void AddPath(Polygon path, JoinType joinType, EndType endType)
        {
            var highI = path.Count - 1;
            if (highI < 0) return;

            var newNode = new PolygonNode
            {
                JoinType = joinType,
                EndType = endType
            };

            //strip duplicate points from path and also get index to the lowest point ...
            if (endType == EndType.ClosedLine || endType == EndType.ClosedPolygon)
            {
                while (highI > 0 && path[0] == path[highI]) highI--;
            }

            newNode.Polygon.Capacity = highI + 1;
            newNode.Polygon.Add(path[0]);
            int j = 0, k = 0;

            for (var i = 1; i <= highI; i++)
            {
                if (newNode.Polygon[j] == path[i]) continue;

                j++;
                newNode.Polygon.Add(path[i]);
                if (path[i].Y > newNode.Polygon[k].Y ||
                    path[i].Y == newNode.Polygon[k].Y &&
                    path[i].X < newNode.Polygon[k].X) k = j;
            }

            if (endType == EndType.ClosedPolygon && j < 2)
            {
                return;
            }

            _polygonNodes.AddChild(newNode);

            //if this path's lowest point is lower than all the others then update _lowest
            if (endType != EndType.ClosedPolygon) return;

            if (_lowest.X < 0)
            {
                _lowest = new IntPoint(_polygonNodes.Children.Count - 1, k);
            }
            else
            {
                var ip = _polygonNodes.Children[(int)_lowest.X].Polygon[(int)_lowest.Y];

                if (newNode.Polygon[k].Y > ip.Y ||
                    newNode.Polygon[k].Y == ip.Y &&
                    newNode.Polygon[k].X < ip.X)
                {
                    _lowest = new IntPoint(_polygonNodes.Children.Count - 1, k);
                }
            }
        }

        public void AddPaths(PolygonPath paths, JoinType joinType, EndType endType)
        {
            foreach (var p in paths)
            {
                AddPath(p, joinType, endType);
            }
        }

        private void FixOrientations()
        {
            // fixup orientations of all closed paths if the orientation of the
            // closed path with the lowermost vertex is wrong ...
            if (_lowest.X >= 0 &&
                _polygonNodes.Children[(int)_lowest.X].Polygon.Orientation == PolygonOrientation.Clockwise)
            {
                foreach (var node in _polygonNodes.Children)
                {
                    if (node.EndType == EndType.ClosedPolygon ||
                        node.EndType == EndType.ClosedLine &&
                        node.Polygon.Orientation == PolygonOrientation.CounterClockwise)
                    {
                        node.Polygon.Reverse();
                    }
                }
            }
            else
            {
                foreach (var node in _polygonNodes.Children)
                {
                    if (node.EndType == EndType.ClosedLine &&
                        node.Polygon.Orientation == PolygonOrientation.Clockwise)
                    {
                        node.Polygon.Reverse();
                    }
                }
            }
        }

        internal static DoublePoint GetUnitNormal(IntPoint pt1, IntPoint pt2)
        {
            double dx = pt2.X - pt1.X;
            double dy = pt2.Y - pt1.Y;

            if (GeometryHelper.NearZero(dx) && GeometryHelper.NearZero(dy))
            {
                return new DoublePoint();
            }

            var f = 1 * 1.0 / Math.Sqrt(dx * dx + dy * dy);
            dx *= f;
            dy *= f;

            return new DoublePoint(dy, -dx);
        }

        private void DoOffset(double delta)
        {
            _destinationPolygons = new PolygonPath();
            _delta = delta;

            //if Zero offset, just copy any CLOSED polygons to m_p and return ...
            if (GeometryHelper.NearZero(delta))
            {
                _destinationPolygons.Capacity = _polygonNodes.Children.Count;
                foreach (var node in _polygonNodes.Children)
                {
                    if (node.EndType == EndType.ClosedPolygon)
                    {
                        _destinationPolygons.Add(node.Polygon);
                    }
                }
                return;
            }

            // see offset_triginometry3.svg in the documentation folder ...
            if (MiterLimit > 2)
            {
                _miterLim = 2 / (MiterLimit * MiterLimit);
            }
            else
            {
                _miterLim = 0.5;
            }

            double y;
            if (ArcTolerance <= 0.0)
            {
                y = DefaulArcTolerance;
            }
            else if (ArcTolerance > Math.Abs(delta) * DefaulArcTolerance)
            {
                y = Math.Abs(delta) * DefaulArcTolerance;
            }
            else
            {
                y = ArcTolerance;
            }

            // see offset_triginometry2.svg in the documentation folder ...
            var steps = Math.PI / Math.Acos(1 - y / Math.Abs(delta));
            _sin = Math.Sin(TwpPi / steps);
            _cos = Math.Cos(TwpPi / steps);
            _stepsPerRad = steps / TwpPi;
            if (delta < 0.0) _sin = -_sin;

            _destinationPolygons.Capacity = _polygonNodes.Children.Count * 2;

            foreach (var node in _polygonNodes.Children)
            {
                _sourcePolygon = node.Polygon;

                var len = _sourcePolygon.Count;

                if (len == 0 ||
                    delta <= 0 &&
                    (len < 3 || node.EndType != EndType.ClosedPolygon))
                {
                    continue;
                }

                _destinationPolygon = new Polygon();

                if (len == 1)
                {
                    if (node.JoinType == JoinType.Round)
                    {
                        var x = 1.0;
                        var Y = 0.0;
                        for (var j = 1; j <= steps; j++)
                        {
                            _destinationPolygon.Add(new IntPoint(
                                (_sourcePolygon[0].X + x * delta).RoundToLong(),
                                (_sourcePolygon[0].Y + Y * delta).RoundToLong()));
                            var x2 = x;
                            x = x * _cos - _sin * Y;
                            Y = x2 * _sin + Y * _cos;
                        }
                    }
                    else
                    {
                        var x = -1.0;
                        var Y = -1.0;

                        for (var j = 0; j < 4; ++j)
                        {
                            _destinationPolygon.Add(new IntPoint(
                                (_sourcePolygon[0].X + x * delta).RoundToLong(),
                                (_sourcePolygon[0].Y + Y * delta).RoundToLong()));

                            if (x < 0) x = 1;
                            else if (Y < 0) Y = 1;
                            else x = -1;
                        }
                    }
                    _destinationPolygons.Add(_destinationPolygon);
                    continue;
                }

                //build _normals ...
                _normals.Clear();
                _normals.Capacity = len;
                for (int j = 0; j < len - 1; j++)
                    _normals.Add(GetUnitNormal(_sourcePolygon[j], _sourcePolygon[j + 1]));
                if (node.EndType == EndType.ClosedLine ||
                    node.EndType == EndType.ClosedPolygon)
                    _normals.Add(GetUnitNormal(_sourcePolygon[len - 1], _sourcePolygon[0]));
                else
                    _normals.Add(new DoublePoint(_normals[len - 2]));

                if (node.EndType == EndType.ClosedPolygon)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.JoinType);
                    _destinationPolygons.Add(_destinationPolygon);
                }
                else if (node.EndType == EndType.ClosedLine)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.JoinType);
                    _destinationPolygons.Add(_destinationPolygon);
                    _destinationPolygon = new Polygon();
                    //re-build _normals ...
                    DoublePoint n = _normals[len - 1];
                    for (int j = len - 1; j > 0; j--)
                        _normals[j] = new DoublePoint(-_normals[j - 1].X, -_normals[j - 1].Y);
                    _normals[0] = new DoublePoint(-n.X, -n.Y);
                    k = 0;
                    for (int j = len - 1; j >= 0; j--)
                        OffsetPoint(j, ref k, node.JoinType);
                    _destinationPolygons.Add(_destinationPolygon);
                }
                else
                {
                    var k = 0;
                    for (var j = 1; j < len - 1; ++j)
                    {
                        OffsetPoint(j, ref k, node.JoinType);
                    }

                    IntPoint pt1;
                    if (node.EndType == EndType.OpenButt)
                    {
                        var j = len - 1;
                        pt1 = new IntPoint(
                            (_sourcePolygon[j].X + _normals[j].X * delta).RoundToLong(),
                            (_sourcePolygon[j].Y + _normals[j].Y * delta).RoundToLong());

                        _destinationPolygon.Add(pt1);

                        pt1 = new IntPoint(
                            (_sourcePolygon[j].X - _normals[j].X * delta).RoundToLong(),
                            (_sourcePolygon[j].Y - _normals[j].Y * delta).RoundToLong());

                        _destinationPolygon.Add(pt1);
                    }
                    else
                    {
                        var j = len - 1;
                        k = len - 2;
                        _sinA = 0;
                        _normals[j] = new DoublePoint(-_normals[j].X, -_normals[j].Y);
                        if (node.EndType == EndType.OpenSquare)
                        {
                            DoSquare(j, k);
                        }
                        else
                        {
                            DoRound(j, k);
                        }
                    }

                    //re-build _normals ...
                    for (int j = len - 1; j > 0; j--)
                        _normals[j] = new DoublePoint(-_normals[j - 1].X, -_normals[j - 1].Y);

                    _normals[0] = new DoublePoint(-_normals[1].X, -_normals[1].Y);

                    k = len - 1;
                    for (int j = k - 1; j > 0; --j)
                        OffsetPoint(j, ref k, node.JoinType);

                    if (node.EndType == EndType.OpenButt)
                    {
                        pt1 = new IntPoint(
                            (_sourcePolygon[0].X - _normals[0].X * delta).RoundToLong(),
                            (_sourcePolygon[0].Y - _normals[0].Y * delta).RoundToLong());

                        _destinationPolygon.Add(pt1);

                        pt1 = new IntPoint(
                            (_sourcePolygon[0].X + _normals[0].X * delta).RoundToLong(),
                            (_sourcePolygon[0].Y + _normals[0].Y * delta).RoundToLong());

                        _destinationPolygon.Add(pt1);
                    }
                    else
                    {
                        _sinA = 0;
                        if (node.EndType == EndType.OpenSquare)
                        {
                            DoSquare(0, 1);
                        }
                        else
                        {
                            DoRound(0, 1);
                        }
                    }
                    _destinationPolygons.Add(_destinationPolygon);
                }
            }
        }

        public void Execute(ref PolygonPath solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);

            //now clean up 'corners' ...
            var clipper = new Clipper();
            if (delta > 0)
            {
                clipper.Execute(
                    ClipOperation.Union, 
                    _destinationPolygons,
                    null,
                    solution,
                    false,
                    PolygonFillType.Positive, PolygonFillType.Positive);
            }
            else
            {
                var r = Clipper.GetBounds(_destinationPolygons);
                var outer = new Polygon
                {
                    new IntPoint(r.Left - 10, r.Bottom + 10),
                    new IntPoint(r.Right + 10, r.Bottom + 10),
                    new IntPoint(r.Right + 10, r.Top - 10),
                    new IntPoint(r.Left - 10, r.Top - 10)
                };

                clipper.ReverseOrientation = true;

                clipper.Execute(
                    ClipOperation.Union, 
                    new PolygonPath(outer),
                    null,
                    solution, 
                    false,
                    PolygonFillType.Negative, 
                    PolygonFillType.Negative);

                if (solution.Count > 0)
                {
                    solution.RemoveAt(0);
                }
            }
        }

        public void Execute(ref PolygonTree solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);

            //now clean up 'corners' ...
            var clipper = new Clipper();

            if (delta > 0)
            {
                clipper.Execute(
                    ClipOperation.Union, 
                    _destinationPolygons, 
                    null, 
                    solution, 
                    false,
                    PolygonFillType.Positive, PolygonFillType.Positive);
            }
            else
            {
                var r = Clipper.GetBounds(_destinationPolygons);
                var outer = new Polygon
                {
                    new IntPoint(r.Left - 10, r.Bottom + 10),
                    new IntPoint(r.Right + 10, r.Bottom + 10),
                    new IntPoint(r.Right + 10, r.Top - 10),
                    new IntPoint(r.Left - 10, r.Top - 10)
                };

                clipper.ReverseOrientation = true;

                clipper.Execute(
                    ClipOperation.Union, 
                    new PolygonPath(outer),
                    null, 
                    solution, 
                    false,
                    PolygonFillType.Negative, 
                    PolygonFillType.Negative);

                // remove the outer PolygonNode rectangle ...
                if (solution.Children.Count == 1 && solution.Children[0].Children.Count > 0)
                {
                    var outerNode = solution.Children[0];
                    solution.Children.Capacity = outerNode.Children.Count;
                    solution.Children[0] = outerNode.Children[0];
                    solution.Children[0].Parent = solution;
                    for (var i = 1; i < outerNode.Children.Count; i++)
                    {
                        solution.AddChild(outerNode.Children[i]);
                    }
                }
                else
                {
                    solution.Clear();
                }
            }
        }

        private void OffsetPoint(int j, ref int k, JoinType jointype)
        {
            // Cross product
            _sinA = (_normals[k].X * _normals[j].Y - _normals[j].X * _normals[k].Y);

            if (Math.Abs(_sinA * _delta) < 1.0)
            {
                // Dot product
                var cosA = (_normals[k].X * _normals[j].X + _normals[j].Y * _normals[k].Y);

                if (cosA > 0) // angle ==> 0 degrees
                {
                    _destinationPolygon.Add(new IntPoint(
                        (_sourcePolygon[j].X + _normals[k].X * _delta).RoundToLong(),
                        (_sourcePolygon[j].Y + _normals[k].Y * _delta).RoundToLong()));
                    return;
                }
                //else angle ==> 180 degrees   
            }
            else if (_sinA > 1.0) _sinA = 1.0;
            else if (_sinA < -1.0) _sinA = -1.0;

            if (_sinA * _delta < 0)
            {
                _destinationPolygon.Add(new IntPoint(
                    (_sourcePolygon[j].X + _normals[k].X * _delta).RoundToLong(),
                    (_sourcePolygon[j].Y + _normals[k].Y * _delta).RoundToLong()));

                _destinationPolygon.Add(_sourcePolygon[j]);

                _destinationPolygon.Add(new IntPoint(
                    (_sourcePolygon[j].X + _normals[j].X * _delta).RoundToLong(),
                    (_sourcePolygon[j].Y + _normals[j].Y * _delta).RoundToLong()));
            }
            else
            {
                switch (jointype)
                {
                    case JoinType.Miter:
                        {
                            var r = 1 + (_normals[j].X * _normals[k].X +
                                            _normals[j].Y * _normals[k].Y);
                            if (r >= _miterLim) DoMiter(j, k, r); else DoSquare(j, k);
                            break;
                        }
                    case JoinType.Square: DoSquare(j, k); break;
                    case JoinType.Round: DoRound(j, k); break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(jointype), jointype, null);
                }
            }

            k = j;
        }

        internal void DoSquare(int j, int k)
        {
            var dx = Math.Tan(Math.Atan2(_sinA, _normals[k].X * _normals[j].X + _normals[k].Y * _normals[j].Y) / 4);

            _destinationPolygon.Add(new IntPoint(
                (_sourcePolygon[j].X + _delta * (_normals[k].X - _normals[k].Y * dx)).RoundToLong(),
                (_sourcePolygon[j].Y + _delta * (_normals[k].Y + _normals[k].X * dx)).RoundToLong()));

            _destinationPolygon.Add(new IntPoint(
                (_sourcePolygon[j].X + _delta * (_normals[j].X + _normals[j].Y * dx)).RoundToLong(),
                (_sourcePolygon[j].Y + _delta * (_normals[j].Y - _normals[j].X * dx)).RoundToLong()));
        }

        internal void DoMiter(int j, int k, double r)
        {
            var q = _delta / r;
            _destinationPolygon.Add(new IntPoint(
                (_sourcePolygon[j].X + (_normals[k].X + _normals[j].X) * q).RoundToLong(),
                (_sourcePolygon[j].Y + (_normals[k].Y + _normals[j].Y) * q).RoundToLong()));
        }

        internal void DoRound(int j, int k)
        {
            var a = Math.Atan2(_sinA, _normals[k].X * _normals[j].X + _normals[k].Y * _normals[j].Y);
            var steps = Math.Max((int)(_stepsPerRad * Math.Abs(a)).RoundToLong(), 1);

            var x = _normals[k].X;
            var y = _normals[k].Y;

            for (var i = 0; i < steps; ++i)
            {
                _destinationPolygon.Add(new IntPoint(
                    (_sourcePolygon[j].X + x * _delta).RoundToLong(),
                    (_sourcePolygon[j].Y + y * _delta).RoundToLong()));

                var x2 = x;
                x = x * _cos - _sin * y;
                y = x2 * _sin + y * _cos;
            }

            _destinationPolygon.Add(new IntPoint(
                (_sourcePolygon[j].X + _normals[j].X * _delta).RoundToLong(),
                (_sourcePolygon[j].Y + _normals[j].Y * _delta).RoundToLong()));
        }
    }
}