/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.4.1                                                           *
* Date      :  5 December 2016                                                 *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2015                                         *
*                                                                              * 
* Modified  : January 2017 - Mekatrol.                                         * 
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* Angus' version has been modified to align with a typical C# style rules.     *
* Unit and performance test added at the same time to allow cross checking     *
* between old (orginal) and new code.                                          *
*                                                                              *
*******************************************************************************/

//use_lines: Enables open path clipping. Adds a very minor cost to performance.
#define use_lines

using System;
using System.Collections.Generic;
using System.Linq;

namespace Clipper
{
    public class Clipper
    {
        private LocalMinima _minimaList;
        private LocalMinima _currentLocalMinima;
        private Scanbeam _scanbeam;
        private readonly List<OutputPolygon> _outputPolygons = new List<OutputPolygon>();
        private Edge _activeEdges;
        private bool _useFullRange;
        private bool _hasOpenPaths;

        private ClipOperation _clipOperation;
        private Maxima _maxima;
        private Edge _sortedEdges;
        private readonly IntersectionList _intersectList = new IntersectionList();
        private bool _executeLocked;
        private PolygonFillType _clipFillType;
        private PolygonFillType _subjFillType;
        private readonly List<Join> _joins = new List<Join>();
        private readonly List<Join> _ghostJoins = new List<Join>();
        private bool _usingTreeSolution;

        public bool PreserveCollinear { get; set; }

        public bool ReverseSolution { get; set; }

        public bool StrictlySimple { get; set; }

        public virtual void Clear()
        {
            _currentLocalMinima = null;
            _minimaList = null;
            _useFullRange = false;
            _hasOpenPaths = false;
        }

        private static Edge FindNextLocalMinima(Edge edge)
        {
            while (true)
            {
                // Skip intermediate edges.
                while (
                    // Bottom not a local minima?
                    edge.Bottom != edge.Prev.Bottom ||

                    // TODO: Have asked Angus which polygon structure would trigger this logic for a description?
                    edge.Current == edge.Top)
                {
                    edge = edge.Next;
                }

                // Non-horizontal edges minima found?
                if (!edge.IsHorizontal && !edge.Prev.IsHorizontal)
                {
                    // edge is left bound for local minima.
                    break;
                }

                // Rewind to edge prior to horizontal
                while (edge.Prev.IsHorizontal)
                {
                    edge = edge.Prev;
                }

                // Mark non-horizontal prev edge.
                var edge2 = edge;

                // Move forward again past horizontals.
                while (edge.IsHorizontal)
                {
                    edge = edge.Next;
                }

                // Was this just an intermediate horizontal?
                if (edge.Top.Y == edge.Prev.Bottom.Y)
                {
                    continue;
                }

                // Which was the left bound, edge or edge2?
                if (edge2.Prev.Bottom.X < edge.Bottom.X)
                {
                    // Edge2 was left bound start.
                    edge = edge2;
                }

                break;
            }

            return edge;
        }

        private Edge ProcessBound(Edge edge, bool leftBoundIsForward)
        {
            Edge start, result = edge;
            Edge horz;

            if (result.OutIndex == ClippingHelper.Skip)
            {
                // Check if there are edges beyond the skip edge in the bound and if so
                // create another LocMin and calling ProcessBound once more.
                edge = result;
                if (leftBoundIsForward)
                {
                    while (edge.Top.Y == edge.Next.Bottom.Y) edge = edge.Next;
                    while (edge != result && edge.IsHorizontal) edge = edge.Prev;
                }
                else
                {
                    while (edge.Top.Y == edge.Prev.Bottom.Y) edge = edge.Prev;
                    while (edge != result && edge.IsHorizontal) edge = edge.Next;
                }
                if (edge == result)
                {
                    result = leftBoundIsForward ? edge.Next : edge.Prev;
                }
                else
                {
                    // There are more edges in the bound beyond result starting with edge
                    edge = leftBoundIsForward ? result.Next : result.Prev;

                    var locMin = new LocalMinima
                    {
                        Next = null,
                        Y = edge.Bottom.Y,
                        LeftBound = null,
                        RightBound = edge
                    };

                    edge.WindDelta = 0;
                    result = ProcessBound(edge, leftBoundIsForward);
                    InsertLocalMinima(locMin);
                }
                return result;
            }

            if (edge.IsHorizontal)
            {
                // We need to be careful with open paths because this may not be a
                // true local minima (ie edge may be following a skip edge).
                // Also, consecutive horz. edges may start heading left before going right.
                start = leftBoundIsForward ? edge.Prev : edge.Next;

                if (start.IsHorizontal) // An adjoining horizontal skip edge.
                {
                    if (start.Bottom.X != edge.Bottom.X && start.Top.X != edge.Bottom.X)
                    {
                        edge.ReverseHorizontal();
                    }
                }
                else if (start.Bottom.X != edge.Bottom.X)
                {
                    edge.ReverseHorizontal();
                }
            }

            start = edge;
            if (leftBoundIsForward)
            {
                while (result.Top.Y == result.Next.Bottom.Y && result.Next.OutIndex != ClippingHelper.Skip)
                {
                    result = result.Next;
                }

                if (result.IsHorizontal && result.Next.OutIndex != ClippingHelper.Skip)
                {
                    // nb: at the top of a bound, horizontals are added to the bound
                    // only when the preceding edge attaches to the horizontal's left vertex
                    // unless a Skip edge is encountered when that becomes the top divide
                    horz = result;
                    while (horz.Prev.IsHorizontal) horz = horz.Prev;
                    if (horz.Prev.Top.X > result.Next.Top.X) result = horz.Prev;
                }

                while (edge != result)
                {
                    edge.NextInLml = edge.Next;
                    if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Prev.Top.X)
                    {
                        edge.ReverseHorizontal();
                    }
                    edge = edge.Next;
                }
                if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Prev.Top.X)
                {
                    edge.ReverseHorizontal();
                }

                result = result.Next; //move to the edge just beyond current bound
            }
            else
            {
                while (result.Top.Y == result.Prev.Bottom.Y && result.Prev.OutIndex != ClippingHelper.Skip)
                    result = result.Prev;
                if (result.IsHorizontal && result.Prev.OutIndex != ClippingHelper.Skip)
                {
                    horz = result;
                    while (horz.Next.IsHorizontal) horz = horz.Next;
                    if (horz.Next.Top.X == result.Prev.Top.X ||
                        horz.Next.Top.X > result.Prev.Top.X) result = horz.Next;
                }

                while (edge != result)
                {
                    edge.NextInLml = edge.Prev;
                    if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Next.Top.X)
                    {
                        edge.ReverseHorizontal();
                    }
                    edge = edge.Prev;
                }
                if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Next.Top.X)
                {
                    edge.ReverseHorizontal();
                }

                result = result.Prev; //move to the edge just beyond current bound
            }

            return result;
        }

        public bool AddPath(Polygon polygon, PolygonKind polygonKind)
        {
#if use_lines
            if (!polygon.IsClosed && polygonKind == PolygonKind.Clip)
                throw new Exception("AddPath: Open paths must be subject.");
#else
      if (!Closed)
        throw new ClipperException("AddPath: Open paths have been disabled.");
#endif

            // Step 1 - Remove duplicate vertices and collinear edges.
            var simplifiedPolygon = polygon.Simplified();

            // Closed polygons must have at least 3 vertices, and open polygons at least 2.
            var minVertexCount = simplifiedPolygon.IsClosed ? 3 : 2;
            if (simplifiedPolygon.Count < minVertexCount) return false;

            // Step 2 - Create a new edge array and perform basic initialization.
            var edges = Enumerable
                .Range(0, simplifiedPolygon.Count)
                .Select(i => new Edge
                {
                    Current = simplifiedPolygon[i],
                    OutIndex = ClippingHelper.Unassigned,
                    Kind = polygonKind
                })
                .ToArray();

            var lastIndex = simplifiedPolygon.Count - 1;

            // Step 3 - Initialize polygon boundary edges as well as range test vertex values.
            GeometryHelper.RangeTest(simplifiedPolygon[0], ref _useFullRange);
            GeometryHelper.RangeTest(simplifiedPolygon[lastIndex], ref _useFullRange);

            edges[0].SetBoundaryLinks(edges[1], edges[lastIndex]);
            edges[lastIndex].SetBoundaryLinks(edges[0], edges[lastIndex - 1]);

            for (var i = lastIndex - 1; i >= 1; --i)
            {
                GeometryHelper.RangeTest(simplifiedPolygon[i], ref _useFullRange);
                edges[i].SetBoundaryLinks(edges[i + 1], edges[i - 1]);
            }

            // Step 4 - Initialize open path settings.
            if (!simplifiedPolygon.IsClosed)
            {
                _hasOpenPaths = true;
                edges[0].Prev.OutIndex = ClippingHelper.Skip;
            }

            // Step 5 - Initialize boundary geometry.
            var edge = edges[0];
            var isFlat = true;
            do
            {
                edge.InitializeGeometry();
                edge = edge.Next;

                if (isFlat && edge.Current.Y != edges[0].Current.Y) isFlat = false;
            }
            while (edge != edges[0]);

            // Step 6 - Build LML
            return isFlat 
                ? BuildFlatLml(simplifiedPolygon, edges)
                : BuildLml(simplifiedPolygon, edges);
        }

        private bool BuildFlatLml(Polygon polygon, IReadOnlyList<Edge> edges)
        {
            var edge = edges[0];

            // Totally flat paths must be handled differently when adding them
            // to LocalMinima list to avoid endless loops etc.
            // Closed polygons cannot be flat.
            if (polygon.IsClosed)
            {
                return false;
            }

            edge.Prev.OutIndex = ClippingHelper.Skip;

            var localMinima = new LocalMinima
            {
                Y = edge.Bottom.Y,
                LeftBound = null,
                RightBound = edge
            };

            localMinima.RightBound.Side = EdgeSide.Right;
            localMinima.RightBound.WindDelta = 0;

            while (true)
            {
                if (edge.Bottom.X != edge.Prev.Top.X) { edge.ReverseHorizontal(); }
                if (edge.Next.OutIndex == ClippingHelper.Skip) { break; }
                edge.NextInLml = edge.Next;
                edge = edge.Next;
            }

            InsertLocalMinima(localMinima);

            return true;
        }

        private bool BuildLml(Polygon polygon, IReadOnlyList<Edge> edges)
        {
            Edge startLocalMinima = null;
            var edge = edges[0];

            while (true)
            {
                // Find next local minima.
                edge = FindNextLocalMinima(edge);

                // Back to begining?
                if (edge == startLocalMinima)
                {
                    // Done building local minima.
                    break;
                }

                if (startLocalMinima == null)
                {
                    // Record start local minima so that we know
                    // when we have iterated all minimas.
                    startLocalMinima = edge;
                }

                var localMinima = new LocalMinima
                {
                    Y = edge.Bottom.Y
                };

                // edge and edge.Prev now share a local minima (left aligned if horizontal).
                // Compare their slopes to find which starts which bound.
                bool leftBoundIsForward;
                if (edge.Dx < edge.Prev.Dx)
                {
                    localMinima.LeftBound = edge.Prev;
                    localMinima.RightBound = edge;
                    leftBoundIsForward = false;
                }
                else
                {
                    localMinima.LeftBound = edge;
                    localMinima.RightBound = edge.Prev;
                    leftBoundIsForward = true;
                }

                localMinima.LeftBound.Side = EdgeSide.Left;
                localMinima.RightBound.Side = EdgeSide.Right;

                // Initialize winding values
                if (!polygon.IsClosed)
                {
                    localMinima.LeftBound.WindDelta = 0;
                }
                else if (localMinima.LeftBound.Next == localMinima.RightBound)
                {
                    localMinima.LeftBound.WindDelta = -1;
                }
                else
                {
                    localMinima.LeftBound.WindDelta = 1;
                }
                localMinima.RightBound.WindDelta = -localMinima.LeftBound.WindDelta;

                edge = ProcessBound(localMinima.LeftBound, leftBoundIsForward);
                if (edge.OutIndex == ClippingHelper.Skip)
                {
                    edge = ProcessBound(edge, leftBoundIsForward);
                }

                var e2 = ProcessBound(localMinima.RightBound, !leftBoundIsForward);
                if (e2.OutIndex == ClippingHelper.Skip)
                {
                    e2 = ProcessBound(e2, !leftBoundIsForward);
                }

                if (localMinima.LeftBound.OutIndex == ClippingHelper.Skip)
                {
                    localMinima.LeftBound = null;
                }
                else if (localMinima.RightBound.OutIndex == ClippingHelper.Skip)
                {
                    localMinima.RightBound = null;
                }

                InsertLocalMinima(localMinima);

                if (!leftBoundIsForward)
                {
                    edge = e2;
                }
            }

            return true;
        }

        public bool AddPaths(PolygonPath path, PolygonKind polygonKind)
        {
            var result = false;
            foreach (var polygon in path)
            {
                if (AddPath(polygon, polygonKind))
                {
                    result = true;
                }
            }
            return result;
        }

        private void InsertLocalMinima(LocalMinima localMinima)
        {
            if (_minimaList == null)
            {
                _minimaList = localMinima;
            }
            else if (localMinima.Y >= _minimaList.Y)
            {
                localMinima.Next = _minimaList;
                _minimaList = localMinima;
            }
            else
            {
                var insertAfter = _minimaList;
                while (insertAfter.Next != null && localMinima.Y < insertAfter.Next.Y)
                {
                    insertAfter = insertAfter.Next;
                }

                localMinima.Next = insertAfter.Next;
                insertAfter.Next = localMinima;
            }
        }

        internal LocalMinima PopLocalMinima(long y)
        {
            var localMinima = _currentLocalMinima;

            // Any local minima matching Y value?
            if (_currentLocalMinima == null || _currentLocalMinima.Y != y)
            {
                // None available
                return null;
            }

            // Move to next (pop current out of list).
            _currentLocalMinima = _currentLocalMinima.Next;

            return localMinima;
        }

        internal virtual void Reset()
        {
            _currentLocalMinima = _minimaList;
            if (_currentLocalMinima == null) return; //ie nothing to process

            // Reset all edges.
            _scanbeam = null;

            var localMinima = _minimaList;
            while (localMinima != null)
            {
                InsertScanbeam(localMinima.Y);

                var edge = localMinima.LeftBound;
                if (edge != null)
                {
                    edge.Current = edge.Bottom;
                    edge.OutIndex = ClippingHelper.Unassigned;
                }

                edge = localMinima.RightBound;

                if (edge != null)
                {
                    edge.Current = edge.Bottom;
                    edge.OutIndex = ClippingHelper.Unassigned;
                }

                localMinima = localMinima.Next;
            }
            _activeEdges = null;
        }

        public static IntRect GetBounds(PolygonPath paths)
        {
            var i = 0;
            var count = paths.Count;

            while (i < count && paths[i].Count == 0)
            {
                i++;
            }

            if (i == count)
            {
                return new IntRect(0, 0, 0, 0);
            }

            var result = new IntRect
            {
                Left = paths[i][0].X,
                Top = paths[i][0].Y
            };
            result.Right = result.Left;
            result.Bottom = result.Top;

            for (; i < count; i++)
            {
                for (var j = 0; j < paths[i].Count; j++)
                {
                    if (paths[i][j].X < result.Left)
                    {
                        result.Left = paths[i][j].X;
                    }
                    else if (paths[i][j].X > result.Right)
                    {
                        result.Right = paths[i][j].X;
                    }

                    if (paths[i][j].Y < result.Top)
                    {
                        result.Top = paths[i][j].Y;
                    }
                    else if (paths[i][j].Y > result.Bottom)
                    {
                        result.Bottom = paths[i][j].Y;
                    }
                }
            }

            return result;
        }

        internal void InsertScanbeam(long y)
        {
            // single-linked list: sorted descending, ignoring dups.
            if (_scanbeam == null)
            {
                _scanbeam = new Scanbeam
                {
                    Y = y
                };
            }
            else if (y > _scanbeam.Y)
            {
                var scanbeam = new Scanbeam
                {
                    Y = y,
                    Next = _scanbeam
                };

                _scanbeam = scanbeam;
            }
            else
            {
                var scanbeam = _scanbeam;
                while (scanbeam.Next != null && y <= scanbeam.Next.Y)
                {
                    scanbeam = scanbeam.Next;
                }
                if (y == scanbeam.Y)
                {
                    // Ignore duplicates
                    return;
                }

                // Append the scanebeam
                scanbeam.Next = new Scanbeam
                {
                    Y = y,
                    Next = scanbeam.Next
                };
            }
        }

        internal bool PopScanbeam(out long y)
        {
            if (_scanbeam == null)
            {
                y = 0;
                return false;
            }

            y = _scanbeam.Y;
            _scanbeam = _scanbeam.Next;

            return true;
        }

        internal OutputPolygon CreateOutputPolygon()
        {
            var outputPolygon = new OutputPolygon
            {
                Index = _outputPolygons.Count
            };

            _outputPolygons.Add(outputPolygon);
            return outputPolygon;
        }

        internal void UpdateEdgeIntoAel(ref Edge edge)
        {
            if (edge.NextInLml == null)
            {
                throw new Exception("UpdateEdgeIntoAEL: invalid call");
            }

            var aelPrev = edge.PrevInAel;
            var aelNext = edge.NextInAel;
            edge.NextInLml.OutIndex = edge.OutIndex;

            if (aelPrev != null)
            {
                aelPrev.NextInAel = edge.NextInLml;
            }
            else
            {
                _activeEdges = edge.NextInLml;
            }

            if (aelNext != null)
            {
                aelNext.PrevInAel = edge.NextInLml;
            }

            edge.NextInLml.Side = edge.Side;
            edge.NextInLml.WindDelta = edge.WindDelta;
            edge.NextInLml.WindCount = edge.WindCount;
            edge.NextInLml.WindCount2 = edge.WindCount2;
            edge = edge.NextInLml;
            edge.Current = edge.Bottom;
            edge.PrevInAel = aelPrev;
            edge.NextInAel = aelNext;

            if (!edge.IsHorizontal)
            {
                InsertScanbeam(edge.Top.Y);
            }
        }

        internal void SwapPositionsInAel(Edge edge1, Edge edge2)
        {
            //check that one or other edge hasn't already been removed from AEL ...
            if (edge1.NextInAel == edge1.PrevInAel ||
              edge2.NextInAel == edge2.PrevInAel) return;

            if (edge1.NextInAel == edge2)
            {
                var next = edge2.NextInAel;

                if (next != null)
                {
                    next.PrevInAel = edge1;
                }

                var prev = edge1.PrevInAel;
                if (prev != null)
                {
                    prev.NextInAel = edge2;
                }

                edge2.PrevInAel = prev;
                edge2.NextInAel = edge1;
                edge1.PrevInAel = edge2;
                edge1.NextInAel = next;
            }
            else if (edge2.NextInAel == edge1)
            {
                var next = edge1.NextInAel;
                if (next != null)
                {
                    next.PrevInAel = edge2;
                }

                var prev = edge2.PrevInAel;
                if (prev != null)
                {
                    prev.NextInAel = edge1;
                }
                edge1.PrevInAel = prev;
                edge1.NextInAel = edge2;
                edge2.PrevInAel = edge1;
                edge2.NextInAel = next;
            }
            else
            {
                var next = edge1.NextInAel;
                var prev = edge1.PrevInAel;
                edge1.NextInAel = edge2.NextInAel;

                if (edge1.NextInAel != null)
                {
                    edge1.NextInAel.PrevInAel = edge1;
                }

                edge1.PrevInAel = edge2.PrevInAel;
                if (edge1.PrevInAel != null)
                {
                    edge1.PrevInAel.NextInAel = edge1;
                }

                edge2.NextInAel = next;

                if (edge2.NextInAel != null)
                {
                    edge2.NextInAel.PrevInAel = edge2;
                }

                edge2.PrevInAel = prev;

                if (edge2.PrevInAel != null)
                {
                    edge2.PrevInAel.NextInAel = edge2;
                }
            }

            if (edge1.PrevInAel == null)
            {
                _activeEdges = edge1;
            }
            else if (edge2.PrevInAel == null)
            {
                _activeEdges = edge2;
            }
        }

        internal void DeleteFromAel(Edge edge)
        {
            var aelPrev = edge.PrevInAel;
            var aelNext = edge.NextInAel;

            if (aelPrev == null && aelNext == null && edge != _activeEdges)
            {
                // Not in AEL
                return;
            }

            if (aelPrev != null)
            {
                aelPrev.NextInAel = aelNext;
            }
            else
            {
                _activeEdges = aelNext;
            }

            if (aelNext != null)
            {
                aelNext.PrevInAel = aelPrev;
            }

            edge.NextInAel = null;
            edge.PrevInAel = null;
        }

        private void InsertMaxima(long x)
        {
            var maxima = new Maxima { X = x };

            if (_maxima == null)
            {
                _maxima = maxima;
                _maxima.Next = null;
                _maxima.Prev = null;
            }
            else if (x < _maxima.X)
            {
                maxima.Next = _maxima;
                maxima.Prev = null;
                _maxima = maxima;
            }
            else
            {
                var m = _maxima;
                while (m.Next != null && x >= m.Next.X)
                {
                    m = m.Next;
                }

                if (x == m.X)
                {
                    // Ignore duplicates.
                    return;
                }

                // Insert maxima between m and m.Next
                maxima.Next = m.Next;
                maxima.Prev = m;
                if (m.Next != null) m.Next.Prev = maxima;
                m.Next = maxima;
            }
        }

        public bool Execute(ClipOperation clipOperation, PolygonPath solution,
            PolygonFillType fillType = PolygonFillType.EvenOdd)
        {
            return Execute(clipOperation, solution, fillType, fillType);
        }

        public bool Execute(ClipOperation clipOperation, PolygonTree polytree,
            PolygonFillType fillType = PolygonFillType.EvenOdd)
        {
            return Execute(clipOperation, polytree, fillType, fillType);
        }

        public bool Execute(ClipOperation clipOperation, PolygonPath solution,
            PolygonFillType subjFillType, PolygonFillType clipFillType)
        {
            if (_executeLocked) return false;

            if (_hasOpenPaths)
            {
                throw new Exception("Error: PolygonTree struct is needed for open path clipping.");
            }

            _executeLocked = true;
            solution.Clear();
            _subjFillType = subjFillType;
            _clipFillType = clipFillType;
            _clipOperation = clipOperation;
            _usingTreeSolution = false;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded)
                {
                    BuildResult(solution);
                }
            }
            finally
            {
                _outputPolygons.Clear();
                _executeLocked = false;
            }
            return succeeded;
        }

        public bool Execute(ClipOperation clipOperation, PolygonTree polytree,
            PolygonFillType subjFillType, PolygonFillType clipFillType)
        {
            if (_executeLocked) return false;
            _executeLocked = true;
            _subjFillType = subjFillType;
            _clipFillType = clipFillType;
            _clipOperation = clipOperation;
            _usingTreeSolution = true;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded) BuildResult(polytree);
            }
            finally
            {
                _outputPolygons.Clear();
                _executeLocked = false;
            }
            return succeeded;
        }

        internal void FixHoleLinkage(OutputPolygon outputPolygon)
        {
            //skip if an outermost polygon or
            //already already points to the correct FirstLeft ...
            if (outputPolygon.FirstLeft == null ||
                  (outputPolygon.IsHole != outputPolygon.FirstLeft.IsHole &&
                  outputPolygon.FirstLeft.Points != null)) return;

            var firstLeft = outputPolygon.FirstLeft;
            while (firstLeft != null && (firstLeft.IsHole == outputPolygon.IsHole || firstLeft.Points == null))
            {
                firstLeft = firstLeft.FirstLeft;
            }

            outputPolygon.FirstLeft = firstLeft;
        }

        private bool ExecuteInternal()
        {
            Reset();

            _sortedEdges = null;
            _maxima = null;

            long botY;
            if (!PopScanbeam(out botY))
            {
                return false;
            }

            InsertLocalMinimaIntoAel(botY);

            long topY;
            while (PopScanbeam(out topY) || _currentLocalMinima != null)
            {
                ProcessHorizontals();
                _ghostJoins.Clear();

                if (!ProcessIntersections(topY))
                {
                    return false;
                }

                ProcessEdgesAtTopOfScanbeam(topY);
                botY = topY;
                InsertLocalMinimaIntoAel(botY);
            }

            // Fix orientations
            foreach (var outputPolygon in _outputPolygons)
            {
                if (outputPolygon.Points == null || outputPolygon.IsOpen) continue;
                if ((outputPolygon.IsHole ^ ReverseSolution) ==
                    (outputPolygon.Orientation == PolygonOrientation.CounterClockwise))
                {
                    ReverseLinks(outputPolygon.Points);
                }
            }

            JoinCommonEdges();

            foreach (var outputPolygon in _outputPolygons)
            {
                if (outputPolygon.Points == null)
                {
                    continue;
                }

                if (outputPolygon.IsOpen)
                {
                    FixupOutPolyline(outputPolygon);
                }
                else
                {
                    FixupOutPolygon(outputPolygon);
                }
            }

            if (StrictlySimple)
            {
                DoSimplePolygons();
            }

            return true;
        }

        private void AddJoin(OutputPoint point1, OutputPoint point2, IntPoint offset)
        {
            var j = new Join
            {
                OutPoint1 = point1,
                OutPoint2 = point2,
                Offset = offset
            };
            _joins.Add(j);
        }

        private void AddGhostJoin(OutputPoint point, IntPoint offset)
        {
            var j = new Join
            {
                OutPoint1 = point,
                Offset = offset
            };
            _ghostJoins.Add(j);
        }

        private void InsertLocalMinimaIntoAel(long botY)
        {
            LocalMinima localMinima;
            while ((localMinima = PopLocalMinima(botY)) != null)
            {
                var leftBound = localMinima.LeftBound;
                var rightBound = localMinima.RightBound;

                OutputPoint outputPoint1 = null;
                if (leftBound == null)
                {
                    InsertEdgeIntoAel(rightBound, null);
                    SetWindingCount(rightBound);

                    if (IsContributing(rightBound))
                    {
                        outputPoint1 = AddOutputPoint(rightBound, rightBound.Bottom);
                    }
                }
                else if (rightBound == null)
                {
                    InsertEdgeIntoAel(leftBound, null);
                    SetWindingCount(leftBound);

                    if (IsContributing(leftBound))
                    {
                        outputPoint1 = AddOutputPoint(leftBound, leftBound.Bottom);
                    }

                    InsertScanbeam(leftBound.Top.Y);
                }
                else
                {
                    InsertEdgeIntoAel(leftBound, null);
                    InsertEdgeIntoAel(rightBound, leftBound);
                    SetWindingCount(leftBound);
                    rightBound.WindCount = leftBound.WindCount;
                    rightBound.WindCount2 = leftBound.WindCount2;

                    if (IsContributing(leftBound))
                    {
                        outputPoint1 = AddLocalMinPoly(leftBound, rightBound, leftBound.Bottom);
                    }

                    InsertScanbeam(leftBound.Top.Y);
                }

                if (rightBound != null)
                {
                    if (rightBound.IsHorizontal)
                    {
                        if (rightBound.NextInLml != null)
                        {
                            InsertScanbeam(rightBound.NextInLml.Top.Y);
                        }
                        AddEdgeToSel(rightBound);
                    }
                    else
                    {
                        InsertScanbeam(rightBound.Top.Y);
                    }
                }

                if (leftBound == null || rightBound == null) continue;

                //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
                if (outputPoint1 != null && rightBound.IsHorizontal &&
                  _ghostJoins.Count > 0 && rightBound.WindDelta != 0)
                {
                    foreach (var j in _ghostJoins)
                    {
                        if (HorzSegmentsOverlap(j.OutPoint1.Point.X, j.Offset.X, rightBound.Bottom.X, rightBound.Top.X))
                        {
                            AddJoin(j.OutPoint1, outputPoint1, j.Offset);
                        }
                    }
                }

                if (leftBound.OutIndex >= 0 && leftBound.PrevInAel != null &&
                  leftBound.PrevInAel.Current.X == leftBound.Bottom.X &&
                  leftBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(leftBound.PrevInAel.Current, leftBound.PrevInAel.Top, leftBound.Current, leftBound.Top, _useFullRange) &&
                  leftBound.WindDelta != 0 && leftBound.PrevInAel.WindDelta != 0)
                {
                    var outputPoint = AddOutputPoint(leftBound.PrevInAel, leftBound.Bottom);
                    AddJoin(outputPoint1, outputPoint, leftBound.Top);
                }

                if (leftBound.NextInAel == rightBound)
                {
                    continue;
                }

                if (rightBound.OutIndex >= 0 && rightBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(rightBound.PrevInAel.Current, rightBound.PrevInAel.Top, rightBound.Current, rightBound.Top, _useFullRange) &&
                    rightBound.WindDelta != 0 && rightBound.PrevInAel.WindDelta != 0)
                {
                    var outputPoint = AddOutputPoint(rightBound.PrevInAel, rightBound.Bottom);
                    AddJoin(outputPoint1, outputPoint, rightBound.Top);
                }

                var edge = leftBound.NextInAel;

                if (edge == null) continue;

                while (edge != rightBound)
                {
                    // nb: For calculating winding counts etc, IntersectEdges() assumes
                    // that param1 will be to the right of param2 ABOVE the intersection ...
                    IntersectEdges(rightBound, edge, leftBound.Current); //order important here
                    edge = edge.NextInAel;
                }
            }
        }

        private void InsertEdgeIntoAel(Edge edge, Edge startEdge)
        {
            if (_activeEdges == null)
            {
                edge.PrevInAel = null;
                edge.NextInAel = null;
                _activeEdges = edge;
            }
            else if (startEdge == null && E2InsertsBeforeE1(_activeEdges, edge))
            {
                edge.PrevInAel = null;
                edge.NextInAel = _activeEdges;
                _activeEdges.PrevInAel = edge;
                _activeEdges = edge;
            }
            else
            {
                if (startEdge == null) startEdge = _activeEdges;
                while (startEdge.NextInAel != null &&
                       !E2InsertsBeforeE1(startEdge.NextInAel, edge))
                {
                    startEdge = startEdge.NextInAel;
                }

                edge.NextInAel = startEdge.NextInAel;

                if (startEdge.NextInAel != null)
                {
                    startEdge.NextInAel.PrevInAel = edge;
                }

                edge.PrevInAel = startEdge;
                startEdge.NextInAel = edge;
            }
        }

        private static bool E2InsertsBeforeE1(Edge e1, Edge e2)
        {
            return e2.Current.X == e1.Current.X
                ? (e2.Top.Y > e1.Top.Y
                    ? e2.Top.X < TopX(e1, e2.Top.Y)
                    : e1.Top.X > TopX(e2, e1.Top.Y))
                : e2.Current.X < e1.Current.X;
        }

        private bool IsEvenOddFillType(Edge edge)
        {
            return edge.Kind == PolygonKind.Subject
                ? _subjFillType == PolygonFillType.EvenOdd
                : _clipFillType == PolygonFillType.EvenOdd;
        }

        private bool IsEvenOddAltFillType(Edge edge)
        {
            return edge.Kind == PolygonKind.Subject
                ? _clipFillType == PolygonFillType.EvenOdd
                : _subjFillType == PolygonFillType.EvenOdd;
        }

        private bool IsContributing(Edge edge)
        {
            PolygonFillType pft, pft2;
            if (edge.Kind == PolygonKind.Subject)
            {
                pft = _subjFillType;
                pft2 = _clipFillType;
            }
            else
            {
                pft = _clipFillType;
                pft2 = _subjFillType;
            }

            switch (pft)
            {
                case PolygonFillType.EvenOdd:
                    // return false if a subj line has been flagged as inside a subj polygon
                    if (edge.WindDelta == 0 && edge.WindCount != 1) return false;
                    break;

                case PolygonFillType.NonZero:
                    if (Math.Abs(edge.WindCount) != 1) return false;
                    break;

                case PolygonFillType.Positive:
                    if (edge.WindCount != 1) return false;
                    break;

                case PolygonFillType.Negative:
                    break;

                default: //PolygonFillType.Negative
                    if (edge.WindCount != -1) return false;
                    break;
            }

            switch (_clipOperation)
            {
                case ClipOperation.Intersection:
                    {
                        switch (pft2)
                        {
                            case PolygonFillType.EvenOdd:
                            case PolygonFillType.NonZero:
                                return (edge.WindCount2 != 0);
                            case PolygonFillType.Positive:
                                return (edge.WindCount2 > 0);
                            default:
                                return (edge.WindCount2 < 0);
                        }
                    }

                case ClipOperation.Union:
                    switch (pft2)
                    {
                        case PolygonFillType.EvenOdd:
                        case PolygonFillType.NonZero:
                            return (edge.WindCount2 == 0);
                        case PolygonFillType.Positive:
                            return (edge.WindCount2 <= 0);
                        default:
                            return (edge.WindCount2 >= 0);
                    }

                case ClipOperation.Difference:
                    if (edge.Kind == PolygonKind.Subject)
                    {
                        switch (pft2)
                        {
                            case PolygonFillType.EvenOdd:
                            case PolygonFillType.NonZero:
                                return (edge.WindCount2 == 0);
                            case PolygonFillType.Positive:
                                return (edge.WindCount2 <= 0);
                            default:
                                return (edge.WindCount2 >= 0);
                        }
                    }
                    else
                        switch (pft2)
                        {
                            case PolygonFillType.EvenOdd:
                            case PolygonFillType.NonZero:
                                return (edge.WindCount2 != 0);
                            case PolygonFillType.Positive:
                                return (edge.WindCount2 > 0);
                            default:
                                return (edge.WindCount2 < 0);
                        }

                case ClipOperation.Xor:
                    if (edge.WindDelta == 0) //XOr always contributing unless open
                    {
                        switch (pft2)
                        {
                            case PolygonFillType.EvenOdd:
                            case PolygonFillType.NonZero:
                                return (edge.WindCount2 == 0);
                            case PolygonFillType.Positive:
                                return (edge.WindCount2 <= 0);
                            default:
                                return (edge.WindCount2 >= 0);
                        }
                    }

                    return true;
            }

            return true;
        }

        private void SetWindingCount(Edge edge)
        {
            var e = edge.PrevInAel;

            //find the edge of the same polytype that immediately preceeds 'edge' in AEL
            while (e != null && (e.Kind != edge.Kind || e.WindDelta == 0))
            {
                e = e.PrevInAel;
            }

            if (e == null)
            {
                var fillType = edge.Kind == PolygonKind.Subject ? _subjFillType : _clipFillType;

                if (edge.WindDelta == 0)
                {
                    edge.WindCount = (fillType == PolygonFillType.Negative ? -1 : 1);
                }
                else
                {
                    edge.WindCount = edge.WindDelta;
                }

                edge.WindCount2 = 0;
                e = _activeEdges; //ie get ready to calc WindCount2
            }
            else if (edge.WindDelta == 0 && _clipOperation != ClipOperation.Union)
            {
                edge.WindCount = 1;
                edge.WindCount2 = e.WindCount2;
                e = e.NextInAel; //ie get ready to calc WindCount2
            }
            else if (IsEvenOddFillType(edge))
            {
                //EvenOdd filling ...
                if (edge.WindDelta == 0)
                {
                    //are we inside a subj polygon ...
                    var inside = true;
                    var e2 = e.PrevInAel;

                    while (e2 != null)
                    {
                        if (e2.Kind == e.Kind && e2.WindDelta != 0)
                            inside = !inside;
                        e2 = e2.PrevInAel;
                    }

                    edge.WindCount = inside ? 0 : 1;
                }
                else
                {
                    edge.WindCount = edge.WindDelta;
                }
                edge.WindCount2 = e.WindCount2;
                e = e.NextInAel; //ie get ready to calc WindCount2
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                if (e.WindCount * e.WindDelta < 0)
                {
                    //prev edge is 'decreasing' WindCount (WC) toward zero
                    //so we're outside the previous polygon ...
                    if (Math.Abs(e.WindCount) > 1)
                    {
                        //outside prev poly but still inside another.
                        //when reversing direction of prev poly use the same WC 
                        if (e.WindDelta * edge.WindDelta < 0) edge.WindCount = e.WindCount;
                        //otherwise continue to 'decrease' WC ...
                        else edge.WindCount = e.WindCount + edge.WindDelta;
                    }
                    else
                        //now outside all polys of same polytype so set own WC ...
                        edge.WindCount = (edge.WindDelta == 0 ? 1 : edge.WindDelta);
                }
                else
                {
                    //prev edge is 'increasing' WindCount (WC) away from zero
                    //so we're inside the previous polygon ...
                    if (edge.WindDelta == 0)
                        edge.WindCount = (e.WindCount < 0 ? e.WindCount - 1 : e.WindCount + 1);
                    //if wind direction is reversing prev then use same WC
                    else if (e.WindDelta * edge.WindDelta < 0)
                        edge.WindCount = e.WindCount;
                    //otherwise add to WC ...
                    else edge.WindCount = e.WindCount + edge.WindDelta;
                }
                edge.WindCount2 = e.WindCount2;
                e = e.NextInAel; //ie get ready to calc WindCount2
            }

            //update WindCount2 ...
            if (IsEvenOddAltFillType(edge))
            {
                //EvenOdd filling ...
                while (e != edge)
                {
                    if (e.WindDelta != 0)
                        edge.WindCount2 = (edge.WindCount2 == 0 ? 1 : 0);
                    e = e.NextInAel;
                }
            }
            else
            {
                //nonZero, Positive or Negative filling ...
                while (e != edge)
                {
                    edge.WindCount2 += e.WindDelta;
                    e = e.NextInAel;
                }
            }
        }

        private void AddEdgeToSel(Edge edge)
        {
            //SEL pointers in PEdge are use to build transient lists of horizontal edges.
            //However, since we don't need to worry about processing order, all additions
            //are made to the front of the list ...
            if (_sortedEdges == null)
            {
                _sortedEdges = edge;
                edge.PrevInSel = null;
                edge.NextInSel = null;
            }
            else
            {
                edge.NextInSel = _sortedEdges;
                edge.PrevInSel = null;
                _sortedEdges.PrevInSel = edge;
                _sortedEdges = edge;
            }
        }

        internal bool PopEdgeFromSel(out Edge edge)
        {
            //Pop edge from front of SEL (ie SEL is a FILO list)
            edge = _sortedEdges;
            if (edge == null) return false;
            Edge oldE = edge;
            _sortedEdges = edge.NextInSel;
            if (_sortedEdges != null) _sortedEdges.PrevInSel = null;
            oldE.NextInSel = null;
            oldE.PrevInSel = null;
            return true;
        }

        private void CopyAeltoSel()
        {
            var e = _activeEdges;
            _sortedEdges = e;
            while (e != null)
            {
                e.PrevInSel = e.PrevInAel;
                e.NextInSel = e.NextInAel;
                e = e.NextInAel;
            }
        }

        private void SwapPositionsInSel(Edge edge1, Edge edge2)
        {
            if (edge1.NextInSel == null && edge1.PrevInSel == null)
            {
                return;
            }

            if (edge2.NextInSel == null && edge2.PrevInSel == null)
            {
                return;
            }

            if (edge1.NextInSel == edge2)
            {
                var next = edge2.NextInSel;
                if (next != null)
                {
                    next.PrevInSel = edge1;
                }

                var prev = edge1.PrevInSel;
                if (prev != null)
                {
                    prev.NextInSel = edge2;
                }

                edge2.PrevInSel = prev;
                edge2.NextInSel = edge1;
                edge1.PrevInSel = edge2;
                edge1.NextInSel = next;
            }
            else if (edge2.NextInSel == edge1)
            {
                var next = edge1.NextInSel;
                if (next != null)
                {
                    next.PrevInSel = edge2;
                }

                var prev = edge2.PrevInSel;
                if (prev != null)
                {
                    prev.NextInSel = edge1;
                }

                edge1.PrevInSel = prev;
                edge1.NextInSel = edge2;
                edge2.PrevInSel = edge1;
                edge2.NextInSel = next;
            }
            else
            {
                var next = edge1.NextInSel;
                var prev = edge1.PrevInSel;
                edge1.NextInSel = edge2.NextInSel;

                if (edge1.NextInSel != null)
                {
                    edge1.NextInSel.PrevInSel = edge1;
                }

                edge1.PrevInSel = edge2.PrevInSel;

                if (edge1.PrevInSel != null)
                {
                    edge1.PrevInSel.NextInSel = edge1;
                }

                edge2.NextInSel = next;

                if (edge2.NextInSel != null)
                {
                    edge2.NextInSel.PrevInSel = edge2;
                }

                edge2.PrevInSel = prev;

                if (edge2.PrevInSel != null)
                {
                    edge2.PrevInSel.NextInSel = edge2;
                }
            }

            if (edge1.PrevInSel == null)
            {
                _sortedEdges = edge1;
            }
            else if (edge2.PrevInSel == null)
            {
                _sortedEdges = edge2;
            }
        }

        private void AddLocalMaxPoly(Edge edge1, Edge edge2, IntPoint point)
        {
            AddOutputPoint(edge1, point);

            if (edge2.WindDelta == 0)
            {
                AddOutputPoint(edge2, point);
            }

            if (edge1.OutIndex == edge2.OutIndex)
            {
                edge1.OutIndex = ClippingHelper.Unassigned;
                edge2.OutIndex = ClippingHelper.Unassigned;
            }
            else if (edge1.OutIndex < edge2.OutIndex)
            {
                AppendPolygon(edge1, edge2);
            }
            else
            {
                AppendPolygon(edge2, edge1);
            }
        }

        private OutputPoint AddLocalMinPoly(Edge edge1, Edge edge2, IntPoint point)
        {
            OutputPoint result;
            Edge edge, prev;

            if (edge2.IsHorizontal || edge1.Dx > edge2.Dx)
            {
                result = AddOutputPoint(edge1, point);
                edge2.OutIndex = edge1.OutIndex;
                edge1.Side = EdgeSide.Left;
                edge2.Side = EdgeSide.Right;
                edge = edge1;
                prev = edge.PrevInAel == edge2 ? edge2.PrevInAel : edge.PrevInAel;
            }
            else
            {
                result = AddOutputPoint(edge2, point);
                edge1.OutIndex = edge2.OutIndex;
                edge1.Side = EdgeSide.Right;
                edge2.Side = EdgeSide.Left;
                edge = edge2;
                prev = edge.PrevInAel == edge1 ? edge1.PrevInAel : edge.PrevInAel;
            }

            if (prev == null || prev.OutIndex < 0) return result;

            var xPrev = TopX(prev, point.Y);
            var xEdge = TopX(edge, point.Y);

            if (xPrev != xEdge ||
                edge.WindDelta == 0 ||
                prev.WindDelta == 0 ||
                !GeometryHelper.SlopesEqual(
                    new IntPoint(xPrev, point.Y), prev.Top, new IntPoint(xEdge, point.Y), edge.Top, _useFullRange))
            {
                return result;
            }

            var outputPoint = AddOutputPoint(prev, point);

            AddJoin(result, outputPoint, edge.Top);

            return result;
        }

        private OutputPoint AddOutputPoint(Edge edge, IntPoint point)
        {
            if (edge.OutIndex < 0)
            {
                var outputPolygon = CreateOutputPolygon();
                outputPolygon.IsOpen = edge.WindDelta == 0;

                var points = new OutputPoint
                {
                    Index = outputPolygon.Index,
                    Point = point
                };

                points.Next = points;
                points.Prev = points;

                outputPolygon.Points = points;

                if (!outputPolygon.IsOpen)
                {
                    SetHoleState(edge, outputPolygon);
                }
                edge.OutIndex = outputPolygon.Index; //nb: do this after SetZ !

                return points;
            }
            else
            {
                var outputPolygon = _outputPolygons[edge.OutIndex];
                //OutputPolygon.Points is the 'left-most' point & OutputPolygon.Points.Prev is the 'right-most'
                var points = outputPolygon.Points;

                var toFront = edge.Side == EdgeSide.Left;

                if (toFront && point == points.Point)
                {
                    return points;
                }

                if (!toFront && point == points.Prev.Point)
                {
                    return points.Prev;
                }

                var p = new OutputPoint
                {
                    Index = outputPolygon.Index,
                    Point = point,
                    Next = points,
                    Prev = points.Prev
                };

                p.Prev.Next = p;
                points.Prev = p;

                if (toFront)
                {
                    outputPolygon.Points = p;
                }

                return p;
            }
        }

        private OutputPoint GetLastOutPoint(Edge edge)
        {
            var outputPolygon = _outputPolygons[edge.OutIndex];
            return edge.Side == EdgeSide.Left ? outputPolygon.Points : outputPolygon.Points.Prev;
        }

        internal void SwapPoints(ref IntPoint point1, ref IntPoint point2)
        {
            var tmp = new IntPoint(point1);
            point1 = point2;
            point2 = tmp;
        }

        private static bool HorzSegmentsOverlap(long seg1A, long seg1B, long seg2A, long seg2B)
        {
            if (seg1A > seg1B) GeometryHelper.Swap(ref seg1A, ref seg1B);
            if (seg2A > seg2B) GeometryHelper.Swap(ref seg2A, ref seg2B);
            return seg1A < seg2B && seg2A < seg1B;
        }

        private void SetHoleState(Edge edge, OutputPolygon outputPolygon)
        {
            var edge2 = edge.PrevInAel;
            Edge tmp = null;

            while (edge2 != null)
            {
                if (edge2.OutIndex >= 0 && edge2.WindDelta != 0)
                {
                    if (tmp == null)
                    {
                        tmp = edge2;
                    }
                    else if (tmp.OutIndex == edge2.OutIndex)
                    {
                        tmp = null; //paired               
                    }
                }
                edge2 = edge2.PrevInAel;
            }

            if (tmp == null)
            {
                outputPolygon.FirstLeft = null;
                outputPolygon.IsHole = false;
            }
            else
            {
                outputPolygon.FirstLeft = _outputPolygons[tmp.OutIndex];
                outputPolygon.IsHole = !outputPolygon.FirstLeft.IsHole;
            }
        }

        private static bool FirstIsBottomPoint(OutputPoint point1, OutputPoint point2)
        {
            var p = point1.Prev;

            while (p.Point == point1.Point && p != point1)
            {
                p = p.Prev;
            }

            var dx1P = Math.Abs(GeometryHelper.GetDx(point1.Point, p.Point));

            p = point1.Next;

            while (p.Point == point1.Point && p != point1)
            {
                p = p.Next;
            }

            var dx1N = Math.Abs(GeometryHelper.GetDx(point1.Point, p.Point));

            p = point2.Prev;
            while (p.Point == point2.Point && p != point2)
            {
                p = p.Prev;
            }

            var dx2P = Math.Abs(GeometryHelper.GetDx(point2.Point, p.Point));

            p = point2.Next;

            while (p.Point == point2.Point && p != point2)
            {
                p = p.Next;
            }

            var dx2N = Math.Abs(GeometryHelper.GetDx(point2.Point, p.Point));

            return GeometryHelper.NearZero(Math.Max(dx1P, dx1N) - Math.Max(dx2P, dx2N)) &&
                   GeometryHelper.NearZero(Math.Min(dx1P, dx1N) - Math.Min(dx2P, dx2N))
                ? point1.Area > 0
                : dx1P >= dx2P && dx1P >= dx2N || dx1N >= dx2P && dx1N >= dx2N;
        }

        private static OutputPoint GetBottomPoint(OutputPoint polygon)
        {
            OutputPoint duplicate = null;
            var p = polygon.Next;

            while (p != polygon)
            {
                if (p.Point.Y > polygon.Point.Y)
                {
                    polygon = p;
                    duplicate = null;
                }
                else if (p.Point.Y == polygon.Point.Y && p.Point.X <= polygon.Point.X)
                {
                    if (p.Point.X < polygon.Point.X)
                    {
                        polygon = p;
                        duplicate = null;
                    }
                    else
                    {
                        if (p.Next != polygon && p.Prev != polygon)
                        {
                            duplicate = p;
                        }
                    }
                }
                p = p.Next;
            }

            if (duplicate == null) return polygon;

            // there appears to be at least 2 vertices at bottom point.
            while (duplicate != p)
            {
                if (!FirstIsBottomPoint(p, duplicate))
                {
                    polygon = duplicate;
                }

                duplicate = duplicate.Next;

                while (duplicate.Point != polygon.Point)
                {
                    duplicate = duplicate.Next;
                }
            }

            return polygon;
        }

        private static OutputPolygon GetLowermostRec(OutputPolygon outPolygon1, OutputPolygon outPolygon2)
        {
            // Work out which polygon fragment has the correct hole state.
            if (outPolygon1.BottomPoint == null)
            {
                outPolygon1.BottomPoint = GetBottomPoint(outPolygon1.Points);
            }

            if (outPolygon2.BottomPoint == null)
            {
                outPolygon2.BottomPoint = GetBottomPoint(outPolygon2.Points);
            }

            var point1 = outPolygon1.BottomPoint;
            var point2 = outPolygon2.BottomPoint;

            if (point1.Point.Y > point2.Point.Y) return outPolygon1;
            if (point1.Point.Y < point2.Point.Y) return outPolygon2;
            if (point1.Point.X < point2.Point.X) return outPolygon1;
            if (point1.Point.X > point2.Point.X) return outPolygon2;
            if (point1.Next == point1) return outPolygon2;
            if (point2.Next == point2) return outPolygon1;

            return FirstIsBottomPoint(point1, point2) ? outPolygon1 : outPolygon2;
        }

        private static bool OutPolygon1RightOfOutPolygon2(OutputPolygon outPolygon1, OutputPolygon outPolygon2)
        {
            do
            {
                outPolygon1 = outPolygon1.FirstLeft;

                if (outPolygon1 == outPolygon2)
                {
                    return true;
                }
            } while (outPolygon1 != null);

            return false;
        }

        private OutputPolygon GetOutPolygon(int index)
        {
            var outPolygon = _outputPolygons[index];

            while (outPolygon != _outputPolygons[outPolygon.Index])
            {
                outPolygon = _outputPolygons[outPolygon.Index];
            }

            return outPolygon;
        }

        private void AppendPolygon(Edge edge1, Edge edge2)
        {
            var polygon1 = _outputPolygons[edge1.OutIndex];
            var polygon2 = _outputPolygons[edge2.OutIndex];

            OutputPolygon hole;

            if (OutPolygon1RightOfOutPolygon2(polygon1, polygon2))
            {
                hole = polygon2;
            }
            else if (OutPolygon1RightOfOutPolygon2(polygon2, polygon1))
            {
                hole = polygon1;
            }
            else
            {
                hole = GetLowermostRec(polygon1, polygon2);
            }

            // get the start and ends of both output polygons and
            // join E2 poly onto E1 poly and delete pointers to E2 ...
            var polygon1Left = polygon1.Points;
            var polygon1Right = polygon1Left.Prev;
            var polygon2Left = polygon2.Points;
            var polygon2Right = polygon2Left.Prev;

            //join edge2 poly onto edge1 poly and delete pointers to edge2 ...
            if (edge1.Side == EdgeSide.Left)
            {
                if (edge2.Side == EdgeSide.Left)
                {
                    // z y x a b c
                    ReverseLinks(polygon2Left);
                    polygon2Left.Next = polygon1Left;
                    polygon1Left.Prev = polygon2Left;
                    polygon1Right.Next = polygon2Right;
                    polygon2Right.Prev = polygon1Right;
                    polygon1.Points = polygon2Right;
                }
                else
                {
                    // x y z a b c
                    polygon2Right.Next = polygon1Left;
                    polygon1Left.Prev = polygon2Right;
                    polygon2Left.Prev = polygon1Right;
                    polygon1Right.Next = polygon2Left;
                    polygon1.Points = polygon2Left;
                }
            }
            else
            {
                if (edge2.Side == EdgeSide.Right)
                {
                    // a b c z y x
                    ReverseLinks(polygon2Left);
                    polygon1Right.Next = polygon2Right;
                    polygon2Right.Prev = polygon1Right;
                    polygon2Left.Next = polygon1Left;
                    polygon1Left.Prev = polygon2Left;
                }
                else
                {
                    // a b c x y z
                    polygon1Right.Next = polygon2Left;
                    polygon2Left.Prev = polygon1Right;
                    polygon1Left.Prev = polygon2Right;
                    polygon2Right.Next = polygon1Left;
                }
            }

            polygon1.BottomPoint = null;
            if (hole == polygon2)
            {
                if (polygon2.FirstLeft != polygon1)
                {
                    polygon1.FirstLeft = polygon2.FirstLeft;
                }
                polygon1.IsHole = polygon2.IsHole;
            }
            polygon2.Points = null;
            polygon2.BottomPoint = null;

            polygon2.FirstLeft = polygon1;

            var okIndex = edge1.OutIndex;
            var obsoleteIndex = edge2.OutIndex;

            edge1.OutIndex = ClippingHelper.Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
            edge2.OutIndex = ClippingHelper.Unassigned;

            var edge = _activeEdges;
            while (edge != null)
            {
                if (edge.OutIndex == obsoleteIndex)
                {
                    edge.OutIndex = okIndex;
                    edge.Side = edge1.Side;
                    break;
                }
                edge = edge.NextInAel;
            }
            polygon2.Index = polygon1.Index;
        }

        private static void ReverseLinks(OutputPoint point)
        {
            if (point == null) return;

            var polygon1 = point;

            do
            {
                var polygon2 = polygon1.Next;
                polygon1.Next = polygon1.Prev;
                polygon1.Prev = polygon2;
                polygon1 = polygon2;
            } while (polygon1 != point);
        }

        private static void SwapSides(Edge edge1, Edge edge2)
        {
            var side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }

        private static void SwapPolyIndexes(Edge edge1, Edge edge2)
        {
            var outIdx = edge1.OutIndex;
            edge1.OutIndex = edge2.OutIndex;
            edge2.OutIndex = outIdx;
        }

        private void IntersectEdges(Edge edge1, Edge edge2, IntPoint pt)
        {
            // edge1 will be to the left of edge2 BELOW the intersection. Therefore edge1 is before
            // edge2 in AEL except when edge1 is being inserted at the intersection point ...

            var e1Contributing = edge1.OutIndex >= 0;
            var e2Contributing = edge2.OutIndex >= 0;

#if use_lines
            // if either edge is on an OPEN path ...
            if (edge1.WindDelta == 0 || edge2.WindDelta == 0)
            {
                // ignore subject-subject open path intersections UNLESS they
                // are both open paths, AND they are both 'contributing maximas' ...
                if (edge1.WindDelta == 0 && edge2.WindDelta == 0) return;

                // if intersecting a subj line with a subj poly ...
                if (edge1.Kind == edge2.Kind &&
                    edge1.WindDelta != edge2.WindDelta &&
                    _clipOperation == ClipOperation.Union)
                {
                    if (edge1.WindDelta == 0)
                    {
                        if (!e2Contributing) return;

                        AddOutputPoint(edge1, pt);
                        if (e1Contributing)
                        {
                            edge1.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                    else
                    {
                        if (!e1Contributing) return;

                        AddOutputPoint(edge2, pt);
                        if (e2Contributing)
                        {
                            edge2.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                }
                else if (edge1.Kind != edge2.Kind)
                {
                    if (edge1.WindDelta == 0 &&
                        Math.Abs(edge2.WindCount) == 1 &&
                        (_clipOperation != ClipOperation.Union || edge2.WindCount2 == 0))
                    {
                        AddOutputPoint(edge1, pt);
                        if (e1Contributing)
                        {
                            edge1.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                    else if (edge2.WindDelta == 0 &&
                             Math.Abs(edge1.WindCount) == 1 &&
                             (_clipOperation != ClipOperation.Union || edge1.WindCount2 == 0))
                    {
                        AddOutputPoint(edge2, pt);
                        if (e2Contributing)
                        {
                            edge2.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                }

                return;
            }
#endif

            // update winding counts...
            // assumes that edge1 will be to the right of edge2 ABOVE the intersection
            if (edge1.Kind == edge2.Kind)
            {
                if (IsEvenOddFillType(edge1))
                {
                    var tmp = edge1.WindCount;
                    edge1.WindCount = edge2.WindCount;
                    edge2.WindCount = tmp;
                }
                else
                {
                    if (edge1.WindCount + edge2.WindDelta == 0)
                    {
                        edge1.WindCount = -edge1.WindCount;
                    }
                    else
                    {
                        edge1.WindCount += edge2.WindDelta;
                    }

                    if (edge2.WindCount - edge1.WindDelta == 0)
                    {
                        edge2.WindCount = -edge2.WindCount;
                    }
                    else
                    {
                        edge2.WindCount -= edge1.WindDelta;
                    }
                }
            }
            else
            {
                if (!IsEvenOddFillType(edge2))
                {
                    edge1.WindCount2 += edge2.WindDelta;
                }
                else
                {
                    edge1.WindCount2 = edge1.WindCount2 == 0 ? 1 : 0;
                }

                if (!IsEvenOddFillType(edge1))
                {
                    edge2.WindCount2 -= edge1.WindDelta;
                }
                else
                {
                    edge2.WindCount2 = edge2.WindCount2 == 0 ? 1 : 0;
                }
            }

            PolygonFillType e1FillType, e2FillType, e1FillType2, e2FillType2;
            if (edge1.Kind == PolygonKind.Subject)
            {
                e1FillType = _subjFillType;
                e1FillType2 = _clipFillType;
            }
            else
            {
                e1FillType = _clipFillType;
                e1FillType2 = _subjFillType;
            }
            if (edge2.Kind == PolygonKind.Subject)
            {
                e2FillType = _subjFillType;
                e2FillType2 = _clipFillType;
            }
            else
            {
                e2FillType = _clipFillType;
                e2FillType2 = _subjFillType;
            }

            int e1Wc, e2Wc;

            switch (e1FillType)
            {
                case PolygonFillType.Positive: e1Wc = edge1.WindCount; break;
                case PolygonFillType.Negative: e1Wc = -edge1.WindCount; break;
                default: e1Wc = Math.Abs(edge1.WindCount); break;
            }

            switch (e2FillType)
            {
                case PolygonFillType.Positive: e2Wc = edge2.WindCount; break;
                case PolygonFillType.Negative: e2Wc = -edge2.WindCount; break;
                default: e2Wc = Math.Abs(edge2.WindCount); break;
            }

            if (e1Contributing && e2Contributing)
            {
                if (e1Wc != 0 &&
                    e1Wc != 1 ||
                    e2Wc != 0 && e2Wc != 1 ||
                    edge1.Kind != edge2.Kind &&
                    _clipOperation != ClipOperation.Xor)
                {
                    AddLocalMaxPoly(edge1, edge2, pt);
                }
                else
                {
                    AddOutputPoint(edge1, pt);
                    AddOutputPoint(edge2, pt);
                    SwapSides(edge1, edge2);
                    SwapPolyIndexes(edge1, edge2);
                }
            }
            else if (e1Contributing)
            {
                if (e2Wc != 0 && e2Wc != 1) return;

                AddOutputPoint(edge1, pt);
                SwapSides(edge1, edge2);
                SwapPolyIndexes(edge1, edge2);
            }
            else if (e2Contributing)
            {
                if (e1Wc != 0 && e1Wc != 1) return;

                AddOutputPoint(edge2, pt);
                SwapSides(edge1, edge2);
                SwapPolyIndexes(edge1, edge2);
            }
            else if ((e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
            {
                // Neither edge is currently contributing.
                long e1Wc2, e2Wc2;

                switch (e1FillType2)
                {
                    case PolygonFillType.Positive: e1Wc2 = edge1.WindCount2; break;
                    case PolygonFillType.Negative: e1Wc2 = -edge1.WindCount2; break;
                    default: e1Wc2 = Math.Abs(edge1.WindCount2); break;
                }

                switch (e2FillType2)
                {
                    case PolygonFillType.Positive: e2Wc2 = edge2.WindCount2; break;
                    case PolygonFillType.Negative: e2Wc2 = -edge2.WindCount2; break;
                    default: e2Wc2 = Math.Abs(edge2.WindCount2); break;
                }

                if (edge1.Kind != edge2.Kind)
                {
                    AddLocalMinPoly(edge1, edge2, pt);
                }
                else if (e1Wc == 1 && e2Wc == 1)
                {
                    switch (_clipOperation)
                    {
                        case ClipOperation.Intersection:
                            if (e1Wc2 > 0 && e2Wc2 > 0)
                                AddLocalMinPoly(edge1, edge2, pt);
                            break;
                        case ClipOperation.Union:
                            if (e1Wc2 <= 0 && e2Wc2 <= 0)
                                AddLocalMinPoly(edge1, edge2, pt);
                            break;
                        case ClipOperation.Difference:
                            if (((edge1.Kind == PolygonKind.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((edge1.Kind == PolygonKind.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                                AddLocalMinPoly(edge1, edge2, pt);
                            break;
                        case ClipOperation.Xor:
                            AddLocalMinPoly(edge1, edge2, pt);
                            break;
                    }
                }
                else
                {
                    SwapSides(edge1, edge2);
                }
            }
        }

        private void ProcessHorizontals()
        {
            Edge horzEdge;
            while (PopEdgeFromSel(out horzEdge))
            {
                ProcessHorizontal(horzEdge);
            }
        }

        private static void GetHorzDirection(Edge horzEdge, out EdgeDirection direction, out long left, out long right)
        {
            if (horzEdge.Bottom.X < horzEdge.Top.X)
            {
                left = horzEdge.Bottom.X;
                right = horzEdge.Top.X;
                direction = EdgeDirection.LeftToRight;
            }
            else
            {
                left = horzEdge.Top.X;
                right = horzEdge.Bottom.X;
                direction = EdgeDirection.RightToLeft;
            }
        }

        private void ProcessHorizontal(Edge horzEdge)
        {
            EdgeDirection edgeDirection;
            long horzLeft, horzRight;
            var isOpen = horzEdge.WindDelta == 0;

            GetHorzDirection(horzEdge, out edgeDirection, out horzLeft, out horzRight);

            var lastHorz = horzEdge;
            Edge maxPair = null;

            while (lastHorz.NextInLml != null && lastHorz.NextInLml.IsHorizontal)
            {
                lastHorz = lastHorz.NextInLml;
            }

            if (lastHorz.NextInLml == null)
            {
                maxPair = GetMaximaPair(lastHorz);
            }

            var currMax = _maxima;
            if (currMax != null)
            {
                //get the first maxima in range (X) ...
                if (edgeDirection == EdgeDirection.LeftToRight)
                {
                    while (currMax != null && currMax.X <= horzEdge.Bottom.X)
                    {
                        currMax = currMax.Next;
                    }

                    if (currMax != null && currMax.X >= lastHorz.Top.X)
                    {
                        currMax = null;
                    }
                }
                else
                {
                    while (currMax.Next != null && currMax.Next.X < horzEdge.Bottom.X)
                    {
                        currMax = currMax.Next;
                    }

                    if (currMax.X <= lastHorz.Top.X)
                    {
                        currMax = null;
                    }
                }
            }

            OutputPoint point = null;
            while (true) //loop through consec. horizontal edges
            {
                var isLastHorz = horzEdge == lastHorz;
                var edge = GetNextInAel(horzEdge, edgeDirection);

                while (edge != null)
                {

                    //this code block inserts extra coords into horizontal edges (in output
                    //polygons) whereever maxima touch these horizontal edges. This helps
                    //'simplifying' polygons (ie if the Simplify property is set).
                    if (currMax != null)
                    {
                        if (edgeDirection == EdgeDirection.LeftToRight)
                        {
                            while (currMax != null && currMax.X < edge.Current.X)
                            {
                                if (horzEdge.OutIndex >= 0 && !isOpen)
                                {
                                    AddOutputPoint(horzEdge, new IntPoint(currMax.X, horzEdge.Bottom.Y));
                                }
                                currMax = currMax.Next;
                            }
                        }
                        else
                        {
                            while (currMax != null && currMax.X > edge.Current.X)
                            {
                                if (horzEdge.OutIndex >= 0 && !isOpen)
                                {
                                    AddOutputPoint(horzEdge, new IntPoint(currMax.X, horzEdge.Bottom.Y));
                                }
                                currMax = currMax.Prev;
                            }
                        }
                    }

                    if (edgeDirection == EdgeDirection.LeftToRight &&
                        edge.Current.X > horzRight ||

                        edgeDirection == EdgeDirection.RightToLeft &&
                        edge.Current.X < horzLeft)
                    {
                        break;
                    }

                    // Also break if we've got to the end of an intermediate horizontal edge ...
                    // nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
                    if (edge.Current.X == horzEdge.Top.X &&
                        horzEdge.NextInLml != null &&
                        edge.Dx < horzEdge.NextInLml.Dx)
                    {
                        break;
                    }

                    if (horzEdge.OutIndex >= 0 && !isOpen)  //note: may be done multiple times
                    {
                        point = AddOutputPoint(horzEdge, edge.Current);

                        var nextHorz = _sortedEdges;

                        while (nextHorz != null)
                        {
                            if (nextHorz.OutIndex >= 0 &&
                              HorzSegmentsOverlap(horzEdge.Bottom.X,
                              horzEdge.Top.X, nextHorz.Bottom.X, nextHorz.Top.X))
                            {
                                var point2 = GetLastOutPoint(nextHorz);
                                AddJoin(point2, point, nextHorz.Top);
                            }
                            nextHorz = nextHorz.NextInSel;
                        }
                        AddGhostJoin(point, horzEdge.Bottom);
                    }

                    // OK, so far we're still in range of the horizontal Edge  but make sure
                    // we're at the last of consec. horizontals when matching with maxPair
                    if (edge == maxPair && isLastHorz)
                    {
                        if (horzEdge.OutIndex >= 0)
                        {
                            AddLocalMaxPoly(horzEdge, maxPair, horzEdge.Top);
                        }

                        DeleteFromAel(horzEdge);
                        DeleteFromAel(maxPair);

                        return;
                    }

                    if (edgeDirection == EdgeDirection.LeftToRight)
                    {
                        IntersectEdges(horzEdge, edge, new IntPoint(edge.Current.X, horzEdge.Current.Y));
                    }
                    else
                    {
                        IntersectEdges(edge, horzEdge, new IntPoint(edge.Current.X, horzEdge.Current.Y));
                    }

                    var nextInAel = GetNextInAel(edge, edgeDirection);
                    SwapPositionsInAel(horzEdge, edge);
                    edge = nextInAel;
                }

                // Break out of loop if HorzEdge.NextInLML is not also horizontal.
                if (horzEdge.NextInLml == null || !horzEdge.NextInLml.IsHorizontal)
                {
                    break;
                }

                UpdateEdgeIntoAel(ref horzEdge);

                if (horzEdge.OutIndex >= 0)
                {
                    AddOutputPoint(horzEdge, horzEdge.Bottom);
                }

                GetHorzDirection(horzEdge, out edgeDirection, out horzLeft, out horzRight);
            }

            if (horzEdge.OutIndex >= 0 && point == null)
            {
                point = GetLastOutPoint(horzEdge);

                var nextHorz = _sortedEdges;

                while (nextHorz != null)
                {
                    if (nextHorz.OutIndex >= 0 &&
                      HorzSegmentsOverlap(horzEdge.Bottom.X,
                      horzEdge.Top.X, nextHorz.Bottom.X, nextHorz.Top.X))
                    {
                        AddJoin(GetLastOutPoint(nextHorz), point, nextHorz.Top);
                    }
                    nextHorz = nextHorz.NextInSel;
                }
                AddGhostJoin(point, horzEdge.Top);
            }

            if (horzEdge.NextInLml != null)
            {
                if (horzEdge.OutIndex >= 0)
                {
                    point = AddOutputPoint(horzEdge, horzEdge.Top);

                    UpdateEdgeIntoAel(ref horzEdge);
                    if (horzEdge.WindDelta == 0)
                    {
                        return;
                    }

                    // nb: HorzEdge is no longer horizontal here
                    var prevInAel = horzEdge.PrevInAel;
                    var nextInAel = horzEdge.NextInAel;

                    if (prevInAel != null &&
                        prevInAel.Current.X == horzEdge.Bottom.X &&
                        prevInAel.Current.Y == horzEdge.Bottom.Y &&
                        prevInAel.WindDelta != 0 &&
                        prevInAel.OutIndex >= 0 &&
                        prevInAel.Current.Y > prevInAel.Top.Y &&
                        GeometryHelper.SlopesEqual(horzEdge, prevInAel, _useFullRange))
                    {
                        AddJoin(point, AddOutputPoint(prevInAel, horzEdge.Bottom), horzEdge.Top);
                    }
                    else if (nextInAel != null &&
                             nextInAel.Current.X == horzEdge.Bottom.X &&
                             nextInAel.Current.Y == horzEdge.Bottom.Y &&
                             nextInAel.WindDelta != 0 &&
                             nextInAel.OutIndex >= 0 &&
                             nextInAel.Current.Y > nextInAel.Top.Y &&
                             GeometryHelper.SlopesEqual(horzEdge, nextInAel, _useFullRange))
                    {
                        AddJoin(point, AddOutputPoint(nextInAel, horzEdge.Bottom), horzEdge.Top);
                    }
                }
                else
                {
                    UpdateEdgeIntoAel(ref horzEdge);
                }
            }
            else
            {
                if (horzEdge.OutIndex >= 0)
                {
                    AddOutputPoint(horzEdge, horzEdge.Top);
                }
                DeleteFromAel(horzEdge);
            }
        }

        private static Edge GetNextInAel(Edge edge, EdgeDirection edgeDirection)
        {
            return edgeDirection == EdgeDirection.LeftToRight ? edge.NextInAel : edge.PrevInAel;
        }

        internal bool IsMinima(Edge edge)
        {
            return edge != null &&
                   edge.Prev.NextInLml != edge &&
                   edge.Next.NextInLml != edge;
        }

        internal static bool IsMaxima(Edge edge, long y)
        {
            return edge != null && edge.Top.Y == y && edge.NextInLml == null;
        }

        public static bool IsIntermediate(Edge edge, long y)
        {
            return edge.Top.Y == y && edge.NextInLml != null;
        }

        internal Edge GetMaximaPair(Edge edge)
        {
            if (edge.Next.Top == edge.Top && edge.Next.NextInLml == null)
            {
                return edge.Next;
            }

            if (edge.Prev.Top == edge.Top && edge.Prev.NextInLml == null)
            {
                return edge.Prev;
            }

            return null;
        }

        internal Edge GetMaximaPairEx(Edge edge)
        {
            //as above but returns null if MaxPair isn't in AEL (unless it's horizontal)
            var pair = GetMaximaPair(edge);
            if (pair == null ||
                pair.OutIndex == ClippingHelper.Skip ||
                pair.NextInAel == pair.PrevInAel &&
                !pair.IsHorizontal)
            {
                return null;
            }

            return pair;
        }

        private bool ProcessIntersections(long topY)
        {
            if (_activeEdges == null)
            {
                return true;
            }

            try
            {
                BuildIntersectList(topY);
                if (_intersectList.Count == 0) return true;
                if (_intersectList.Count == 1 || FixupIntersectionOrder())
                {
                    ProcessIntersectList();
                }
                else
                {
                    return false;
                }

                _sortedEdges = null;
                return true;
            }
            catch
            {
                _sortedEdges = null;
                _intersectList.Clear();
                throw new Exception("ProcessIntersections error");
            }
        }

        private void BuildIntersectList(long topY)
        {
            if (_activeEdges == null)
            {
                return;
            }

            // prepare for sorting ...
            var edge = _activeEdges;
            _sortedEdges = edge;

            while (edge != null)
            {
                edge.PrevInSel = edge.PrevInAel;
                edge.NextInSel = edge.NextInAel;
                edge.Current.X = TopX(edge, topY);
                edge = edge.NextInAel;
            }

            // bubblesort ...
            var isModified = true;

            while (isModified && _sortedEdges != null)
            {
                isModified = false;
                edge = _sortedEdges;
                while (edge.NextInSel != null)
                {
                    var nextInSel = edge.NextInSel;
                    if (edge.Current.X > nextInSel.Current.X)
                    {
                        IntPoint point;
                        IntersectPoint(edge, nextInSel, out point);

                        if (point.Y < topY)
                        {
                            point = new IntPoint(TopX(edge, topY), topY);
                        }

                        var node = new IntersectNode
                        {
                            Edge1 = edge,
                            Edge2 = nextInSel,
                            Point = point
                        };

                        _intersectList.Add(node);

                        SwapPositionsInSel(edge, nextInSel);
                        isModified = true;
                    }
                    else
                    {
                        edge = nextInSel;
                    }
                }
                if (edge.PrevInSel != null)
                {
                    edge.PrevInSel.NextInSel = null;
                }
                else
                {
                    break;
                }
            }
            _sortedEdges = null;
        }

        private static bool EdgesAdjacent(IntersectNode node)
        {
            return node.Edge1.NextInSel == node.Edge2 || node.Edge1.PrevInSel == node.Edge2;
        }

        private bool FixupIntersectionOrder()
        {
            // pre-condition: intersections are sorted bottom-most first.
            // Now it's crucial that intersections are made only between adjacent edges,
            // so to ensure this the order of intersections may need adjusting ...
            _intersectList.Sort();

            CopyAeltoSel();
            for (var i = 0; i < _intersectList.Count; i++)
            {
                if (!EdgesAdjacent(_intersectList[i]))
                {
                    var j = i + 1;

                    while (j < _intersectList.Count && !EdgesAdjacent(_intersectList[j]))
                    {
                        j++;
                    }

                    if (j == _intersectList.Count)
                    {
                        return false;
                    }

                    var tmp = _intersectList[i];
                    _intersectList[i] = _intersectList[j];
                    _intersectList[j] = tmp;

                }

                SwapPositionsInSel(_intersectList[i].Edge1, _intersectList[i].Edge2);
            }

            return true;
        }

        private void ProcessIntersectList()
        {
            foreach (var node in _intersectList)
            {
                IntersectEdges(node.Edge1, node.Edge2, node.Point);
                SwapPositionsInAel(node.Edge1, node.Edge2);
            }
            _intersectList.Clear();
        }

        private static long TopX(Edge edge, long currentY)
        {
            return currentY == edge.Top.Y
                ? edge.Top.X
                : edge.Bottom.X + (edge.Dx * (currentY - edge.Bottom.Y)).RoundToLong();
        }

        private static void IntersectPoint(Edge edge1, Edge edge2, out IntPoint point)
        {
            point = new IntPoint();

            // nb: with very large coordinate values, it's possible for SlopesEqual() to 
            // return false but for the edge.Dx value be equal due to double precision rounding.
            if (GeometryHelper.NearZero(edge1.Dx - edge2.Dx))
            {
                point.Y = edge1.Current.Y;
                point.X = TopX(edge1, point.Y);
                return;
            }

            double b1;
            double b2;

            if (edge1.Delta.X == 0)
            {
                point.X = edge1.Bottom.X;
                if (edge2.IsHorizontal)
                {
                    point.Y = edge2.Bottom.Y;
                }
                else
                {
                    b2 = edge2.Bottom.Y - edge2.Bottom.X / edge2.Dx;
                    point.Y = (point.X / edge2.Dx + b2).RoundToLong();
                }
            }
            else if (edge2.Delta.X == 0)
            {
                point.X = edge2.Bottom.X;
                if (edge1.IsHorizontal)
                {
                    point.Y = edge1.Bottom.Y;
                }
                else
                {
                    b1 = edge1.Bottom.Y - (edge1.Bottom.X / edge1.Dx);
                    point.Y = (point.X / edge1.Dx + b1).RoundToLong();
                }
            }
            else
            {
                b1 = edge1.Bottom.X - edge1.Bottom.Y * edge1.Dx;
                b2 = edge2.Bottom.X - edge2.Bottom.Y * edge2.Dx;
                var q = (b2 - b1) / (edge1.Dx - edge2.Dx);
                point.Y = q.RoundToLong();
                point.X = Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx)
                    ? (edge1.Dx * q + b1).RoundToLong()
                    : (edge2.Dx * q + b2).RoundToLong();
            }

            if (point.Y < edge1.Top.Y || point.Y < edge2.Top.Y)
            {
                point.Y = edge1.Top.Y > edge2.Top.Y ? edge1.Top.Y : edge2.Top.Y;
                point.X = TopX(Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx) ? edge1 : edge2, point.Y);
            }

            //finally, don't allow 'point' to be BELOW curr.Y (ie bottom of scanbeam) ...
            if (point.Y <= edge1.Current.Y) return;

            point.Y = edge1.Current.Y;

            //better to use the more vertical edge to derive X ...
            point.X = TopX(Math.Abs(edge1.Dx) > Math.Abs(edge2.Dx) ? edge2 : edge1, point.Y);
        }

        private void ProcessEdgesAtTopOfScanbeam(long topY)
        {
            var edge = _activeEdges;
            while (edge != null)
            {
                // 1. process maxima, treating them as if they're 'bent' horizontal edges,
                //    but exclude maxima with horizontal edges. nb: edge can't be a horizontal.
                var isMaximaEdge = IsMaxima(edge, topY);

                if (isMaximaEdge)
                {
                    var maxPair = GetMaximaPairEx(edge);
                    isMaximaEdge = maxPair == null || !maxPair.IsHorizontal;
                }

                if (isMaximaEdge)
                {
                    if (StrictlySimple)
                    {
                        InsertMaxima(edge.Top.X);
                    }

                    var prevInAel = edge.PrevInAel;
                    DoMaxima(edge);
                    edge = prevInAel == null ? _activeEdges : prevInAel.NextInAel;
                }
                else
                {
                    // 2. promote horizontal edges, otherwise update Current.X and Current.Y ...
                    if (IsIntermediate(edge, topY) && edge.NextInLml.IsHorizontal)
                    {
                        UpdateEdgeIntoAel(ref edge);
                        if (edge.OutIndex >= 0)
                        {
                            AddOutputPoint(edge, edge.Bottom);
                        }
                        AddEdgeToSel(edge);
                    }
                    else
                    {
                        edge.Current.X = TopX(edge, topY);
                        edge.Current.Y = topY;
                    }

                    // When StrictlySimple and 'edge' is being touched by another edge, then
                    // make sure both edges have a vertex here ...
                    if (StrictlySimple)
                    {
                        var prevInAel = edge.PrevInAel;
                        if (edge.OutIndex >= 0 &&
                            edge.WindDelta != 0 &&
                            prevInAel != null &&
                            prevInAel.OutIndex >= 0 &&
                            prevInAel.Current.X == edge.Current.X &&
                            prevInAel.WindDelta != 0)
                        {
                            var point = new IntPoint(edge.Current);
                            var outputPoint1 = AddOutputPoint(prevInAel, point);
                            var outputPoint2 = AddOutputPoint(edge, point);
                            AddJoin(outputPoint1, outputPoint2, point); // StrictlySimple (type-3) join
                        }
                    }

                    edge = edge.NextInAel;
                }
            }

            // 3. Process horizontals at the Top of the scanbeam ...
            ProcessHorizontals();
            _maxima = null;

            // 4. Promote intermediate vertices ...
            edge = _activeEdges;
            while (edge != null)
            {
                if (IsIntermediate(edge, topY))
                {
                    OutputPoint op = null;
                    if (edge.OutIndex >= 0)
                    {
                        op = AddOutputPoint(edge, edge.Top);
                    }

                    UpdateEdgeIntoAel(ref edge);

                    // if output polygons share an edge, they'll need joining later ...
                    var prevInAel = edge.PrevInAel;
                    var nextInAel = edge.NextInAel;

                    if (prevInAel != null &&
                        prevInAel.Current.X == edge.Bottom.X &&
                        prevInAel.Current.Y == edge.Bottom.Y &&
                        op != null &&
                        prevInAel.OutIndex >= 0 &&
                        prevInAel.Current.Y > prevInAel.Top.Y &&
                        GeometryHelper.SlopesEqual(edge.Current, edge.Top, prevInAel.Current, prevInAel.Top, _useFullRange) &&
                        edge.WindDelta != 0 && (prevInAel.WindDelta != 0))
                    {
                        AddJoin(op, AddOutputPoint(prevInAel, edge.Bottom), edge.Top);
                    }
                    else if (nextInAel != null &&
                             nextInAel.Current.X == edge.Bottom.X &&
                             nextInAel.Current.Y == edge.Bottom.Y && op != null &&
                             nextInAel.OutIndex >= 0 &&
                             nextInAel.Current.Y > nextInAel.Top.Y &&
                             GeometryHelper.SlopesEqual(edge.Current, edge.Top, nextInAel.Current, nextInAel.Top, _useFullRange) &&
                             edge.WindDelta != 0 &&
                             nextInAel.WindDelta != 0)
                    {
                        AddJoin(op, AddOutputPoint(nextInAel, edge.Bottom), edge.Top);
                    }
                }

                edge = edge.NextInAel;
            }
        }

        private void DoMaxima(Edge edge)
        {
            var maxPair = GetMaximaPairEx(edge);

            if (maxPair == null)
            {
                if (edge.OutIndex >= 0)
                {
                    AddOutputPoint(edge, edge.Top);
                }

                DeleteFromAel(edge);
                return;
            }

            var nextInAel = edge.NextInAel;
            while (nextInAel != null && nextInAel != maxPair)
            {
                IntersectEdges(edge, nextInAel, edge.Top);
                SwapPositionsInAel(edge, nextInAel);
                nextInAel = edge.NextInAel;
            }

            if (edge.OutIndex == ClippingHelper.Unassigned && maxPair.OutIndex == ClippingHelper.Unassigned)
            {
                DeleteFromAel(edge);
                DeleteFromAel(maxPair);
            }
            else if (edge.OutIndex >= 0 && maxPair.OutIndex >= 0)
            {
                if (edge.OutIndex >= 0) AddLocalMaxPoly(edge, maxPair, edge.Top);
                DeleteFromAel(edge);
                DeleteFromAel(maxPair);
            }
#if use_lines
            else if (edge.WindDelta == 0)
            {
                if (edge.OutIndex >= 0)
                {
                    AddOutputPoint(edge, edge.Top);
                    edge.OutIndex = ClippingHelper.Unassigned;
                }
                DeleteFromAel(edge);

                if (maxPair.OutIndex >= 0)
                {
                    AddOutputPoint(maxPair, edge.Top);
                    maxPair.OutIndex = ClippingHelper.Unassigned;
                }
                DeleteFromAel(maxPair);
            }
#endif
            else
            {
                throw new Exception("DoMaxima error");
            }
        }

        public static void ReversePaths(PolygonPath path)
        {
            foreach (var polygon in path) { polygon.Reverse(); }
        }

        private static int PointCount(OutputPoint polygon)
        {
            if (polygon == null) return 0;
            var result = 0;
            var p = polygon;
            do
            {
                result++;
                p = p.Next;
            }
            while (p != polygon);
            return result;
        }

        private void BuildResult(PolygonPath path)
        {
            path.Clear();
            path.Capacity = _outputPolygons.Count;
            foreach (var outputPolygon in _outputPolygons)
            {
                if (outputPolygon.Points == null) continue;
                var p = outputPolygon.Points.Prev;
                var pointCount = PointCount(p);
                if (pointCount < 2) continue;

                var polygon = new Polygon(pointCount);

                for (var j = 0; j < pointCount; j++)
                {
                    polygon.Add(p.Point);
                    p = p.Prev;
                }
                path.Add(polygon);
            }
        }

        private void BuildResult(PolygonTree tree)
        {
            tree.Clear();

            //add each output polygon/contour to tree ...
            tree.AllPolygons.Capacity = _outputPolygons.Count;
            foreach (var outputPolygon in _outputPolygons)
            {
                var pointCount = PointCount(outputPolygon.Points);
                if (outputPolygon.IsOpen && pointCount < 2 ||
                    !outputPolygon.IsOpen && pointCount < 3)
                {
                    continue;
                }

                FixHoleLinkage(outputPolygon);

                var node = new PolygonNode();

                tree.AllPolygons.Add(node);
                outputPolygon.PolygonNode = node;
                node.Polygon.Capacity = pointCount;

                var polygonPart = outputPolygon.Points.Prev;
                for (var j = 0; j < pointCount; j++)
                {
                    node.Polygon.Add(polygonPart.Point);
                    polygonPart = polygonPart.Prev;
                }
            }

            // fixup PolygonNode links etc ...
            tree.Children.Capacity = _outputPolygons.Count;
            foreach (var outputPolygon in _outputPolygons)
            {
                if (outputPolygon.PolygonNode == null) continue;

                if (outputPolygon.IsOpen)
                {
                    outputPolygon.PolygonNode.IsOpen = true;
                    tree.AddChild(outputPolygon.PolygonNode);
                }
                else if (outputPolygon.FirstLeft?.PolygonNode != null)
                {
                    outputPolygon.FirstLeft.PolygonNode.AddChild(outputPolygon.PolygonNode);
                }
                else
                {
                    tree.AddChild(outputPolygon.PolygonNode);
                }
            }
        }

        private static void FixupOutPolyline(OutputPolygon outPolygon)
        {
            var polygon = outPolygon.Points;
            var prevPolygon = polygon.Prev;

            while (polygon != prevPolygon)
            {
                polygon = polygon.Next;
                if (polygon.Point != polygon.Prev.Point)
                {
                    continue;
                }

                if (polygon == prevPolygon) prevPolygon = polygon.Prev;

                var tmp = polygon.Prev;

                tmp.Next = polygon.Next;
                polygon.Next.Prev = tmp;
                polygon = tmp;
            }

            if (polygon == polygon.Prev)
            {
                outPolygon.Points = null;
            }
        }

        private void FixupOutPolygon(OutputPolygon outputPolygon)
        {
            // FixupOutPolygon() - removes duplicate points and simplifies consecutive
            // parallel edges by removing the middle vertex.
            OutputPoint lastOk = null;
            outputPolygon.BottomPoint = null;
            var polygonPart = outputPolygon.Points;
            var preserveCol = PreserveCollinear || StrictlySimple;

            while (true)
            {
                if (polygonPart.Prev == polygonPart || polygonPart.Prev == polygonPart.Next)
                {
                    outputPolygon.Points = null;
                    return;
                }
                //test for duplicate points and collinear edges ...
                if (polygonPart.Point == polygonPart.Next.Point ||
                    polygonPart.Point == polygonPart.Prev.Point ||
                    GeometryHelper.SlopesEqual(polygonPart.Prev.Point, polygonPart.Point, polygonPart.Next.Point, _useFullRange) &&
                    (!preserveCol || !GeometryHelper.Pt2IsBetweenPt1AndPt3(polygonPart.Prev.Point, polygonPart.Point, polygonPart.Next.Point)))
                {
                    lastOk = null;
                    polygonPart.Prev.Next = polygonPart.Next;
                    polygonPart.Next.Prev = polygonPart.Prev;
                    polygonPart = polygonPart.Prev;
                }
                else if (polygonPart == lastOk)
                {
                    break;
                }
                else
                {
                    if (lastOk == null)
                    {
                        lastOk = polygonPart;
                    }
                    polygonPart = polygonPart.Next;
                }
            }
            outputPolygon.Points = polygonPart;
        }

        private static OutputPoint DuplicateOutputPoint(OutputPoint outputPoint, bool insertAfter)
        {
            var result = new OutputPoint
            {
                Point = outputPoint.Point,
                Index = outputPoint.Index
            };

            if (insertAfter)
            {
                result.Next = outputPoint.Next;
                result.Prev = outputPoint;
                outputPoint.Next.Prev = result;
                outputPoint.Next = result;
            }
            else
            {
                result.Prev = outputPoint.Prev;
                result.Next = outputPoint;
                outputPoint.Prev.Next = result;
                outputPoint.Prev = result;
            }
            return result;
        }

        private static bool GetOverlap(long a1, long a2, long b1, long b2, out long left, out long right)
        {
            if (a1 < a2)
            {
                if (b1 < b2)
                {
                    left = Math.Max(a1, b1);
                    right = Math.Min(a2, b2);
                }
                else
                {
                    left = Math.Max(a1, b2);
                    right = Math.Min(a2, b1);
                }
            }
            else
            {
                if (b1 < b2)
                {
                    left = Math.Max(a2, b1);
                    right = Math.Min(a1, b2);
                }
                else
                {
                    left = Math.Max(a2, b2);
                    right = Math.Min(a1, b1);
                }
            }
            return left < right;
        }

        private static bool JoinHorz(
            OutputPoint op1, OutputPoint op1B,
            OutputPoint op2, OutputPoint op2B,
            IntPoint point, bool discardLeft)
        {
            var dir1 = op1.Point.X > op1B.Point.X
                ? EdgeDirection.RightToLeft
                : EdgeDirection.LeftToRight;

            var dir2 = op2.Point.X > op2B.Point.X
                ? EdgeDirection.RightToLeft
                : EdgeDirection.LeftToRight;

            if (dir1 == dir2)
            {
                return false;
            }

            // When DiscardLeft, we want Op1b to be on the left of point1, otherwise we
            // want Op1b to be on the right. (And likewise with point2 and Op2b.)
            // So, to facilitate this while inserting Op1b and Op2b ...
            // when DiscardLeft, make sure we're AT or RIGHT of Point before adding Op1b,
            // otherwise make sure we're AT or LEFT of Point. (Likewise with Op2b.)
            if (dir1 == EdgeDirection.LeftToRight)
            {
                while (op1.Next.Point.X <= point.X &&
                       op1.Next.Point.X >= op1.Point.X &&
                       op1.Next.Point.Y == point.Y)
                {
                    op1 = op1.Next;
                }

                if (discardLeft && op1.Point.X != point.X)
                {
                    op1 = op1.Next;
                }

                op1B = DuplicateOutputPoint(op1, !discardLeft);

                if (op1B.Point != point)
                {
                    op1 = op1B;
                    op1.Point = point;
                    op1B = DuplicateOutputPoint(op1, !discardLeft);
                }
            }
            else
            {
                while (op1.Next.Point.X >= point.X &&
                       op1.Next.Point.X <= op1.Point.X &&
                       op1.Next.Point.Y == point.Y)
                {
                    op1 = op1.Next;
                }

                if (!discardLeft && op1.Point.X != point.X)
                {
                    op1 = op1.Next;
                }

                op1B = DuplicateOutputPoint(op1, discardLeft);

                if (op1B.Point != point)
                {
                    op1 = op1B;
                    op1.Point = point;
                    op1B = DuplicateOutputPoint(op1, discardLeft);
                }
            }

            if (dir2 == EdgeDirection.LeftToRight)
            {
                while (op2.Next.Point.X <= point.X &&
                       op2.Next.Point.X >= op2.Point.X &&
                       op2.Next.Point.Y == point.Y)
                {
                    op2 = op2.Next;
                }

                if (discardLeft && op2.Point.X != point.X)
                {
                    op2 = op2.Next;
                }

                op2B = DuplicateOutputPoint(op2, !discardLeft);

                if (op2B.Point != point)
                {
                    op2 = op2B;
                    op2.Point = point;
                    op2B = DuplicateOutputPoint(op2, !discardLeft);
                }
            }
            else
            {
                while (op2.Next.Point.X >= point.X &&
                       op2.Next.Point.X <= op2.Point.X &&
                       op2.Next.Point.Y == point.Y)
                {
                    op2 = op2.Next;
                }

                if (!discardLeft && op2.Point.X != point.X)
                {
                    op2 = op2.Next;
                }

                op2B = DuplicateOutputPoint(op2, discardLeft);
                if (op2B.Point != point)
                {
                    op2 = op2B;
                    op2.Point = point;
                    op2B = DuplicateOutputPoint(op2, discardLeft);
                }
            }

            if (dir1 == EdgeDirection.LeftToRight == discardLeft)
            {
                op1.Prev = op2;
                op2.Next = op1;
                op1B.Next = op2B;
                op2B.Prev = op1B;
            }
            else
            {
                op1.Next = op2;
                op2.Prev = op1;
                op1B.Prev = op2B;
                op2B.Next = op1B;
            }
            return true;
        }

        private bool JoinPoints(Join j, OutputPolygon outputPolygon1, OutputPolygon outputPolygon2)
        {
            var op1 = j.OutPoint1;
            var op2 = j.OutPoint2;

            OutputPoint op1B;
            OutputPoint op2B;

            // There are 3 kinds of joins for output polygons ...
            // 1. Horizontal joins where Join.OutPoint1 & Join.OutPoint2 are vertices anywhere
            //    along (horizontal) collinear edges (& Join.Offset is on the same horizontal).
            //
            // 2. Non-horizontal joins where Join.OutPoint1 & Join.OutPoint2 are at the same
            //    location at the Bottom of the overlapping segment (& Join.Offset is above).
            //
            // 3. StrictlySimple joins where edges touch but are not collinear and where
            //    Join.OutPoint1, Join.OutPoint2 & Join.Offset all share the same point.

            var isHorizontal = j.OutPoint1.Point.Y == j.Offset.Y;

            bool reverse1;
            bool reverse2;

            if (isHorizontal && j.Offset == j.OutPoint1.Point && j.Offset == j.OutPoint2.Point)
            {
                // Strictly Simple join ...
                if (outputPolygon1 != outputPolygon2)
                {
                    return false;
                }

                op1B = j.OutPoint1.Next;

                while (op1B != op1 && (op1B.Point == j.Offset))
                {
                    op1B = op1B.Next;
                }

                reverse1 = op1B.Point.Y > j.Offset.Y;

                op2B = j.OutPoint2.Next;

                while (op2B != op2 && op2B.Point == j.Offset)
                {
                    op2B = op2B.Next;
                }

                reverse2 = op2B.Point.Y > j.Offset.Y;

                if (reverse1 == reverse2)
                {
                    return false;
                }

                if (reverse1)
                {
                    op1B = DuplicateOutputPoint(op1, false);
                    op2B = DuplicateOutputPoint(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1B.Next = op2B;
                    op2B.Prev = op1B;
                    j.OutPoint1 = op1;
                    j.OutPoint2 = op1B;
                    return true;
                }

                op1B = DuplicateOutputPoint(op1, true);
                op2B = DuplicateOutputPoint(op2, false);
                op1.Next = op2;
                op2.Prev = op1;
                op1B.Prev = op2B;
                op2B.Next = op1B;
                j.OutPoint1 = op1;
                j.OutPoint2 = op1B;

                return true;
            }

            if (isHorizontal)
            {
                // treat horizontal joins differently to non-horizontal joins since with
                // them we're not yet sure where the overlapping is. OutPoint1.Point & OutPoint2.Point
                // may be anywhere along the horizontal edge.
                op1B = op1;
                while (op1.Prev.Point.Y == op1.Point.Y && op1.Prev != op1B && op1.Prev != op2)
                {
                    op1 = op1.Prev;
                }

                while (op1B.Next.Point.Y == op1B.Point.Y && op1B.Next != op1 && op1B.Next != op2)
                {
                    op1B = op1B.Next;
                }

                if (op1B.Next == op1 || op1B.Next == op2) return false; //a flat 'polygon'

                op2B = op2;
                while (op2.Prev.Point.Y == op2.Point.Y && op2.Prev != op2B && op2.Prev != op1B)
                {
                    op2 = op2.Prev;
                }
                while (op2B.Next.Point.Y == op2B.Point.Y && op2B.Next != op2 && op2B.Next != op1)
                {
                    op2B = op2B.Next;
                }
                if (op2B.Next == op2 || op2B.Next == op1) return false; //a flat 'polygon'

                long left, right;
                //point1 -. Op1b & point2 -. Op2b are the extremites of the horizontal edges
                if (!GetOverlap(op1.Point.X, op1B.Point.X, op2.Point.X, op2B.Point.X, out left, out right))
                {
                    return false;
                }

                //DiscardLeftSide: when overlapping edges are joined, a spike will created
                //which needs to be cleaned up. However, we don't want point1 or point2 caught up
                //on the discard Side as either may still be needed for other joins ...
                IntPoint point;
                bool discardLeftSide;
                if (op1.Point.X >= left && op1.Point.X <= right)
                {
                    point = op1.Point; discardLeftSide = op1.Point.X > op1B.Point.X;
                }
                else if (op2.Point.X >= left && op2.Point.X <= right)
                {
                    point = op2.Point; discardLeftSide = op2.Point.X > op2B.Point.X;
                }
                else if (op1B.Point.X >= left && op1B.Point.X <= right)
                {
                    point = op1B.Point; discardLeftSide = op1B.Point.X > op1.Point.X;
                }
                else
                {
                    point = op2B.Point; discardLeftSide = op2B.Point.X > op2.Point.X;
                }

                j.OutPoint1 = op1;
                j.OutPoint2 = op2;

                return JoinHorz(op1, op1B, op2, op2B, point, discardLeftSide);
            }
            // nb: For non-horizontal joins ...
            //    1. Jr.OutPoint1.Point.Y == Jr.OutPoint2.Point.Y
            //    2. Jr.OutPoint1.Point > Jr.Offset.Y

            // make sure the polygons are correctly oriented ...
            op1B = op1.Next;
            while (op1B.Point == op1.Point && op1B != op1)
            {
                op1B = op1B.Next;
            }

            reverse1 = op1B.Point.Y > op1.Point.Y ||
                           !GeometryHelper.SlopesEqual(op1.Point, op1B.Point, j.Offset, _useFullRange);

            if (reverse1)
            {
                op1B = op1.Prev;
                while (op1B.Point == op1.Point && op1B != op1)
                {
                    op1B = op1B.Prev;
                }

                if (op1B.Point.Y > op1.Point.Y ||
                    !GeometryHelper.SlopesEqual(op1.Point, op1B.Point, j.Offset, _useFullRange))
                {
                    return false;
                }
            }

            op2B = op2.Next;
            while ((op2B.Point == op2.Point) && (op2B != op2)) op2B = op2B.Next;

            reverse2 = op2B.Point.Y > op2.Point.Y ||
                           !GeometryHelper.SlopesEqual(op2.Point, op2B.Point, j.Offset, _useFullRange);

            if (reverse2)
            {
                op2B = op2.Prev;

                while (op2B.Point == op2.Point && op2B != op2)
                {
                    op2B = op2B.Prev;
                }

                if (op2B.Point.Y > op2.Point.Y ||
                    !GeometryHelper.SlopesEqual(op2.Point, op2B.Point, j.Offset, _useFullRange))
                {
                    return false;
                }
            }

            if (op1B == op1 || op2B == op2 || op1B == op2B ||
                outputPolygon1 == outputPolygon2 && reverse1 == reverse2)
            {
                return false;
            }

            if (reverse1)
            {
                op1B = DuplicateOutputPoint(op1, false);
                op2B = DuplicateOutputPoint(op2, true);
                op1.Prev = op2;
                op2.Next = op1;
                op1B.Next = op2B;
                op2B.Prev = op1B;
                j.OutPoint1 = op1;
                j.OutPoint2 = op1B;
                return true;
            }

            op1B = DuplicateOutputPoint(op1, true);
            op2B = DuplicateOutputPoint(op2, false);
            op1.Next = op2;
            op2.Prev = op1;
            op1B.Prev = op2B;
            op2B.Next = op1B;
            j.OutPoint1 = op1;
            j.OutPoint2 = op1B;
            return true;
        }

        private static int PointInPolygon(IntPoint point, OutputPoint op)
        {
            // returns 0 if false, +1 if true, -1 if point ON polygon boundary
            var result = 0;
            var startOp = op;
            var ptx = point.X;
            var pty = point.Y;
            var poly0X = op.Point.X;
            var poly0Y = op.Point.Y;
            do
            {
                op = op.Next;
                var poly1X = op.Point.X;
                var poly1Y = op.Point.Y;

                if (poly1Y == pty)
                {
                    if (poly1X == ptx || poly0Y == pty &&
                        poly1X > ptx == poly0X < ptx)
                    {
                        return -1;
                    }
                }

                if (poly0Y < pty != poly1Y < pty)
                {
                    if (poly0X >= ptx)
                    {
                        if (poly1X > ptx)
                        {
                            result = 1 - result;
                        }
                        else
                        {
                            var d = (double)(poly0X - ptx) * (poly1Y - pty) -
                                    (double)(poly1X - ptx) * (poly0Y - pty);

                            if (GeometryHelper.NearZero(d))
                            {
                                return -1;
                            }

                            if (d > 0 == poly1Y > poly0Y)
                            {
                                result = 1 - result;
                            }
                        }
                    }
                    else
                    {
                        if (poly1X > ptx)
                        {
                            var d = (double)(poly0X - ptx) * (poly1Y - pty) -
                                    (double)(poly1X - ptx) * (poly0Y - pty);

                            if (GeometryHelper.NearZero(d))
                            {
                                return -1;
                            }

                            if (d > 0 == poly1Y > poly0Y)
                            {
                                result = 1 - result;
                            }
                        }
                    }
                }

                poly0X = poly1X;
                poly0Y = poly1Y;

            } while (startOp != op);

            return result;
        }

        private static bool Poly2ContainsPoly1(OutputPoint outputPoint1, OutputPoint outputPoint2)
        {
            var op = outputPoint1;
            do
            {
                // nb: PointInPolygon returns 0 if false, +1 if true, -1 if point on polygon
                var res = PointInPolygon(op.Point, outputPoint2);

                if (res >= 0)
                {
                    return res > 0;
                }
                op = op.Next;
            }
            while (op != outputPoint1);

            return true;
        }

        private void FixupFirstLefts1(OutputPolygon oldOutputPolygon, OutputPolygon newOutputPolygon)
        {
            foreach (var polygon in _outputPolygons)
            {
                var firstLeft = ParseFirstLeft(polygon.FirstLeft);

                if (polygon.Points == null || firstLeft != oldOutputPolygon)
                {
                    continue;
                }

                if (Poly2ContainsPoly1(polygon.Points, newOutputPolygon.Points))
                {
                    polygon.FirstLeft = newOutputPolygon;
                }
            }
        }

        private void FixupFirstLefts2(OutputPolygon innerOutputPolygon, OutputPolygon outerOutputPolygon)
        {
            // A polygon has split into two such that one is now the inner of the other.
            // It's possible that these polygons now wrap around other polygons, so check
            // every polygon that's also contained by OuterOutRec's FirstLeft container
            // (including nil) to see if they've become inner to the new inner polygon ...
            var outRecFirstLeft = outerOutputPolygon.FirstLeft;

            foreach (var polygon in _outputPolygons)
            {
                if (polygon.Points == null || polygon == outerOutputPolygon || polygon == innerOutputPolygon)
                {
                    continue;
                }

                var firstLeft = ParseFirstLeft(polygon.FirstLeft);

                if (firstLeft != outRecFirstLeft && firstLeft != innerOutputPolygon && firstLeft != outerOutputPolygon)
                {
                    continue;
                }

                if (Poly2ContainsPoly1(polygon.Points, innerOutputPolygon.Points))
                {
                    polygon.FirstLeft = innerOutputPolygon;
                }
                else if (Poly2ContainsPoly1(polygon.Points, outerOutputPolygon.Points))
                {
                    polygon.FirstLeft = outerOutputPolygon;
                }
                else if (polygon.FirstLeft == innerOutputPolygon || polygon.FirstLeft == outerOutputPolygon)
                {
                    polygon.FirstLeft = outRecFirstLeft;
                }
            }
        }

        private void FixupFirstLefts3(OutputPolygon oldOutputPolygon, OutputPolygon newOutputPolygon)
        {
            // same as FixupFirstLefts1 but doesn't call Poly2ContainsPoly1()
            foreach (var polygon in _outputPolygons)
            {
                var firstLeft = ParseFirstLeft(polygon.FirstLeft);
                if (polygon.Points != null && firstLeft == oldOutputPolygon)
                {
                    polygon.FirstLeft = newOutputPolygon;
                }
            }
        }

        private static OutputPolygon ParseFirstLeft(OutputPolygon firstLeft)
        {
            while (firstLeft != null && firstLeft.Points == null)
            {
                firstLeft = firstLeft.FirstLeft;
            }
            return firstLeft;
        }

        private void JoinCommonEdges()
        {
            foreach (var j in _joins)
            {
                var outputPolygon1 = GetOutPolygon(j.OutPoint1.Index);
                var outputPolygon2 = GetOutPolygon(j.OutPoint2.Index);

                if (outputPolygon1.Points == null || outputPolygon2.Points == null) continue;
                if (outputPolygon1.IsOpen || outputPolygon2.IsOpen) continue;

                // get the polygon fragment with the correct hole state (FirstLeft)
                // before calling JoinPoints() ...
                OutputPolygon hole;
                if (outputPolygon1 == outputPolygon2)
                {
                    hole = outputPolygon1;
                }
                else if (OutPolygon1RightOfOutPolygon2(outputPolygon1, outputPolygon2)) hole = outputPolygon2;
                else if (OutPolygon1RightOfOutPolygon2(outputPolygon2, outputPolygon1)) hole = outputPolygon1;
                else hole = GetLowermostRec(outputPolygon1, outputPolygon2);

                if (!JoinPoints(j, outputPolygon1, outputPolygon2)) continue;

                if (outputPolygon1 == outputPolygon2)
                {
                    // instead of joining two polygons, we've just created a new one by
                    // splitting one polygon into two.
                    outputPolygon1.Points = j.OutPoint1;
                    outputPolygon1.BottomPoint = null;
                    outputPolygon2 = CreateOutputPolygon();
                    outputPolygon2.Points = j.OutPoint2;

                    // update all outputPolygon2.Points Index's ...
                    UpdateOutPtIdxs(outputPolygon2);

                    if (Poly2ContainsPoly1(outputPolygon2.Points, outputPolygon1.Points))
                    {
                        // outPolygon1 contains outPolygon2 ...
                        outputPolygon2.IsHole = !outputPolygon1.IsHole;
                        outputPolygon2.FirstLeft = outputPolygon1;

                        if (_usingTreeSolution) FixupFirstLefts2(outputPolygon2, outputPolygon1);

                        if ((outputPolygon2.IsHole ^ ReverseSolution) == outputPolygon2.Area > 0)
                            ReverseLinks(outputPolygon2.Points);

                    }
                    else if (Poly2ContainsPoly1(outputPolygon1.Points, outputPolygon2.Points))
                    {
                        // outPolygon2 contains outPolygon1 ...
                        outputPolygon2.IsHole = outputPolygon1.IsHole;
                        outputPolygon1.IsHole = !outputPolygon2.IsHole;
                        outputPolygon2.FirstLeft = outputPolygon1.FirstLeft;
                        outputPolygon1.FirstLeft = outputPolygon2;

                        if (_usingTreeSolution)
                        {
                            FixupFirstLefts2(outputPolygon1, outputPolygon2);
                        }

                        if ((outputPolygon1.IsHole ^ ReverseSolution) == outputPolygon1.Area > 0)
                        {
                            ReverseLinks(outputPolygon1.Points);
                        }
                    }
                    else
                    {
                        // the 2 polygons are completely separate ...
                        outputPolygon2.IsHole = outputPolygon1.IsHole;
                        outputPolygon2.FirstLeft = outputPolygon1.FirstLeft;

                        // fixup FirstLeft pointers that may need reassigning to OutRec2
                        if (_usingTreeSolution)
                        {
                            FixupFirstLefts1(outputPolygon1, outputPolygon2);
                        }
                    }

                }
                else
                {
                    //joined 2 polygons together ...

                    outputPolygon2.Points = null;
                    outputPolygon2.BottomPoint = null;
                    outputPolygon2.Index = outputPolygon1.Index;

                    outputPolygon1.IsHole = hole.IsHole;
                    if (hole == outputPolygon2)
                        outputPolygon1.FirstLeft = outputPolygon2.FirstLeft;
                    outputPolygon2.FirstLeft = outputPolygon1;

                    //fixup FirstLeft pointers that may need reassigning to OutRec1
                    if (_usingTreeSolution)
                    {
                        FixupFirstLefts3(outputPolygon2, outputPolygon1);
                    }
                }
            }
        }

        private static void UpdateOutPtIdxs(OutputPolygon outputPolygon)
        {
            var points = outputPolygon.Points;
            do
            {
                points.Index = outputPolygon.Index;
                points = points.Prev;
            }
            while (points != outputPolygon.Points);
        }

        private void DoSimplePolygons()
        {
            var i = 0;
            while (i < _outputPolygons.Count)
            {
                var outputPolygon1 = _outputPolygons[i++];
                var op = outputPolygon1.Points;
                if (op == null || outputPolygon1.IsOpen)
                {
                    continue;
                }

                do // for each Point in Polygon until duplicate found do ...
                {
                    var op2 = op.Next;
                    while (op2 != outputPolygon1.Points)
                    {
                        if (op.Point == op2.Point && op2.Next != op && op2.Prev != op)
                        {
                            // split the polygon into two ...
                            var op3 = op.Prev;
                            var op4 = op2.Prev;
                            op.Prev = op4;
                            op4.Next = op;
                            op2.Prev = op3;
                            op3.Next = op2;

                            outputPolygon1.Points = op;
                            var outputPolygon2 = CreateOutputPolygon();
                            outputPolygon2.Points = op2;
                            UpdateOutPtIdxs(outputPolygon2);
                            if (Poly2ContainsPoly1(outputPolygon2.Points, outputPolygon1.Points))
                            {
                                // outputPolygon2 is contained by outputPolygon1
                                outputPolygon2.IsHole = !outputPolygon1.IsHole;
                                outputPolygon2.FirstLeft = outputPolygon1;
                                if (_usingTreeSolution)
                                {
                                    FixupFirstLefts2(outputPolygon2, outputPolygon1);
                                }
                            }
                            else if (Poly2ContainsPoly1(outputPolygon1.Points, outputPolygon2.Points))
                            {
                                // outputPolygon1 is contained by outputPolygon2.
                                outputPolygon2.IsHole = outputPolygon1.IsHole;
                                outputPolygon1.IsHole = !outputPolygon2.IsHole;
                                outputPolygon2.FirstLeft = outputPolygon1.FirstLeft;
                                outputPolygon1.FirstLeft = outputPolygon2;
                                if (_usingTreeSolution)
                                {
                                    FixupFirstLefts2(outputPolygon1, outputPolygon2);
                                }
                            }
                            else
                            {
                                // the 2 polygons are separate ...
                                outputPolygon2.IsHole = outputPolygon1.IsHole;
                                outputPolygon2.FirstLeft = outputPolygon1.FirstLeft;
                                if (_usingTreeSolution)
                                {
                                    FixupFirstLefts1(outputPolygon1, outputPolygon2);
                                }
                            }
                            op2 = op; // Get ready for the next iteration
                        }
                        op2 = op2.Next;
                    }
                    op = op.Next;
                }
                while (op != outputPolygon1.Points);
            }
        }
    }
}
