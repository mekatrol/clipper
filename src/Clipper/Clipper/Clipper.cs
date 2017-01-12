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

        private ClipOperation _mClipOperation;
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

                if (start.IsHorizontal) //ie an adjoining horizontal skip edge
                {
                    if (start.Bottom.X != edge.Bottom.X && start.Top.X != edge.Bottom.X)
                    {
                        ReverseHorizontal(edge);
                    }
                }
                else if (start.Bottom.X != edge.Bottom.X)
                {
                    ReverseHorizontal(edge);
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
                        ReverseHorizontal(edge);
                    }
                    edge = edge.Next;
                }
                if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Prev.Top.X)
                {
                    ReverseHorizontal(edge);
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
                        ReverseHorizontal(edge);
                    }
                    edge = edge.Prev;
                }
                if (edge.IsHorizontal && edge != start && edge.Bottom.X != edge.Next.Top.X)
                {
                    ReverseHorizontal(edge);
                }

                result = result.Prev; //move to the edge just beyond current bound
            }

            return result;
        }

        public bool AddPath(Polygon pg, PolygonKind polygonKind, bool closed)
        {
#if use_lines
            if (!closed && polygonKind == PolygonKind.Clip)
                throw new Exception("AddPath: Open paths must be subject.");
#else
      if (!Closed)
        throw new ClipperException("AddPath: Open paths have been disabled.");
#endif

            var highI = pg.Count - 1;
            if (closed) while (highI > 0 && (pg[highI] == pg[0])) --highI;
            while (highI > 0 && (pg[highI] == pg[highI - 1])) --highI;
            if ((closed && highI < 2) || (!closed && highI < 1)) return false;

            //create a new edge array ...
            var edges = new List<Edge>(highI + 1);
            for (var i = 0; i <= highI; i++) edges.Add(new Edge());

            var isFlat = true;

            //1. Basic (first) edge initialization ...
            edges[1].Current = pg[1];
            GeometryHelper.RangeTest(pg[0], ref _useFullRange);
            GeometryHelper.RangeTest(pg[highI], ref _useFullRange);
            edges[0].InitializeEdge(edges[1], edges[highI], pg[0]);
            edges[highI].InitializeEdge(edges[0], edges[highI - 1], pg[highI]);
            for (var i = highI - 1; i >= 1; --i)
            {
                GeometryHelper.RangeTest(pg[i], ref _useFullRange);
                edges[i].InitializeEdge(edges[i + 1], edges[i - 1], pg[i]);
            }

            Edge eStart = edges[0];

            //2. Remove duplicate vertices, and (when closed) collinear edges ...
            Edge edge = eStart, eLoopStop = eStart;
            for (;;)
            {
                //nb: allows matching start and end points when not Closed ...
                if (edge.Current == edge.Next.Current && (closed || edge.Next != eStart))
                {
                    if (edge == edge.Next) break;
                    if (edge == eStart) eStart = edge.Next;
                    edge = RemoveEdge(edge);
                    eLoopStop = edge;
                    continue;
                }
                if (edge.Prev == edge.Next)
                    break; //only two vertices
                else if (closed && GeometryHelper.SlopesEqual(edge.Prev.Current, edge.Current, edge.Next.Current, _useFullRange) &&
                  (!PreserveCollinear ||
                  !Pt2IsBetweenPt1AndPt3(edge.Prev.Current, edge.Current, edge.Next.Current)))
                {
                    //Collinear edges are allowed for open paths but in closed paths
                    //the default is to merge adjacent collinear edges into a single edge.
                    //However, if the PreserveCollinear property is enabled, only overlapping
                    //collinear edges (ie spikes) will be removed from closed paths.
                    if (edge == eStart) eStart = edge.Next;
                    edge = RemoveEdge(edge);
                    edge = edge.Prev;
                    eLoopStop = edge;
                    continue;
                }
                edge = edge.Next;
                if ((edge == eLoopStop) || (!closed && edge.Next == eStart)) break;
            }

            if ((!closed && (edge == edge.Next)) || (closed && (edge.Prev == edge.Next)))
                return false;

            if (!closed)
            {
                _hasOpenPaths = true;
                eStart.Prev.OutIndex = ClippingHelper.Skip;
            }

            //3. Do second stage of edge initialization ...
            edge = eStart;
            do
            {
                edge.InitializeEdge(polygonKind);
                edge = edge.Next;
                if (isFlat && edge.Current.Y != eStart.Current.Y) isFlat = false;
            }
            while (edge != eStart);

            //4. Finally, add edge bounds to LocalMinima list ...

            // Totally flat paths must be handled differently when adding them
            // to LocalMinima list to avoid endless loops etc.
            if (isFlat)
            {
                if (closed) return false;

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
                    if (edge.Bottom.X != edge.Prev.Top.X) ReverseHorizontal(edge);
                    if (edge.Next.OutIndex == ClippingHelper.Skip) break;
                    edge.NextInLml = edge.Next;
                    edge = edge.Next;
                }

                InsertLocalMinima(localMinima);
                return true;
            }

            Edge startLocalMinima = null;

            //workaround to avoid an endless loop in the while loop below when
            //open paths have matching start and end points ...
            if (edge.Prev.Bottom == edge.Prev.Top) edge = edge.Next;

            while (true)
            {
                // Find next local minima
                edge = FindNextLocalMinima(edge);

                // Back to begining?
                if (edge == startLocalMinima) break;

                // Record start local minima.
                if (startLocalMinima == null)
                {
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

                if (!closed) localMinima.LeftBound.WindDelta = 0;
                else if (localMinima.LeftBound.Next == localMinima.RightBound)
                    localMinima.LeftBound.WindDelta = -1;
                else localMinima.LeftBound.WindDelta = 1;
                localMinima.RightBound.WindDelta = -localMinima.LeftBound.WindDelta;

                edge = ProcessBound(localMinima.LeftBound, leftBoundIsForward);
                if (edge.OutIndex == ClippingHelper.Skip) edge = ProcessBound(edge, leftBoundIsForward);

                Edge E2 = ProcessBound(localMinima.RightBound, !leftBoundIsForward);
                if (E2.OutIndex == ClippingHelper.Skip) E2 = ProcessBound(E2, !leftBoundIsForward);

                if (localMinima.LeftBound.OutIndex == ClippingHelper.Skip)
                    localMinima.LeftBound = null;
                else if (localMinima.RightBound.OutIndex == ClippingHelper.Skip)
                    localMinima.RightBound = null;
                InsertLocalMinima(localMinima);
                if (!leftBoundIsForward) edge = E2;
            }
            return true;

        }

        public bool AddPaths(PolygonPath ppg, PolygonKind polygonKind)
        {
            bool result = false;
            for (int i = 0; i < ppg.Count; ++i)
                if (AddPath(ppg[i], polygonKind, ppg[i].IsClosed)) result = true;
            return result;
        }

        internal bool Pt2IsBetweenPt1AndPt3(IntPoint pt1, IntPoint pt2, IntPoint pt3)
        {
            if ((pt1 == pt3) || (pt1 == pt2) || (pt3 == pt2)) return false;
            else if (pt1.X != pt3.X) return (pt2.X > pt1.X) == (pt2.X < pt3.X);
            else return (pt2.Y > pt1.Y) == (pt2.Y < pt3.Y);
        }

        private static Edge RemoveEdge(Edge e)
        {
            //removes edge from double_linked_list (but without removing from memory)
            e.Prev.Next = e.Next;
            e.Next.Prev = e.Prev;
            Edge result = e.Next;
            e.Prev = null; //flag as removed (see ClipperBase.Clear)
            return result;
        }

        private void InsertLocalMinima(LocalMinima newLm)
        {
            if (_minimaList == null)
            {
                _minimaList = newLm;
            }
            else if (newLm.Y >= _minimaList.Y)
            {
                newLm.Next = _minimaList;
                _minimaList = newLm;
            }
            else
            {
                LocalMinima tmpLm = _minimaList;
                while (tmpLm.Next != null && (newLm.Y < tmpLm.Next.Y))
                    tmpLm = tmpLm.Next;
                newLm.Next = tmpLm.Next;
                tmpLm.Next = newLm;
            }
        }

        internal bool PopLocalMinima(long Y, out LocalMinima current)
        {
            current = _currentLocalMinima;
            if (_currentLocalMinima != null && _currentLocalMinima.Y == Y)
            {
                _currentLocalMinima = _currentLocalMinima.Next;
                return true;
            }
            return false;
        }

        private static void ReverseHorizontal(Edge e)
        {
            //swap horizontal edges' top and bottom x's so they follow the natural
            //progression of the bounds - ie so their xbots will align with the
            //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]
            GeometryHelper.Swap(ref e.Top.X, ref e.Bottom.X);
        }

        internal virtual void Reset()
        {
            _currentLocalMinima = _minimaList;
            if (_currentLocalMinima == null) return; //ie nothing to process

            //reset all edges ...
            _scanbeam = null;
            LocalMinima lm = _minimaList;
            while (lm != null)
            {
                InsertScanbeam(lm.Y);
                Edge e = lm.LeftBound;
                if (e != null)
                {
                    e.Current = e.Bottom;
                    e.OutIndex = ClippingHelper.Unassigned;
                }
                e = lm.RightBound;
                if (e != null)
                {
                    e.Current = e.Bottom;
                    e.OutIndex = ClippingHelper.Unassigned;
                }
                lm = lm.Next;
            }
            _activeEdges = null;
        }

        public static IntRect GetBounds(PolygonPath paths)
        {
            int i = 0, cnt = paths.Count;
            while (i < cnt && paths[i].Count == 0) i++;
            if (i == cnt) return new IntRect(0, 0, 0, 0);
            var result = new IntRect { Left = paths[i][0].X };
            result.Right = result.Left;
            result.Top = paths[i][0].Y;
            result.Bottom = result.Top;
            for (; i < cnt; i++)
                for (var j = 0; j < paths[i].Count; j++)
                {
                    if (paths[i][j].X < result.Left) result.Left = paths[i][j].X;
                    else if (paths[i][j].X > result.Right) result.Right = paths[i][j].X;
                    if (paths[i][j].Y < result.Top) result.Top = paths[i][j].Y;
                    else if (paths[i][j].Y > result.Bottom) result.Bottom = paths[i][j].Y;
                }
            return result;
        }

        internal void InsertScanbeam(long Y)
        {
            //single-linked list: sorted descending, ignoring dups.
            if (_scanbeam == null)
            {
                _scanbeam = new Scanbeam
                {
                    Y = Y
                };
            }
            else if (Y > _scanbeam.Y)
            {
                var newSb = new Scanbeam
                {
                    Y = Y,
                    Next = _scanbeam
                };
                _scanbeam = newSb;
            }
            else
            {
                var sb2 = _scanbeam;
                while (sb2.Next != null && (Y <= sb2.Next.Y)) sb2 = sb2.Next;
                if (Y == sb2.Y) return; //ie ignores duplicates
                var newSb = new Scanbeam
                {
                    Y = Y,
                    Next = sb2.Next
                };
                sb2.Next = newSb;
            }
        }

        internal bool PopScanbeam(out long Y)
        {
            if (_scanbeam == null)
            {
                Y = 0;
                return false;
            }
            Y = _scanbeam.Y;
            _scanbeam = _scanbeam.Next;
            return true;
        }

        internal bool LocalMinimaPending()
        {
            return (_currentLocalMinima != null);
        }

        internal OutputPolygon CreateOutRec()
        {
            var result = new OutputPolygon
            {
                Index = ClippingHelper.Unassigned,
                IsHole = false,
                IsOpen = false,
                FirstLeft = null,
                Points = null,
                BottomPoint = null,
                PolygonNode = null
            };
            _outputPolygons.Add(result);
            result.Index = _outputPolygons.Count - 1;
            return result;
        }

        internal void DisposeOutRec(int index)
        {
            OutputPolygon outputPolygon = _outputPolygons[index];
            outputPolygon.Points = null;
            outputPolygon = null;
            _outputPolygons[index] = null;
        }

        internal void UpdateEdgeIntoAel(ref Edge edge)
        {
            if (edge.NextInLml == null)
            {
                throw new Exception("UpdateEdgeIntoAEL: invalid call");
            }

            Edge AelPrev = edge.PrevInAel;
            Edge AelNext = edge.NextInAel;
            edge.NextInLml.OutIndex = edge.OutIndex;
            if (AelPrev != null)
                AelPrev.NextInAel = edge.NextInLml;
            else _activeEdges = edge.NextInLml;
            if (AelNext != null)
                AelNext.PrevInAel = edge.NextInLml;
            edge.NextInLml.Side = edge.Side;
            edge.NextInLml.WindDelta = edge.WindDelta;
            edge.NextInLml.WindCount = edge.WindCount;
            edge.NextInLml.WindCount2 = edge.WindCount2;
            edge = edge.NextInLml;
            edge.Current = edge.Bottom;
            edge.PrevInAel = AelPrev;
            edge.NextInAel = AelNext;
            if (!edge.IsHorizontal) InsertScanbeam(edge.Top.Y);
        }

        internal void SwapPositionsInAel(Edge edge1, Edge edge2)
        {
            //check that one or other edge hasn't already been removed from AEL ...
            if (edge1.NextInAel == edge1.PrevInAel ||
              edge2.NextInAel == edge2.PrevInAel) return;

            if (edge1.NextInAel == edge2)
            {
                Edge next = edge2.NextInAel;
                if (next != null)
                    next.PrevInAel = edge1;
                Edge prev = edge1.PrevInAel;
                if (prev != null)
                    prev.NextInAel = edge2;
                edge2.PrevInAel = prev;
                edge2.NextInAel = edge1;
                edge1.PrevInAel = edge2;
                edge1.NextInAel = next;
            }
            else if (edge2.NextInAel == edge1)
            {
                Edge next = edge1.NextInAel;
                if (next != null)
                    next.PrevInAel = edge2;
                Edge prev = edge2.PrevInAel;
                if (prev != null)
                    prev.NextInAel = edge1;
                edge1.PrevInAel = prev;
                edge1.NextInAel = edge2;
                edge2.PrevInAel = edge1;
                edge2.NextInAel = next;
            }
            else
            {
                Edge next = edge1.NextInAel;
                Edge prev = edge1.PrevInAel;
                edge1.NextInAel = edge2.NextInAel;
                if (edge1.NextInAel != null)
                    edge1.NextInAel.PrevInAel = edge1;
                edge1.PrevInAel = edge2.PrevInAel;
                if (edge1.PrevInAel != null)
                    edge1.PrevInAel.NextInAel = edge1;
                edge2.NextInAel = next;
                if (edge2.NextInAel != null)
                    edge2.NextInAel.PrevInAel = edge2;
                edge2.PrevInAel = prev;
                if (edge2.PrevInAel != null)
                    edge2.PrevInAel.NextInAel = edge2;
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

        internal void DeleteFromAel(Edge e)
        {
            var aelPrev = e.PrevInAel;
            var aelNext = e.NextInAel;

            if (aelPrev == null && aelNext == null && e != _activeEdges)
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
                aelNext.PrevInAel = aelPrev;
            e.NextInAel = null;
            e.PrevInAel = null;
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
            _mClipOperation = clipOperation;
            _usingTreeSolution = false;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded) BuildResult(solution);
            }
            finally
            {
                DisposeAllPolyPts();
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
            _mClipOperation = clipOperation;
            _usingTreeSolution = true;
            bool succeeded;
            try
            {
                succeeded = ExecuteInternal();
                //build the return polygons ...
                if (succeeded) BuildResult2(polytree);
            }
            finally
            {
                DisposeAllPolyPts();
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

            OutputPolygon orfl = outputPolygon.FirstLeft;
            while (orfl != null && ((orfl.IsHole == outputPolygon.IsHole) || orfl.Points == null))
                orfl = orfl.FirstLeft;
            outputPolygon.FirstLeft = orfl;
        }

        private bool ExecuteInternal()
        {
            Reset();

            _sortedEdges = null;
            _maxima = null;

            if (!PopScanbeam(out long botY)) return false;

            InsertLocalMinimaIntoAel(botY);

            while (PopScanbeam(out long topY) || LocalMinimaPending())
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

            // fix orientations ...
            foreach (var outputPolygon in _outputPolygons)
            {
                if (outputPolygon.Points == null || outputPolygon.IsOpen) continue;
                if ((outputPolygon.IsHole ^ ReverseSolution) == (Area(outputPolygon) > 0))
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

        private void DisposeAllPolyPts()
        {
            for (int i = 0; i < _outputPolygons.Count; ++i) DisposeOutRec(i);
            _outputPolygons.Clear();
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
            while (PopLocalMinima(botY, out var lm))
            {
                var leftBound = lm.LeftBound;
                var rightBound = lm.RightBound;

                OutputPoint op1 = null;
                if (leftBound == null)
                {
                    InsertEdgeIntoAel(rightBound, null);
                    SetWindingCount(rightBound);
                    if (IsContributing(rightBound))
                        op1 = AddOutPt(rightBound, rightBound.Bottom);
                }
                else if (rightBound == null)
                {
                    InsertEdgeIntoAel(leftBound, null);
                    SetWindingCount(leftBound);
                    if (IsContributing(leftBound))
                        op1 = AddOutPt(leftBound, leftBound.Bottom);
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
                        op1 = AddLocalMinPoly(leftBound, rightBound, leftBound.Bottom);
                    InsertScanbeam(leftBound.Top.Y);
                }

                if (rightBound != null)
                {
                    if (rightBound.IsHorizontal)
                    {
                        if (rightBound.NextInLml != null)
                            InsertScanbeam(rightBound.NextInLml.Top.Y);
                        AddEdgeToSel(rightBound);
                    }
                    else
                        InsertScanbeam(rightBound.Top.Y);
                }

                if (leftBound == null || rightBound == null) continue;

                //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
                if (op1 != null && rightBound.IsHorizontal &&
                  _ghostJoins.Count > 0 && rightBound.WindDelta != 0)
                {
                    for (int i = 0; i < _ghostJoins.Count; i++)
                    {
                        //if the horizontal Rb and a 'ghost' horizontal overlap, then convert
                        //the 'ghost' join to a real join ready for later ...
                        Join j = _ghostJoins[i];
                        if (HorzSegmentsOverlap(j.OutPoint1.Point.X, j.Offset.X, rightBound.Bottom.X, rightBound.Top.X))
                            AddJoin(j.OutPoint1, op1, j.Offset);
                    }
                }

                if (leftBound.OutIndex >= 0 && leftBound.PrevInAel != null &&
                  leftBound.PrevInAel.Current.X == leftBound.Bottom.X &&
                  leftBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(leftBound.PrevInAel.Current, leftBound.PrevInAel.Top, leftBound.Current, leftBound.Top, _useFullRange) &&
                  leftBound.WindDelta != 0 && leftBound.PrevInAel.WindDelta != 0)
                {
                    OutputPoint Op2 = AddOutPt(leftBound.PrevInAel, leftBound.Bottom);
                    AddJoin(op1, Op2, leftBound.Top);
                }

                if (leftBound.NextInAel != rightBound)
                {

                    if (rightBound.OutIndex >= 0 && rightBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(rightBound.PrevInAel.Current, rightBound.PrevInAel.Top, rightBound.Current, rightBound.Top, _useFullRange) &&
                      rightBound.WindDelta != 0 && rightBound.PrevInAel.WindDelta != 0)
                    {
                        OutputPoint Op2 = AddOutPt(rightBound.PrevInAel, rightBound.Bottom);
                        AddJoin(op1, Op2, rightBound.Top);
                    }

                    Edge e = leftBound.NextInAel;
                    if (e != null)
                        while (e != rightBound)
                        {
                            //nb: For calculating winding counts etc, IntersectEdges() assumes
                            //that param1 will be to the right of param2 ABOVE the intersection ...
                            IntersectEdges(rightBound, e, leftBound.Current); //order important here
                            e = e.NextInAel;
                        }
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
                    startEdge = startEdge.NextInAel;
                edge.NextInAel = startEdge.NextInAel;
                if (startEdge.NextInAel != null) startEdge.NextInAel.PrevInAel = edge;
                edge.PrevInAel = startEdge;
                startEdge.NextInAel = edge;
            }
        }

        private static bool E2InsertsBeforeE1(Edge e1, Edge e2)
        {
            if (e2.Current.X == e1.Current.X)
            {
                if (e2.Top.Y > e1.Top.Y)
                    return e2.Top.X < TopX(e1, e2.Top.Y);
                else return e1.Top.X > TopX(e2, e1.Top.Y);
            }
            else return e2.Current.X < e1.Current.X;
        }

        private bool IsEvenOddFillType(Edge edge)
        {
            if (edge.Kind == PolygonKind.Subject)
                return _subjFillType == PolygonFillType.EvenOdd;
            else
                return _clipFillType == PolygonFillType.EvenOdd;
        }

        private bool IsEvenOddAltFillType(Edge edge)
        {
            if (edge.Kind == PolygonKind.Subject)
                return _clipFillType == PolygonFillType.EvenOdd;
            else
                return _subjFillType == PolygonFillType.EvenOdd;
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
                    //return false if a subj line has been flagged as inside a subj polygon
                    if (edge.WindDelta == 0 && edge.WindCount != 1) return false;
                    break;
                case PolygonFillType.NonZero:
                    if (Math.Abs(edge.WindCount) != 1) return false;
                    break;
                case PolygonFillType.Positive:
                    if (edge.WindCount != 1) return false;
                    break;
                default: //PolygonFillType.Negative
                    if (edge.WindCount != -1) return false;
                    break;
            }

            switch (_mClipOperation)
            {
                case ClipOperation.Intersection:
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
                    else
                        return true;
            }
            return true;
        }

        private void SetWindingCount(Edge edge)
        {
            Edge e = edge.PrevInAel;
            //find the edge of the same polytype that immediately preceeds 'edge' in AEL
            while (e != null && ((e.Kind != edge.Kind) || (e.WindDelta == 0))) e = e.PrevInAel;
            if (e == null)
            {
                PolygonFillType pft;
                pft = (edge.Kind == PolygonKind.Subject ? _subjFillType : _clipFillType);
                if (edge.WindDelta == 0) edge.WindCount = (pft == PolygonFillType.Negative ? -1 : 1);
                else edge.WindCount = edge.WindDelta;
                edge.WindCount2 = 0;
                e = _activeEdges; //ie get ready to calc WindCount2
            }
            else if (edge.WindDelta == 0 && _mClipOperation != ClipOperation.Union)
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
                    bool Inside = true;
                    Edge e2 = e.PrevInAel;
                    while (e2 != null)
                    {
                        if (e2.Kind == e.Kind && e2.WindDelta != 0)
                            Inside = !Inside;
                        e2 = e2.PrevInAel;
                    }
                    edge.WindCount = (Inside ? 0 : 1);
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
            Edge e = _activeEdges;
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
                return;
            if (edge2.NextInSel == null && edge2.PrevInSel == null)
                return;

            if (edge1.NextInSel == edge2)
            {
                Edge next = edge2.NextInSel;
                if (next != null)
                    next.PrevInSel = edge1;
                Edge prev = edge1.PrevInSel;
                if (prev != null)
                    prev.NextInSel = edge2;
                edge2.PrevInSel = prev;
                edge2.NextInSel = edge1;
                edge1.PrevInSel = edge2;
                edge1.NextInSel = next;
            }
            else if (edge2.NextInSel == edge1)
            {
                Edge next = edge1.NextInSel;
                if (next != null)
                    next.PrevInSel = edge2;
                Edge prev = edge2.PrevInSel;
                if (prev != null)
                    prev.NextInSel = edge1;
                edge1.PrevInSel = prev;
                edge1.NextInSel = edge2;
                edge2.PrevInSel = edge1;
                edge2.NextInSel = next;
            }
            else
            {
                Edge next = edge1.NextInSel;
                Edge prev = edge1.PrevInSel;
                edge1.NextInSel = edge2.NextInSel;
                if (edge1.NextInSel != null)
                    edge1.NextInSel.PrevInSel = edge1;
                edge1.PrevInSel = edge2.PrevInSel;
                if (edge1.PrevInSel != null)
                    edge1.PrevInSel.NextInSel = edge1;
                edge2.NextInSel = next;
                if (edge2.NextInSel != null)
                    edge2.NextInSel.PrevInSel = edge2;
                edge2.PrevInSel = prev;
                if (edge2.PrevInSel != null)
                    edge2.PrevInSel.NextInSel = edge2;
            }

            if (edge1.PrevInSel == null)
                _sortedEdges = edge1;
            else if (edge2.PrevInSel == null)
                _sortedEdges = edge2;
        }

        private void AddLocalMaxPoly(Edge edge1, Edge edge2, IntPoint point)
        {
            AddOutPt(edge1, point);
            if (edge2.WindDelta == 0) AddOutPt(edge2, point);
            if (edge1.OutIndex == edge2.OutIndex)
            {
                edge1.OutIndex = ClippingHelper.Unassigned;
                edge2.OutIndex = ClippingHelper.Unassigned;
            }
            else if (edge1.OutIndex < edge2.OutIndex)
                AppendPolygon(edge1, edge2);
            else
                AppendPolygon(edge2, edge1);
        }

        private OutputPoint AddLocalMinPoly(Edge edge1, Edge edge2, IntPoint point)
        {
            OutputPoint result;
            Edge e, prevE;
            if (edge2.IsHorizontal || (edge1.Dx > edge2.Dx))
            {
                result = AddOutPt(edge1, point);
                edge2.OutIndex = edge1.OutIndex;
                edge1.Side = EdgeSide.Left;
                edge2.Side = EdgeSide.Right;
                e = edge1;
                if (e.PrevInAel == edge2)
                    prevE = edge2.PrevInAel;
                else
                    prevE = e.PrevInAel;
            }
            else
            {
                result = AddOutPt(edge2, point);
                edge1.OutIndex = edge2.OutIndex;
                edge1.Side = EdgeSide.Right;
                edge2.Side = EdgeSide.Left;
                e = edge2;
                if (e.PrevInAel == edge1)
                    prevE = edge1.PrevInAel;
                else
                    prevE = e.PrevInAel;
            }

            if (prevE != null && prevE.OutIndex >= 0)
            {
                long xPrev = TopX(prevE, point.Y);
                long xE = TopX(e, point.Y);
                if ((xPrev == xE) && (e.WindDelta != 0) && (prevE.WindDelta != 0) && GeometryHelper.SlopesEqual(new IntPoint(xPrev, point.Y), prevE.Top, new IntPoint(xE, point.Y), e.Top, _useFullRange))
                {
                    OutputPoint outputPoint = AddOutPt(prevE, point);
                    AddJoin(result, outputPoint, e.Top);
                }
            }
            return result;
        }

        private OutputPoint AddOutPt(Edge edge, IntPoint point)
        {
            if (edge.OutIndex < 0)
            {
                OutputPolygon outputPolygon = CreateOutRec();
                outputPolygon.IsOpen = (edge.WindDelta == 0);
                OutputPoint newOp = new OutputPoint();
                outputPolygon.Points = newOp;
                newOp.Index = outputPolygon.Index;
                newOp.Point = point;
                newOp.Next = newOp;
                newOp.Prev = newOp;
                if (!outputPolygon.IsOpen)
                    SetHoleState(edge, outputPolygon);
                edge.OutIndex = outputPolygon.Index; //nb: do this after SetZ !
                return newOp;
            }
            else
            {
                OutputPolygon outputPolygon = _outputPolygons[edge.OutIndex];
                //OutputPolygon.Points is the 'left-most' point & OutputPolygon.Points.Prev is the 'right-most'
                OutputPoint op = outputPolygon.Points;
                bool ToFront = (edge.Side == EdgeSide.Left);
                if (ToFront && point == op.Point) return op;
                else if (!ToFront && point == op.Prev.Point) return op.Prev;

                var newOp = new OutputPoint
                {
                    Index = outputPolygon.Index,
                    Point = point,
                    Next = op,
                    Prev = op.Prev
                };
                newOp.Prev.Next = newOp;
                op.Prev = newOp;
                if (ToFront) outputPolygon.Points = newOp;
                return newOp;
            }
        }

        private OutputPoint GetLastOutPt(Edge edge)
        {
            OutputPolygon outputPolygon = _outputPolygons[edge.OutIndex];
            if (edge.Side == EdgeSide.Left)
                return outputPolygon.Points;
            else
                return outputPolygon.Points.Prev;
        }

        internal void SwapPoints(ref IntPoint point1, ref IntPoint point2)
        {
            IntPoint tmp = new IntPoint(point1);
            point1 = point2;
            point2 = tmp;
        }

        private static bool HorzSegmentsOverlap(long seg1a, long seg1b, long seg2a, long seg2b)
        {
            if (seg1a > seg1b) GeometryHelper.Swap(ref seg1a, ref seg1b);
            if (seg2a > seg2b) GeometryHelper.Swap(ref seg2a, ref seg2b);
            return (seg1a < seg2b) && (seg2a < seg1b);
        }

        private void SetHoleState(Edge edge, OutputPolygon outputPolygon)
        {
            Edge e2 = edge.PrevInAel;
            Edge eTmp = null;
            while (e2 != null)
            {
                if (e2.OutIndex >= 0 && e2.WindDelta != 0)
                {
                    if (eTmp == null)
                        eTmp = e2;
                    else if (eTmp.OutIndex == e2.OutIndex)
                        eTmp = null; //paired               
                }
                e2 = e2.PrevInAel;
            }

            if (eTmp == null)
            {
                outputPolygon.FirstLeft = null;
                outputPolygon.IsHole = false;
            }
            else
            {
                outputPolygon.FirstLeft = _outputPolygons[eTmp.OutIndex];
                outputPolygon.IsHole = !outputPolygon.FirstLeft.IsHole;
            }
        }

        private bool FirstIsBottomPt(OutputPoint btmPt1, OutputPoint btmPt2)
        {
            OutputPoint p = btmPt1.Prev;
            while ((p.Point == btmPt1.Point) && (p != btmPt1)) p = p.Prev;
            double dx1p = Math.Abs(GeometryHelper.GetDx(btmPt1.Point, p.Point));
            p = btmPt1.Next;
            while ((p.Point == btmPt1.Point) && (p != btmPt1)) p = p.Next;
            double dx1n = Math.Abs(GeometryHelper.GetDx(btmPt1.Point, p.Point));

            p = btmPt2.Prev;
            while ((p.Point == btmPt2.Point) && (p != btmPt2)) p = p.Prev;
            double dx2p = Math.Abs(GeometryHelper.GetDx(btmPt2.Point, p.Point));
            p = btmPt2.Next;
            while ((p.Point == btmPt2.Point) && (p != btmPt2)) p = p.Next;
            double dx2n = Math.Abs(GeometryHelper.GetDx(btmPt2.Point, p.Point));

            if (Math.Max(dx1p, dx1n) == Math.Max(dx2p, dx2n) &&
              Math.Min(dx1p, dx1n) == Math.Min(dx2p, dx2n))
                return Area(btmPt1) > 0; //if otherwise identical use orientation
            else
                return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
        }

        private OutputPoint GetBottomPt(OutputPoint pp)
        {
            OutputPoint dups = null;
            OutputPoint p = pp.Next;
            while (p != pp)
            {
                if (p.Point.Y > pp.Point.Y)
                {
                    pp = p;
                    dups = null;
                }
                else if (p.Point.Y == pp.Point.Y && p.Point.X <= pp.Point.X)
                {
                    if (p.Point.X < pp.Point.X)
                    {
                        dups = null;
                        pp = p;
                    }
                    else
                    {
                        if (p.Next != pp && p.Prev != pp) dups = p;
                    }
                }
                p = p.Next;
            }
            if (dups != null)
            {
                //there appears to be at least 2 vertices at bottomPt so ...
                while (dups != p)
                {
                    if (!FirstIsBottomPt(p, dups)) pp = dups;
                    dups = dups.Next;
                    while (dups.Point != pp.Point) dups = dups.Next;
                }
            }
            return pp;
        }

        private OutputPolygon GetLowermostRec(OutputPolygon outPolygon1, OutputPolygon outPolygon2)
        {
            //work out which polygon fragment has the correct hole state ...
            if (outPolygon1.BottomPoint == null)
                outPolygon1.BottomPoint = GetBottomPt(outPolygon1.Points);
            if (outPolygon2.BottomPoint == null)
                outPolygon2.BottomPoint = GetBottomPt(outPolygon2.Points);
            OutputPoint bPt1 = outPolygon1.BottomPoint;
            OutputPoint bPt2 = outPolygon2.BottomPoint;
            if (bPt1.Point.Y > bPt2.Point.Y) return outPolygon1;
            else if (bPt1.Point.Y < bPt2.Point.Y) return outPolygon2;
            else if (bPt1.Point.X < bPt2.Point.X) return outPolygon1;
            else if (bPt1.Point.X > bPt2.Point.X) return outPolygon2;
            else if (bPt1.Next == bPt1) return outPolygon2;
            else if (bPt2.Next == bPt2) return outPolygon1;
            else if (FirstIsBottomPt(bPt1, bPt2)) return outPolygon1;
            else return outPolygon2;
        }

        private static bool OutRec1RightOfOutRec2(OutputPolygon outPolygon1, OutputPolygon outPolygon2)
        {
            do
            {
                outPolygon1 = outPolygon1.FirstLeft;
                if (outPolygon1 == outPolygon2) return true;
            } while (outPolygon1 != null);
            return false;
        }

        private OutputPolygon GetOutPolygon(int index)
        {
            OutputPolygon outrec = _outputPolygons[index];
            while (outrec != _outputPolygons[outrec.Index])
                outrec = _outputPolygons[outrec.Index];
            return outrec;
        }

        private void AppendPolygon(Edge edge1, Edge edge2)
        {
            OutputPolygon outRec1 = _outputPolygons[edge1.OutIndex];
            OutputPolygon outRec2 = _outputPolygons[edge2.OutIndex];

            OutputPolygon holeStateRec;
            if (OutRec1RightOfOutRec2(outRec1, outRec2))
                holeStateRec = outRec2;
            else if (OutRec1RightOfOutRec2(outRec2, outRec1))
                holeStateRec = outRec1;
            else
                holeStateRec = GetLowermostRec(outRec1, outRec2);

            //get the start and ends of both output polygons and
            //join E2 poly onto E1 poly and delete pointers to E2 ...
            OutputPoint p1_lft = outRec1.Points;
            OutputPoint p1_rt = p1_lft.Prev;
            OutputPoint p2_lft = outRec2.Points;
            OutputPoint p2_rt = p2_lft.Prev;

            //join edge2 poly onto edge1 poly and delete pointers to edge2 ...
            if (edge1.Side == EdgeSide.Left)
            {
                if (edge2.Side == EdgeSide.Left)
                {
                    //z y x a b c
                    ReverseLinks(p2_lft);
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    outRec1.Points = p2_rt;
                }
                else
                {
                    //x y z a b c
                    p2_rt.Next = p1_lft;
                    p1_lft.Prev = p2_rt;
                    p2_lft.Prev = p1_rt;
                    p1_rt.Next = p2_lft;
                    outRec1.Points = p2_lft;
                }
            }
            else
            {
                if (edge2.Side == EdgeSide.Right)
                {
                    //a b c z y x
                    ReverseLinks(p2_lft);
                    p1_rt.Next = p2_rt;
                    p2_rt.Prev = p1_rt;
                    p2_lft.Next = p1_lft;
                    p1_lft.Prev = p2_lft;
                }
                else
                {
                    //a b c x y z
                    p1_rt.Next = p2_lft;
                    p2_lft.Prev = p1_rt;
                    p1_lft.Prev = p2_rt;
                    p2_rt.Next = p1_lft;
                }
            }

            outRec1.BottomPoint = null;
            if (holeStateRec == outRec2)
            {
                if (outRec2.FirstLeft != outRec1)
                    outRec1.FirstLeft = outRec2.FirstLeft;
                outRec1.IsHole = outRec2.IsHole;
            }
            outRec2.Points = null;
            outRec2.BottomPoint = null;

            outRec2.FirstLeft = outRec1;

            int OKIdx = edge1.OutIndex;
            int ObsoleteIdx = edge2.OutIndex;

            edge1.OutIndex = ClippingHelper.Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
            edge2.OutIndex = ClippingHelper.Unassigned;

            Edge e = _activeEdges;
            while (e != null)
            {
                if (e.OutIndex == ObsoleteIdx)
                {
                    e.OutIndex = OKIdx;
                    e.Side = edge1.Side;
                    break;
                }
                e = e.NextInAel;
            }
            outRec2.Index = outRec1.Index;
        }

        private static void ReverseLinks(OutputPoint point)
        {
            if (point == null) return;
            OutputPoint pp1;
            OutputPoint pp2;
            pp1 = point;
            do
            {
                pp2 = pp1.Next;
                pp1.Next = pp1.Prev;
                pp1.Prev = pp2;
                pp1 = pp2;
            } while (pp1 != point);
        }

        private static void SwapSides(Edge edge1, Edge edge2)
        {
            EdgeSide side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }

        private static void SwapPolyIndexes(Edge edge1, Edge edge2)
        {
            int outIdx = edge1.OutIndex;
            edge1.OutIndex = edge2.OutIndex;
            edge2.OutIndex = outIdx;
        }

        private void IntersectEdges(Edge edge1, Edge edge2, IntPoint pt)
        {
            //edge1 will be to the left of edge2 BELOW the intersection. Therefore edge1 is before
            //edge2 in AEL except when edge1 is being inserted at the intersection point ...

            bool e1Contributing = (edge1.OutIndex >= 0);
            bool e2Contributing = (edge2.OutIndex >= 0);

#if use_lines
            //if either edge is on an OPEN path ...
            if (edge1.WindDelta == 0 || edge2.WindDelta == 0)
            {
                //ignore subject-subject open path intersections UNLESS they
                //are both open paths, AND they are both 'contributing maximas' ...
                if (edge1.WindDelta == 0 && edge2.WindDelta == 0) return;
                //if intersecting a subj line with a subj poly ...
                else if (edge1.Kind == edge2.Kind &&
                  edge1.WindDelta != edge2.WindDelta && _mClipOperation == ClipOperation.Union)
                {
                    if (edge1.WindDelta == 0)
                    {
                        if (e2Contributing)
                        {
                            AddOutPt(edge1, pt);
                            if (e1Contributing) edge1.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                    else
                    {
                        if (e1Contributing)
                        {
                            AddOutPt(edge2, pt);
                            if (e2Contributing) edge2.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                }
                else if (edge1.Kind != edge2.Kind)
                {
                    if ((edge1.WindDelta == 0) && Math.Abs(edge2.WindCount) == 1 &&
                      (_mClipOperation != ClipOperation.Union || edge2.WindCount2 == 0))
                    {
                        AddOutPt(edge1, pt);
                        if (e1Contributing) edge1.OutIndex = ClippingHelper.Unassigned;
                    }
                    else if ((edge2.WindDelta == 0) && (Math.Abs(edge1.WindCount) == 1) &&
                      (_mClipOperation != ClipOperation.Union || edge1.WindCount2 == 0))
                    {
                        AddOutPt(edge2, pt);
                        if (e2Contributing) edge2.OutIndex = ClippingHelper.Unassigned;
                    }
                }
                return;
            }
#endif

            //update winding counts...
            //assumes that edge1 will be to the right of edge2 ABOVE the intersection
            if (edge1.Kind == edge2.Kind)
            {
                if (IsEvenOddFillType(edge1))
                {
                    int oldE1WindCnt = edge1.WindCount;
                    edge1.WindCount = edge2.WindCount;
                    edge2.WindCount = oldE1WindCnt;
                }
                else
                {
                    if (edge1.WindCount + edge2.WindDelta == 0) edge1.WindCount = -edge1.WindCount;
                    else edge1.WindCount += edge2.WindDelta;
                    if (edge2.WindCount - edge1.WindDelta == 0) edge2.WindCount = -edge2.WindCount;
                    else edge2.WindCount -= edge1.WindDelta;
                }
            }
            else
            {
                if (!IsEvenOddFillType(edge2)) edge1.WindCount2 += edge2.WindDelta;
                else edge1.WindCount2 = (edge1.WindCount2 == 0) ? 1 : 0;
                if (!IsEvenOddFillType(edge1)) edge2.WindCount2 -= edge1.WindDelta;
                else edge2.WindCount2 = (edge2.WindCount2 == 0) ? 1 : 0;
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
                if ((e1Wc != 0 && e1Wc != 1) || (e2Wc != 0 && e2Wc != 1) ||
                  (edge1.Kind != edge2.Kind && _mClipOperation != ClipOperation.Xor))
                {
                    AddLocalMaxPoly(edge1, edge2, pt);
                }
                else
                {
                    AddOutPt(edge1, pt);
                    AddOutPt(edge2, pt);
                    SwapSides(edge1, edge2);
                    SwapPolyIndexes(edge1, edge2);
                }
            }
            else if (e1Contributing)
            {
                if (e2Wc == 0 || e2Wc == 1)
                {
                    AddOutPt(edge1, pt);
                    SwapSides(edge1, edge2);
                    SwapPolyIndexes(edge1, edge2);
                }

            }
            else if (e2Contributing)
            {
                if (e1Wc == 0 || e1Wc == 1)
                {
                    AddOutPt(edge2, pt);
                    SwapSides(edge1, edge2);
                    SwapPolyIndexes(edge1, edge2);
                }
            }
            else if ((e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
            {
                //neither edge is currently contributing ...
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
                    switch (_mClipOperation)
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
                else
                    SwapSides(edge1, edge2);
            }
        }

        private void ProcessHorizontals()
        {
            //_sortedEdges;
            while (PopEdgeFromSel(out var horzEdge))
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
            EdgeDirection dir;
            long horzLeft, horzRight;
            bool IsOpen = horzEdge.WindDelta == 0;

            GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

            Edge eLastHorz = horzEdge, eMaxPair = null;
            while (eLastHorz.NextInLml != null && eLastHorz.NextInLml.IsHorizontal)
                eLastHorz = eLastHorz.NextInLml;
            if (eLastHorz.NextInLml == null)
                eMaxPair = GetMaximaPair(eLastHorz);

            Maxima currMax = _maxima;
            if (currMax != null)
            {
                //get the first maxima in range (X) ...
                if (dir == EdgeDirection.LeftToRight)
                {
                    while (currMax != null && currMax.X <= horzEdge.Bottom.X)
                        currMax = currMax.Next;
                    if (currMax != null && currMax.X >= eLastHorz.Top.X)
                        currMax = null;
                }
                else
                {
                    while (currMax.Next != null && currMax.Next.X < horzEdge.Bottom.X)
                        currMax = currMax.Next;
                    if (currMax.X <= eLastHorz.Top.X) currMax = null;
                }
            }

            OutputPoint op1 = null;
            for (;;) //loop through consec. horizontal edges
            {
                bool IsLastHorz = (horzEdge == eLastHorz);
                Edge e = GetNextInAel(horzEdge, dir);
                while (e != null)
                {

                    //this code block inserts extra coords into horizontal edges (in output
                    //polygons) whereever maxima touch these horizontal edges. This helps
                    //'simplifying' polygons (ie if the Simplify property is set).
                    if (currMax != null)
                    {
                        if (dir == EdgeDirection.LeftToRight)
                        {
                            while (currMax != null && currMax.X < e.Current.X)
                            {
                                if (horzEdge.OutIndex >= 0 && !IsOpen)
                                    AddOutPt(horzEdge, new IntPoint(currMax.X, horzEdge.Bottom.Y));
                                currMax = currMax.Next;
                            }
                        }
                        else
                        {
                            while (currMax != null && currMax.X > e.Current.X)
                            {
                                if (horzEdge.OutIndex >= 0 && !IsOpen)
                                    AddOutPt(horzEdge, new IntPoint(currMax.X, horzEdge.Bottom.Y));
                                currMax = currMax.Prev;
                            }
                        }
                    };

                    if ((dir == EdgeDirection.LeftToRight && e.Current.X > horzRight) ||
                      (dir == EdgeDirection.RightToLeft && e.Current.X < horzLeft)) break;

                    //Also break if we've got to the end of an intermediate horizontal edge ...
                    //nb: Smaller Dx's are to the right of larger Dx's ABOVE the horizontal.
                    if (e.Current.X == horzEdge.Top.X && horzEdge.NextInLml != null &&
                      e.Dx < horzEdge.NextInLml.Dx) break;

                    if (horzEdge.OutIndex >= 0 && !IsOpen)  //note: may be done multiple times
                    {
                        op1 = AddOutPt(horzEdge, e.Current);
                        Edge eNextHorz = _sortedEdges;
                        while (eNextHorz != null)
                        {
                            if (eNextHorz.OutIndex >= 0 &&
                              HorzSegmentsOverlap(horzEdge.Bottom.X,
                              horzEdge.Top.X, eNextHorz.Bottom.X, eNextHorz.Top.X))
                            {
                                OutputPoint op2 = GetLastOutPt(eNextHorz);
                                AddJoin(op2, op1, eNextHorz.Top);
                            }
                            eNextHorz = eNextHorz.NextInSel;
                        }
                        AddGhostJoin(op1, horzEdge.Bottom);
                    }

                    //OK, so far we're still in range of the horizontal Edge  but make sure
                    //we're at the last of consec. horizontals when matching with eMaxPair
                    if (e == eMaxPair && IsLastHorz)
                    {
                        if (horzEdge.OutIndex >= 0)
                            AddLocalMaxPoly(horzEdge, eMaxPair, horzEdge.Top);
                        DeleteFromAel(horzEdge);
                        DeleteFromAel(eMaxPair);
                        return;
                    }

                    if (dir == EdgeDirection.LeftToRight)
                    {
                        IntPoint Pt = new IntPoint(e.Current.X, horzEdge.Current.Y);
                        IntersectEdges(horzEdge, e, Pt);
                    }
                    else
                    {
                        IntPoint Pt = new IntPoint(e.Current.X, horzEdge.Current.Y);
                        IntersectEdges(e, horzEdge, Pt);
                    }
                    Edge eNext = GetNextInAel(e, dir);
                    SwapPositionsInAel(horzEdge, e);
                    e = eNext;
                } //end while(edge != null)

                //Break out of loop if HorzEdge.NextInLML is not also horizontal ...
                if (horzEdge.NextInLml == null || !horzEdge.NextInLml.IsHorizontal) break;

                UpdateEdgeIntoAel(ref horzEdge);
                if (horzEdge.OutIndex >= 0) AddOutPt(horzEdge, horzEdge.Bottom);
                GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

            } //end for (;;)

            if (horzEdge.OutIndex >= 0 && op1 == null)
            {
                op1 = GetLastOutPt(horzEdge);
                Edge eNextHorz = _sortedEdges;
                while (eNextHorz != null)
                {
                    if (eNextHorz.OutIndex >= 0 &&
                      HorzSegmentsOverlap(horzEdge.Bottom.X,
                      horzEdge.Top.X, eNextHorz.Bottom.X, eNextHorz.Top.X))
                    {
                        OutputPoint op2 = GetLastOutPt(eNextHorz);
                        AddJoin(op2, op1, eNextHorz.Top);
                    }
                    eNextHorz = eNextHorz.NextInSel;
                }
                AddGhostJoin(op1, horzEdge.Top);
            }

            if (horzEdge.NextInLml != null)
            {
                if (horzEdge.OutIndex >= 0)
                {
                    op1 = AddOutPt(horzEdge, horzEdge.Top);

                    UpdateEdgeIntoAel(ref horzEdge);
                    if (horzEdge.WindDelta == 0) return;
                    //nb: HorzEdge is no longer horizontal here
                    Edge ePrev = horzEdge.PrevInAel;
                    Edge eNext = horzEdge.NextInAel;
                    if (ePrev != null && ePrev.Current.X == horzEdge.Bottom.X &&
                      ePrev.Current.Y == horzEdge.Bottom.Y && ePrev.WindDelta != 0 &&
                      (ePrev.OutIndex >= 0 && ePrev.Current.Y > ePrev.Top.Y && GeometryHelper.SlopesEqual(horzEdge, ePrev, _useFullRange)))
                    {
                        OutputPoint op2 = AddOutPt(ePrev, horzEdge.Bottom);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                    else if (eNext != null && eNext.Current.X == horzEdge.Bottom.X &&
                      eNext.Current.Y == horzEdge.Bottom.Y && eNext.WindDelta != 0 &&
                      eNext.OutIndex >= 0 && eNext.Current.Y > eNext.Top.Y && GeometryHelper.SlopesEqual(horzEdge, eNext, _useFullRange))
                    {
                        OutputPoint op2 = AddOutPt(eNext, horzEdge.Bottom);
                        AddJoin(op1, op2, horzEdge.Top);
                    }
                }
                else
                    UpdateEdgeIntoAel(ref horzEdge);
            }
            else
            {
                if (horzEdge.OutIndex >= 0) AddOutPt(horzEdge, horzEdge.Top);
                DeleteFromAel(horzEdge);
            }
        }

        private static Edge GetNextInAel(Edge edge, EdgeDirection edgeDirection)
        {
            return edgeDirection == EdgeDirection.LeftToRight ? edge.NextInAel : edge.PrevInAel;
        }

        internal bool IsMinima(Edge edge)
        {
            return edge != null && (edge.Prev.NextInLml != edge) && (edge.Next.NextInLml != edge);
        }

        internal static bool IsMaxima(Edge edge, double y)
        {
            return (edge != null && edge.Top.Y == y && edge.NextInLml == null);
        }

        public static bool IsIntermediate(Edge edge, double y)
        {
            return (edge.Top.Y == y && edge.NextInLml != null);
        }

        internal Edge GetMaximaPair(Edge edge)
        {
            if ((edge.Next.Top == edge.Top) && edge.Next.NextInLml == null)
                return edge.Next;
            else if ((edge.Prev.Top == edge.Top) && edge.Prev.NextInLml == null)
                return edge.Prev;
            else
                return null;
        }

        internal Edge GetMaximaPairEx(Edge e)
        {
            //as above but returns null if MaxPair isn't in AEL (unless it's horizontal)
            Edge result = GetMaximaPair(e);
            if (result == null || result.OutIndex == ClippingHelper.Skip ||
              ((result.NextInAel == result.PrevInAel) && !result.IsHorizontal)) return null;
            return result;
        }

        private bool ProcessIntersections(long topY)
        {
            if (_activeEdges == null) return true;
            try
            {
                BuildIntersectList(topY);
                if (_intersectList.Count == 0) return true;
                if (_intersectList.Count == 1 || FixupIntersectionOrder())
                    ProcessIntersectList();
                else
                    return false;
            }
            catch
            {
                _sortedEdges = null;
                _intersectList.Clear();
                throw new Exception("ProcessIntersections error");
            }
            _sortedEdges = null;
            return true;
        }

        private void BuildIntersectList(long topY)
        {
            if (_activeEdges == null) return;

            //prepare for sorting ...
            Edge e = _activeEdges;
            _sortedEdges = e;
            while (e != null)
            {
                e.PrevInSel = e.PrevInAel;
                e.NextInSel = e.NextInAel;
                e.Current.X = TopX(e, topY);
                e = e.NextInAel;
            }

            //bubblesort ...
            bool isModified = true;
            while (isModified && _sortedEdges != null)
            {
                isModified = false;
                e = _sortedEdges;
                while (e.NextInSel != null)
                {
                    Edge eNext = e.NextInSel;
                    IntPoint pt;
                    if (e.Current.X > eNext.Current.X)
                    {
                        IntersectPoint(e, eNext, out pt);
                        if (pt.Y < topY)
                            pt = new IntPoint(TopX(e, topY), topY);
                        IntersectNode newNode = new IntersectNode();
                        newNode.Edge1 = e;
                        newNode.Edge2 = eNext;
                        newNode.Point = pt;
                        _intersectList.Add(newNode);

                        SwapPositionsInSel(e, eNext);
                        isModified = true;
                    }
                    else
                        e = eNext;
                }
                if (e.PrevInSel != null) e.PrevInSel.NextInSel = null;
                else break;
            }
            _sortedEdges = null;
        }

        private static bool EdgesAdjacent(IntersectNode node)
        {
            return node.Edge1.NextInSel == node.Edge2 || node.Edge1.PrevInSel == node.Edge2;
        }

        private bool FixupIntersectionOrder()
        {
            //pre-condition: intersections are sorted bottom-most first.
            //Now it's crucial that intersections are made only between adjacent edges,
            //so to ensure this the order of intersections may need adjusting ...
            _intersectList.Sort();

            CopyAeltoSel();
            int cnt = _intersectList.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (!EdgesAdjacent(_intersectList[i]))
                {
                    int j = i + 1;
                    while (j < cnt && !EdgesAdjacent(_intersectList[j])) j++;
                    if (j == cnt) return false;

                    IntersectNode tmp = _intersectList[i];
                    _intersectList[i] = _intersectList[j];
                    _intersectList[j] = tmp;

                }
                SwapPositionsInSel(_intersectList[i].Edge1, _intersectList[i].Edge2);
            }
            return true;
        }

        private void ProcessIntersectList()
        {
            for (int i = 0; i < _intersectList.Count; i++)
            {
                IntersectNode iNode = _intersectList[i];
                {
                    IntersectEdges(iNode.Edge1, iNode.Edge2, iNode.Point);
                    SwapPositionsInAel(iNode.Edge1, iNode.Edge2);
                }
            }
            _intersectList.Clear();
        }

        private static long TopX(Edge edge, long currentY)
        {
            if (currentY == edge.Top.Y)
                return edge.Top.X;
            return edge.Bottom.X + (edge.Dx * (currentY - edge.Bottom.Y)).RoundToLong();
        }

        private static void IntersectPoint(Edge edge1, Edge edge2, out IntPoint point)
        {
            point = new IntPoint();
            double b1, b2;
            //nb: with very large coordinate values, it's possible for SlopesEqual() to 
            //return false but for the edge.Dx value be equal due to double precision rounding.
            if (edge1.Dx == edge2.Dx)
            {
                point.Y = edge1.Current.Y;
                point.X = TopX(edge1, point.Y);
                return;
            }

            if (edge1.Delta.X == 0)
            {
                point.X = edge1.Bottom.X;
                if (edge2.IsHorizontal)
                {
                    point.Y = edge2.Bottom.Y;
                }
                else
                {
                    b2 = edge2.Bottom.Y - (edge2.Bottom.X / edge2.Dx);
                    point.Y = GeometryHelper.RoundToLong(point.X / edge2.Dx + b2);
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
                    point.Y = GeometryHelper.RoundToLong(point.X / edge1.Dx + b1);
                }
            }
            else
            {
                b1 = edge1.Bottom.X - edge1.Bottom.Y * edge1.Dx;
                b2 = edge2.Bottom.X - edge2.Bottom.Y * edge2.Dx;
                double q = (b2 - b1) / (edge1.Dx - edge2.Dx);
                point.Y = GeometryHelper.RoundToLong(q);
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    point.X = GeometryHelper.RoundToLong(edge1.Dx * q + b1);
                else
                    point.X = GeometryHelper.RoundToLong(edge2.Dx * q + b2);
            }

            if (point.Y < edge1.Top.Y || point.Y < edge2.Top.Y)
            {
                if (edge1.Top.Y > edge2.Top.Y)
                    point.Y = edge1.Top.Y;
                else
                    point.Y = edge2.Top.Y;
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    point.X = TopX(edge1, point.Y);
                else
                    point.X = TopX(edge2, point.Y);
            }
            //finally, don't allow 'point' to be BELOW curr.Y (ie bottom of scanbeam) ...
            if (point.Y > edge1.Current.Y)
            {
                point.Y = edge1.Current.Y;
                //better to use the more vertical edge to derive X ...
                if (Math.Abs(edge1.Dx) > Math.Abs(edge2.Dx))
                    point.X = TopX(edge2, point.Y);
                else
                    point.X = TopX(edge1, point.Y);
            }
        }

        private void ProcessEdgesAtTopOfScanbeam(long topY)
        {
            Edge e = _activeEdges;
            while (e != null)
            {
                //1. process maxima, treating them as if they're 'bent' horizontal edges,
                //   but exclude maxima with horizontal edges. nb: edge can't be a horizontal.
                var isMaximaEdge = IsMaxima(e, topY);

                if (isMaximaEdge)
                {
                    Edge eMaxPair = GetMaximaPairEx(e);
                    isMaximaEdge = (eMaxPair == null || !eMaxPair.IsHorizontal);
                }

                if (isMaximaEdge)
                {
                    if (StrictlySimple) InsertMaxima(e.Top.X);
                    Edge ePrev = e.PrevInAel;
                    DoMaxima(e);
                    e = ePrev == null ? _activeEdges : ePrev.NextInAel;
                }
                else
                {
                    //2. promote horizontal edges, otherwise update Current.X and Current.Y ...
                    if (IsIntermediate(e, topY) && e.NextInLml.IsHorizontal)
                    {
                        UpdateEdgeIntoAel(ref e);
                        if (e.OutIndex >= 0)
                        {
                            AddOutPt(e, e.Bottom);
                        }
                        AddEdgeToSel(e);
                    }
                    else
                    {
                        e.Current.X = TopX(e, topY);
                        e.Current.Y = topY;
                    }

                    //When StrictlySimple and 'edge' is being touched by another edge, then
                    //make sure both edges have a vertex here ...
                    if (StrictlySimple)
                    {
                        Edge ePrev = e.PrevInAel;
                        if ((e.OutIndex >= 0) && (e.WindDelta != 0) && ePrev != null &&
                          (ePrev.OutIndex >= 0) && (ePrev.Current.X == e.Current.X) &&
                          (ePrev.WindDelta != 0))
                        {
                            IntPoint ip = new IntPoint(e.Current);
                            OutputPoint op = AddOutPt(ePrev, ip);
                            OutputPoint op2 = AddOutPt(e, ip);
                            AddJoin(op, op2, ip); //StrictlySimple (type-3) join
                        }
                    }

                    e = e.NextInAel;
                }
            }

            //3. Process horizontals at the Top of the scanbeam ...
            ProcessHorizontals();
            _maxima = null;

            //4. Promote intermediate vertices ...
            e = _activeEdges;
            while (e != null)
            {
                if (IsIntermediate(e, topY))
                {
                    OutputPoint op = null;
                    if (e.OutIndex >= 0)
                        op = AddOutPt(e, e.Top);
                    UpdateEdgeIntoAel(ref e);

                    //if output polygons share an edge, they'll need joining later ...
                    Edge ePrev = e.PrevInAel;
                    Edge eNext = e.NextInAel;
                    if (ePrev != null && ePrev.Current.X == e.Bottom.X &&
                      ePrev.Current.Y == e.Bottom.Y && op != null &&
                      ePrev.OutIndex >= 0 && ePrev.Current.Y > ePrev.Top.Y && GeometryHelper.SlopesEqual(e.Current, e.Top, ePrev.Current, ePrev.Top, _useFullRange) &&
                      (e.WindDelta != 0) && (ePrev.WindDelta != 0))
                    {
                        OutputPoint op2 = AddOutPt(ePrev, e.Bottom);
                        AddJoin(op, op2, e.Top);
                    }
                    else if (eNext != null && eNext.Current.X == e.Bottom.X &&
                      eNext.Current.Y == e.Bottom.Y && op != null &&
                      eNext.OutIndex >= 0 && eNext.Current.Y > eNext.Top.Y && GeometryHelper.SlopesEqual(e.Current, e.Top, eNext.Current, eNext.Top, _useFullRange) &&
                      (e.WindDelta != 0) && (eNext.WindDelta != 0))
                    {
                        OutputPoint op2 = AddOutPt(eNext, e.Bottom);
                        AddJoin(op, op2, e.Top);
                    }
                }
                e = e.NextInAel;
            }
        }

        private void DoMaxima(Edge edge)
        {
            Edge maxPair = GetMaximaPairEx(edge);
            if (maxPair == null)
            {
                if (edge.OutIndex >= 0)
                    AddOutPt(edge, edge.Top);
                DeleteFromAel(edge);
                return;
            }

            Edge eNext = edge.NextInAel;
            while (eNext != null && eNext != maxPair)
            {
                IntersectEdges(edge, eNext, edge.Top);
                SwapPositionsInAel(edge, eNext);
                eNext = edge.NextInAel;
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
                    AddOutPt(edge, edge.Top);
                    edge.OutIndex = ClippingHelper.Unassigned;
                }
                DeleteFromAel(edge);

                if (maxPair.OutIndex >= 0)
                {
                    AddOutPt(maxPair, edge.Top);
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

        public static void ReversePaths(PolygonPath polys)
        {
            foreach (var poly in polys) { poly.Reverse(); }
        }

        public static bool Orientation(Polygon poly)
        {
            return Area(poly) >= 0;
        }

        private int PointCount(OutputPoint pts)
        {
            if (pts == null) return 0;
            int result = 0;
            OutputPoint p = pts;
            do
            {
                result++;
                p = p.Next;
            }
            while (p != pts);
            return result;
        }

        private void BuildResult(PolygonPath polyg)
        {
            polyg.Clear();
            polyg.Capacity = _outputPolygons.Count;
            for (int i = 0; i < _outputPolygons.Count; i++)
            {
                OutputPolygon outputPolygon = _outputPolygons[i];
                if (outputPolygon.Points == null) continue;
                OutputPoint p = outputPolygon.Points.Prev;
                int cnt = PointCount(p);
                if (cnt < 2) continue;
                Polygon pg = new Polygon(cnt);
                for (int j = 0; j < cnt; j++)
                {
                    pg.Add(p.Point);
                    p = p.Prev;
                }
                polyg.Add(pg);
            }
        }

        private void BuildResult2(PolygonTree polytree)
        {
            polytree.Clear();

            //add each output polygon/contour to polytree ...
            polytree.AllPolygons.Capacity = _outputPolygons.Count;
            for (int i = 0; i < _outputPolygons.Count; i++)
            {
                OutputPolygon outputPolygon = _outputPolygons[i];
                int cnt = PointCount(outputPolygon.Points);
                if ((outputPolygon.IsOpen && cnt < 2) ||
                  (!outputPolygon.IsOpen && cnt < 3)) continue;
                FixHoleLinkage(outputPolygon);
                PolygonNode pn = new PolygonNode();
                polytree.AllPolygons.Add(pn);
                outputPolygon.PolygonNode = pn;
                pn.Polygon.Capacity = cnt;
                OutputPoint op = outputPolygon.Points.Prev;
                for (int j = 0; j < cnt; j++)
                {
                    pn.Polygon.Add(op.Point);
                    op = op.Prev;
                }
            }

            //fixup PolygonNode links etc ...
            polytree.Children.Capacity = _outputPolygons.Count;
            for (int i = 0; i < _outputPolygons.Count; i++)
            {
                OutputPolygon outputPolygon = _outputPolygons[i];
                if (outputPolygon.PolygonNode == null) continue;
                else if (outputPolygon.IsOpen)
                {
                    outputPolygon.PolygonNode.IsOpen = true;
                    polytree.AddChild(outputPolygon.PolygonNode);
                }
                else if (outputPolygon.FirstLeft != null &&
                  outputPolygon.FirstLeft.PolygonNode != null)
                    outputPolygon.FirstLeft.PolygonNode.AddChild(outputPolygon.PolygonNode);
                else
                    polytree.AddChild(outputPolygon.PolygonNode);
            }
        }

        private void FixupOutPolyline(OutputPolygon outrec)
        {
            OutputPoint pp = outrec.Points;
            OutputPoint lastPP = pp.Prev;
            while (pp != lastPP)
            {
                pp = pp.Next;
                if (pp.Point == pp.Prev.Point)
                {
                    if (pp == lastPP) lastPP = pp.Prev;
                    OutputPoint tmpPP = pp.Prev;
                    tmpPP.Next = pp.Next;
                    pp.Next.Prev = tmpPP;
                    pp = tmpPP;
                }
            }
            if (pp == pp.Prev) outrec.Points = null;
        }

        private void FixupOutPolygon(OutputPolygon outputPolygon)
        {
            //FixupOutPolygon() - removes duplicate points and simplifies consecutive
            //parallel edges by removing the middle vertex.
            OutputPoint lastOK = null;
            outputPolygon.BottomPoint = null;
            OutputPoint pp = outputPolygon.Points;
            bool preserveCol = PreserveCollinear || StrictlySimple;
            for (;;)
            {
                if (pp.Prev == pp || pp.Prev == pp.Next)
                {
                    outputPolygon.Points = null;
                    return;
                }
                //test for duplicate points and collinear edges ...
                if ((pp.Point == pp.Next.Point) || (pp.Point == pp.Prev.Point) ||
                  (GeometryHelper.SlopesEqual(pp.Prev.Point, pp.Point, pp.Next.Point, _useFullRange) &&
                  (!preserveCol || !Pt2IsBetweenPt1AndPt3(pp.Prev.Point, pp.Point, pp.Next.Point))))
                {
                    lastOK = null;
                    pp.Prev.Next = pp.Next;
                    pp.Next.Prev = pp.Prev;
                    pp = pp.Prev;
                }
                else if (pp == lastOK) break;
                else
                {
                    if (lastOK == null) lastOK = pp;
                    pp = pp.Next;
                }
            }
            outputPolygon.Points = pp;
        }

        OutputPoint DupOutPt(OutputPoint outputPoint, bool InsertAfter)
        {
            OutputPoint result = new OutputPoint();
            result.Point = outputPoint.Point;
            result.Index = outputPoint.Index;
            if (InsertAfter)
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

        private bool GetOverlap(long a1, long a2, long b1, long b2, out long left, out long right)
        {
            if (a1 < a2)
            {
                if (b1 < b2) { left = Math.Max(a1, b1); right = Math.Min(a2, b2); }
                else { left = Math.Max(a1, b2); right = Math.Min(a2, b1); }
            }
            else
            {
                if (b1 < b2) { left = Math.Max(a2, b1); right = Math.Min(a1, b2); }
                else { left = Math.Max(a2, b2); right = Math.Min(a1, b1); }
            }
            return left < right;
        }

        private bool JoinHorz(OutputPoint op1, OutputPoint op1b, OutputPoint op2, OutputPoint op2b,
          IntPoint point, bool discardLeft)
        {
            EdgeDirection Dir1 = (op1.Point.X > op1b.Point.X ?
              EdgeDirection.RightToLeft : EdgeDirection.LeftToRight);
            EdgeDirection Dir2 = (op2.Point.X > op2b.Point.X ?
              EdgeDirection.RightToLeft : EdgeDirection.LeftToRight);
            if (Dir1 == Dir2) return false;

            //When DiscardLeft, we want Op1b to be on the left of point1, otherwise we
            //want Op1b to be on the right. (And likewise with point2 and Op2b.)
            //So, to facilitate this while inserting Op1b and Op2b ...
            //when DiscardLeft, make sure we're AT or RIGHT of Point before adding Op1b,
            //otherwise make sure we're AT or LEFT of Point. (Likewise with Op2b.)
            if (Dir1 == EdgeDirection.LeftToRight)
            {
                while (op1.Next.Point.X <= point.X &&
                  op1.Next.Point.X >= op1.Point.X && op1.Next.Point.Y == point.Y)
                    op1 = op1.Next;
                if (discardLeft && (op1.Point.X != point.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, !discardLeft);
                if (op1b.Point != point)
                {
                    op1 = op1b;
                    op1.Point = point;
                    op1b = DupOutPt(op1, !discardLeft);
                }
            }
            else
            {
                while (op1.Next.Point.X >= point.X &&
                  op1.Next.Point.X <= op1.Point.X && op1.Next.Point.Y == point.Y)
                    op1 = op1.Next;
                if (!discardLeft && (op1.Point.X != point.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, discardLeft);
                if (op1b.Point != point)
                {
                    op1 = op1b;
                    op1.Point = point;
                    op1b = DupOutPt(op1, discardLeft);
                }
            }

            if (Dir2 == EdgeDirection.LeftToRight)
            {
                while (op2.Next.Point.X <= point.X &&
                  op2.Next.Point.X >= op2.Point.X && op2.Next.Point.Y == point.Y)
                    op2 = op2.Next;
                if (discardLeft && (op2.Point.X != point.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, !discardLeft);
                if (op2b.Point != point)
                {
                    op2 = op2b;
                    op2.Point = point;
                    op2b = DupOutPt(op2, !discardLeft);
                };
            }
            else
            {
                while (op2.Next.Point.X >= point.X &&
                  op2.Next.Point.X <= op2.Point.X && op2.Next.Point.Y == point.Y)
                    op2 = op2.Next;
                if (!discardLeft && (op2.Point.X != point.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, discardLeft);
                if (op2b.Point != point)
                {
                    op2 = op2b;
                    op2.Point = point;
                    op2b = DupOutPt(op2, discardLeft);
                };
            };

            if ((Dir1 == EdgeDirection.LeftToRight) == discardLeft)
            {
                op1.Prev = op2;
                op2.Next = op1;
                op1b.Next = op2b;
                op2b.Prev = op1b;
            }
            else
            {
                op1.Next = op2;
                op2.Prev = op1;
                op1b.Prev = op2b;
                op2b.Next = op1b;
            }
            return true;
        }

        private bool JoinPoints(Join j, OutputPolygon outRec1, OutputPolygon outRec2)
        {
            OutputPoint op1 = j.OutPoint1, op1b;
            OutputPoint op2 = j.OutPoint2, op2b;

            //There are 3 kinds of joins for output polygons ...
            //1. Horizontal joins where Join.OutPoint1 & Join.OutPoint2 are vertices anywhere
            //along (horizontal) collinear edges (& Join.Offset is on the same horizontal).
            //2. Non-horizontal joins where Join.OutPoint1 & Join.OutPoint2 are at the same
            //location at the Bottom of the overlapping segment (& Join.Offset is above).
            //3. StrictlySimple joins where edges touch but are not collinear and where
            //Join.OutPoint1, Join.OutPoint2 & Join.Offset all share the same point.
            bool isHorizontal = (j.OutPoint1.Point.Y == j.Offset.Y);

            if (isHorizontal && (j.Offset == j.OutPoint1.Point) && (j.Offset == j.OutPoint2.Point))
            {
                //Strictly Simple join ...
                if (outRec1 != outRec2) return false;
                op1b = j.OutPoint1.Next;
                while (op1b != op1 && (op1b.Point == j.Offset))
                    op1b = op1b.Next;
                bool reverse1 = (op1b.Point.Y > j.Offset.Y);
                op2b = j.OutPoint2.Next;
                while (op2b != op2 && (op2b.Point == j.Offset))
                    op2b = op2b.Next;
                bool reverse2 = (op2b.Point.Y > j.Offset.Y);
                if (reverse1 == reverse2) return false;
                if (reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPoint1 = op1;
                    j.OutPoint2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPoint1 = op1;
                    j.OutPoint2 = op1b;
                    return true;
                }
            }
            else if (isHorizontal)
            {
                //treat horizontal joins differently to non-horizontal joins since with
                //them we're not yet sure where the overlapping is. OutPoint1.Point & OutPoint2.Point
                //may be anywhere along the horizontal edge.
                op1b = op1;
                while (op1.Prev.Point.Y == op1.Point.Y && op1.Prev != op1b && op1.Prev != op2)
                    op1 = op1.Prev;
                while (op1b.Next.Point.Y == op1b.Point.Y && op1b.Next != op1 && op1b.Next != op2)
                    op1b = op1b.Next;
                if (op1b.Next == op1 || op1b.Next == op2) return false; //a flat 'polygon'

                op2b = op2;
                while (op2.Prev.Point.Y == op2.Point.Y && op2.Prev != op2b && op2.Prev != op1b)
                    op2 = op2.Prev;
                while (op2b.Next.Point.Y == op2b.Point.Y && op2b.Next != op2 && op2b.Next != op1)
                    op2b = op2b.Next;
                if (op2b.Next == op2 || op2b.Next == op1) return false; //a flat 'polygon'

                long Left, Right;
                //point1 -. Op1b & point2 -. Op2b are the extremites of the horizontal edges
                if (!GetOverlap(op1.Point.X, op1b.Point.X, op2.Point.X, op2b.Point.X, out Left, out Right))
                    return false;

                //DiscardLeftSide: when overlapping edges are joined, a spike will created
                //which needs to be cleaned up. However, we don't want point1 or point2 caught up
                //on the discard Side as either may still be needed for other joins ...
                IntPoint Pt;
                bool DiscardLeftSide;
                if (op1.Point.X >= Left && op1.Point.X <= Right)
                {
                    Pt = op1.Point; DiscardLeftSide = (op1.Point.X > op1b.Point.X);
                }
                else if (op2.Point.X >= Left && op2.Point.X <= Right)
                {
                    Pt = op2.Point; DiscardLeftSide = (op2.Point.X > op2b.Point.X);
                }
                else if (op1b.Point.X >= Left && op1b.Point.X <= Right)
                {
                    Pt = op1b.Point; DiscardLeftSide = op1b.Point.X > op1.Point.X;
                }
                else
                {
                    Pt = op2b.Point; DiscardLeftSide = (op2b.Point.X > op2.Point.X);
                }
                j.OutPoint1 = op1;
                j.OutPoint2 = op2;
                return JoinHorz(op1, op1b, op2, op2b, Pt, DiscardLeftSide);
            }
            else
            {
                //nb: For non-horizontal joins ...
                //    1. Jr.OutPoint1.Point.Y == Jr.OutPoint2.Point.Y
                //    2. Jr.OutPoint1.Point > Jr.Offset.Y

                //make sure the polygons are correctly oriented ...
                op1b = op1.Next;
                while ((op1b.Point == op1.Point) && (op1b != op1)) op1b = op1b.Next;
                bool Reverse1 = ((op1b.Point.Y > op1.Point.Y) ||
                  !GeometryHelper.SlopesEqual(op1.Point, op1b.Point, j.Offset, _useFullRange));
                if (Reverse1)
                {
                    op1b = op1.Prev;
                    while ((op1b.Point == op1.Point) && (op1b != op1)) op1b = op1b.Prev;
                    if ((op1b.Point.Y > op1.Point.Y) ||
                      !GeometryHelper.SlopesEqual(op1.Point, op1b.Point, j.Offset, _useFullRange)) return false;
                };
                op2b = op2.Next;
                while ((op2b.Point == op2.Point) && (op2b != op2)) op2b = op2b.Next;
                bool Reverse2 = ((op2b.Point.Y > op2.Point.Y) ||
                  !GeometryHelper.SlopesEqual(op2.Point, op2b.Point, j.Offset, _useFullRange));
                if (Reverse2)
                {
                    op2b = op2.Prev;
                    while ((op2b.Point == op2.Point) && (op2b != op2)) op2b = op2b.Prev;
                    if ((op2b.Point.Y > op2.Point.Y) ||
                      !GeometryHelper.SlopesEqual(op2.Point, op2b.Point, j.Offset, _useFullRange)) return false;
                }

                if ((op1b == op1) || (op2b == op2) || (op1b == op2b) ||
                  ((outRec1 == outRec2) && (Reverse1 == Reverse2))) return false;

                if (Reverse1)
                {
                    op1b = DupOutPt(op1, false);
                    op2b = DupOutPt(op2, true);
                    op1.Prev = op2;
                    op2.Next = op1;
                    op1b.Next = op2b;
                    op2b.Prev = op1b;
                    j.OutPoint1 = op1;
                    j.OutPoint2 = op1b;
                    return true;
                }
                else
                {
                    op1b = DupOutPt(op1, true);
                    op2b = DupOutPt(op2, false);
                    op1.Next = op2;
                    op2.Prev = op1;
                    op1b.Prev = op2b;
                    op2b.Next = op1b;
                    j.OutPoint1 = op1;
                    j.OutPoint2 = op1b;
                    return true;
                }
            }
        }

        public static int PointInPolygon(IntPoint pt, Polygon path)
        {
            //returns 0 if false, +1 if true, -1 if point ON polygon boundary
            //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
            int result = 0, cnt = path.Count;
            if (cnt < 3) return 0;
            IntPoint ip = path[0];
            for (int i = 1; i <= cnt; ++i)
            {
                IntPoint ipNext = (i == cnt ? path[0] : path[i]);
                if (ipNext.Y == pt.Y)
                {
                    if ((ipNext.X == pt.X) || (ip.Y == pt.Y &&
                      ((ipNext.X > pt.X) == (ip.X < pt.X)))) return -1;
                }
                if ((ip.Y < pt.Y) != (ipNext.Y < pt.Y))
                {
                    if (ip.X >= pt.X)
                    {
                        if (ipNext.X > pt.X) result = 1 - result;
                        else
                        {
                            double d = (double)(ip.X - pt.X) * (ipNext.Y - pt.Y) -
                              (double)(ipNext.X - pt.X) * (ip.Y - pt.Y);
                            if (d == 0) return -1;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = 1 - result;
                        }
                    }
                    else
                    {
                        if (ipNext.X > pt.X)
                        {
                            double d = (double)(ip.X - pt.X) * (ipNext.Y - pt.Y) -
                              (double)(ipNext.X - pt.X) * (ip.Y - pt.Y);
                            if (d == 0) return -1;
                            else if ((d > 0) == (ipNext.Y > ip.Y)) result = 1 - result;
                        }
                    }
                }
                ip = ipNext;
            }
            return result;
        }

        private static int PointInPolygon(IntPoint pt, OutputPoint op)
        {
            //returns 0 if false, +1 if true, -1 if point ON polygon boundary
            int result = 0;
            OutputPoint startOp = op;
            long ptx = pt.X, pty = pt.Y;
            long poly0x = op.Point.X, poly0y = op.Point.Y;
            do
            {
                op = op.Next;
                long poly1x = op.Point.X, poly1y = op.Point.Y;

                if (poly1y == pty)
                {
                    if ((poly1x == ptx) || (poly0y == pty &&
                      ((poly1x > ptx) == (poly0x < ptx)))) return -1;
                }
                if ((poly0y < pty) != (poly1y < pty))
                {
                    if (poly0x >= ptx)
                    {
                        if (poly1x > ptx) result = 1 - result;
                        else
                        {
                            double d = (double)(poly0x - ptx) * (poly1y - pty) -
                              (double)(poly1x - ptx) * (poly0y - pty);
                            if (d == 0) return -1;
                            if ((d > 0) == (poly1y > poly0y)) result = 1 - result;
                        }
                    }
                    else
                    {
                        if (poly1x > ptx)
                        {
                            double d = (double)(poly0x - ptx) * (poly1y - pty) -
                              (double)(poly1x - ptx) * (poly0y - pty);
                            if (d == 0) return -1;
                            if ((d > 0) == (poly1y > poly0y)) result = 1 - result;
                        }
                    }
                }
                poly0x = poly1x; poly0y = poly1y;
            } while (startOp != op);
            return result;
        }

        private static bool Poly2ContainsPoly1(OutputPoint outPt1, OutputPoint outPt2)
        {
            OutputPoint op = outPt1;
            do
            {
                //nb: PointInPolygon returns 0 if false, +1 if true, -1 if point on polygon
                int res = PointInPolygon(op.Point, outPt2);
                if (res >= 0) return res > 0;
                op = op.Next;
            }
            while (op != outPt1);
            return true;
        }

        private void FixupFirstLefts1(OutputPolygon oldOutputPolygon, OutputPolygon newOutputPolygon)
        {
            foreach (OutputPolygon outRec in _outputPolygons)
            {
                OutputPolygon firstLeft = ParseFirstLeft(outRec.FirstLeft);
                if (outRec.Points != null && firstLeft == oldOutputPolygon)
                {
                    if (Poly2ContainsPoly1(outRec.Points, newOutputPolygon.Points))
                        outRec.FirstLeft = newOutputPolygon;
                }
            }
        }

        private void FixupFirstLefts2(OutputPolygon innerOutputPolygon, OutputPolygon outerOutputPolygon)
        {
            //A polygon has split into two such that one is now the inner of the other.
            //It's possible that these polygons now wrap around other polygons, so check
            //every polygon that's also contained by OuterOutRec's FirstLeft container
            //(including nil) to see if they've become inner to the new inner polygon ...
            OutputPolygon orfl = outerOutputPolygon.FirstLeft;
            foreach (OutputPolygon outRec in _outputPolygons)
            {
                if (outRec.Points == null || outRec == outerOutputPolygon || outRec == innerOutputPolygon)
                    continue;
                OutputPolygon firstLeft = ParseFirstLeft(outRec.FirstLeft);
                if (firstLeft != orfl && firstLeft != innerOutputPolygon && firstLeft != outerOutputPolygon)
                    continue;
                if (Poly2ContainsPoly1(outRec.Points, innerOutputPolygon.Points))
                    outRec.FirstLeft = innerOutputPolygon;
                else if (Poly2ContainsPoly1(outRec.Points, outerOutputPolygon.Points))
                    outRec.FirstLeft = outerOutputPolygon;
                else if (outRec.FirstLeft == innerOutputPolygon || outRec.FirstLeft == outerOutputPolygon)
                    outRec.FirstLeft = orfl;
            }
        }

        private void FixupFirstLefts3(OutputPolygon oldOutputPolygon, OutputPolygon newOutputPolygon)
        {
            //same as FixupFirstLefts1 but doesn't call Poly2ContainsPoly1()
            foreach (OutputPolygon outRec in _outputPolygons)
            {
                OutputPolygon firstLeft = ParseFirstLeft(outRec.FirstLeft);
                if (outRec.Points != null && firstLeft == oldOutputPolygon)
                    outRec.FirstLeft = newOutputPolygon;
            }
        }

        private static OutputPolygon ParseFirstLeft(OutputPolygon firstLeft)
        {
            while (firstLeft != null && firstLeft.Points == null)
                firstLeft = firstLeft.FirstLeft;
            return firstLeft;
        }

        private void JoinCommonEdges()
        {
            for (int i = 0; i < _joins.Count; i++)
            {
                Join join = _joins[i];

                OutputPolygon outRec1 = GetOutPolygon(join.OutPoint1.Index);
                OutputPolygon outRec2 = GetOutPolygon(join.OutPoint2.Index);

                if (outRec1.Points == null || outRec2.Points == null) continue;
                if (outRec1.IsOpen || outRec2.IsOpen) continue;

                //get the polygon fragment with the correct hole state (FirstLeft)
                //before calling JoinPoints() ...
                OutputPolygon holeStateRec;
                if (outRec1 == outRec2) holeStateRec = outRec1;
                else if (OutRec1RightOfOutRec2(outRec1, outRec2)) holeStateRec = outRec2;
                else if (OutRec1RightOfOutRec2(outRec2, outRec1)) holeStateRec = outRec1;
                else holeStateRec = GetLowermostRec(outRec1, outRec2);

                if (!JoinPoints(join, outRec1, outRec2)) continue;

                if (outRec1 == outRec2)
                {
                    //instead of joining two polygons, we've just created a new one by
                    //splitting one polygon into two.
                    outRec1.Points = join.OutPoint1;
                    outRec1.BottomPoint = null;
                    outRec2 = CreateOutRec();
                    outRec2.Points = join.OutPoint2;

                    //update all OutRec2.Points Index's ...
                    UpdateOutPtIdxs(outRec2);

                    if (Poly2ContainsPoly1(outRec2.Points, outRec1.Points))
                    {
                        //outPolygon1 contains outPolygon2 ...
                        outRec2.IsHole = !outRec1.IsHole;
                        outRec2.FirstLeft = outRec1;

                        if (_usingTreeSolution) FixupFirstLefts2(outRec2, outRec1);

                        if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                            ReverseLinks(outRec2.Points);

                    }
                    else if (Poly2ContainsPoly1(outRec1.Points, outRec2.Points))
                    {
                        //outPolygon2 contains outPolygon1 ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec1.IsHole = !outRec2.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;
                        outRec1.FirstLeft = outRec2;

                        if (_usingTreeSolution) FixupFirstLefts2(outRec1, outRec2);

                        if ((outRec1.IsHole ^ ReverseSolution) == (Area(outRec1) > 0))
                            ReverseLinks(outRec1.Points);
                    }
                    else
                    {
                        //the 2 polygons are completely separate ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;

                        //fixup FirstLeft pointers that may need reassigning to OutRec2
                        if (_usingTreeSolution) FixupFirstLefts1(outRec1, outRec2);
                    }

                }
                else
                {
                    //joined 2 polygons together ...

                    outRec2.Points = null;
                    outRec2.BottomPoint = null;
                    outRec2.Index = outRec1.Index;

                    outRec1.IsHole = holeStateRec.IsHole;
                    if (holeStateRec == outRec2)
                        outRec1.FirstLeft = outRec2.FirstLeft;
                    outRec2.FirstLeft = outRec1;

                    //fixup FirstLeft pointers that may need reassigning to OutRec1
                    if (_usingTreeSolution) FixupFirstLefts3(outRec2, outRec1);
                }
            }
        }

        private void UpdateOutPtIdxs(OutputPolygon outrec)
        {
            OutputPoint op = outrec.Points;
            do
            {
                op.Index = outrec.Index;
                op = op.Prev;
            }
            while (op != outrec.Points);
        }

        private void DoSimplePolygons()
        {
            int i = 0;
            while (i < _outputPolygons.Count)
            {
                OutputPolygon outrec = _outputPolygons[i++];
                OutputPoint op = outrec.Points;
                if (op == null || outrec.IsOpen) continue;
                do //for each Point in Polygon until duplicate found do ...
                {
                    OutputPoint op2 = op.Next;
                    while (op2 != outrec.Points)
                    {
                        if ((op.Point == op2.Point) && op2.Next != op && op2.Prev != op)
                        {
                            //split the polygon into two ...
                            OutputPoint op3 = op.Prev;
                            OutputPoint op4 = op2.Prev;
                            op.Prev = op4;
                            op4.Next = op;
                            op2.Prev = op3;
                            op3.Next = op2;

                            outrec.Points = op;
                            OutputPolygon outrec2 = CreateOutRec();
                            outrec2.Points = op2;
                            UpdateOutPtIdxs(outrec2);
                            if (Poly2ContainsPoly1(outrec2.Points, outrec.Points))
                            {
                                //OutRec2 is contained by OutRec1 ...
                                outrec2.IsHole = !outrec.IsHole;
                                outrec2.FirstLeft = outrec;
                                if (_usingTreeSolution) FixupFirstLefts2(outrec2, outrec);
                            }
                            else
                              if (Poly2ContainsPoly1(outrec.Points, outrec2.Points))
                            {
                                //OutRec1 is contained by OutRec2 ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec.IsHole = !outrec2.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                                outrec.FirstLeft = outrec2;
                                if (_usingTreeSolution) FixupFirstLefts2(outrec, outrec2);
                            }
                            else
                            {
                                //the 2 polygons are separate ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                                if (_usingTreeSolution) FixupFirstLefts1(outrec, outrec2);
                            }
                            op2 = op; //ie get ready for the next iteration
                        }
                        op2 = op2.Next;
                    }
                    op = op.Next;
                }
                while (op != outrec.Points);
            }
        }

        public static double Area(Polygon poly)
        {
            int cnt = (int)poly.Count;
            if (cnt < 3) return 0;
            double a = 0;
            for (int i = 0, j = cnt - 1; i < cnt; ++i)
            {
                a += ((double)poly[j].X + poly[i].X) * ((double)poly[j].Y - poly[i].Y);
                j = i;
            }
            return -a * 0.5;
        }

        internal double Area(OutputPolygon outputPolygon)
        {
            return Area(outputPolygon.Points);
        }

        internal double Area(OutputPoint op)
        {
            OutputPoint opFirst = op;
            if (op == null) return 0;
            double a = 0;
            do
            {
                a = a + (double)(op.Prev.Point.X + op.Point.X) * (double)(op.Prev.Point.Y - op.Point.Y);
                op = op.Next;
            } while (op != opFirst);
            return a * 0.5;
        }

        public static PolygonPath SimplifyPolygon(Polygon poly,
              PolygonFillType fillType = PolygonFillType.EvenOdd)
        {
            PolygonPath result = new PolygonPath();
            Clipper c = new Clipper();
            c.StrictlySimple = true;
            c.AddPath(poly, PolygonKind.Subject, true);
            c.Execute(ClipOperation.Union, result, fillType, fillType);
            return result;
        }

        public static PolygonPath SimplifyPolygons(PolygonPath polys,
            PolygonFillType fillType = PolygonFillType.EvenOdd)
        {
            PolygonPath result = new PolygonPath();
            Clipper c = new Clipper();
            c.StrictlySimple = true;
            c.AddPaths(polys, PolygonKind.Subject);
            c.Execute(ClipOperation.Union, result, fillType, fillType);
            return result;
        }

        private static double DistanceFromLineSqrd(IntPoint point, IntPoint linePoint1, IntPoint linePoint2)
        {
            //The equation of a line in general form (Ax + By + C = 0)
            //given 2 points (x¹,y¹) & (x²,y²) is ...
            //(y¹ - y²)x + (x² - x¹)y + (y² - y¹)x¹ - (x² - x¹)y¹ = 0
            //A = (y¹ - y²); B = (x² - x¹); C = (y² - y¹)x¹ - (x² - x¹)y¹
            //perpendicular distance of point (x³,y³) = (Ax³ + By³ + C)/Sqrt(A² + B²)
            //see http://en.wikipedia.org/wiki/Perpendicular_distance
            double A = linePoint1.Y - linePoint2.Y;
            double B = linePoint2.X - linePoint1.X;
            double C = A * linePoint1.X + B * linePoint1.Y;
            C = A * point.X + B * point.Y - C;
            return (C * C) / (A * A + B * B);
        }

        private static bool SlopesNearCollinear(
            IntPoint point1, IntPoint point2, IntPoint point3, double distanceSquared)
        {
            // this function is more accurate when the point that's GEOMETRICALLY 
            // between the other 2 points is the one that's tested for distance.  
            // nb: with 'spikes', either point1 or point3 is geometrically between the other pts                    
            if (Math.Abs(point1.X - point2.X) > Math.Abs(point1.Y - point2.Y))
            {
                if ((point1.X > point2.X) == (point1.X < point3.X))
                    return DistanceFromLineSqrd(point1, point2, point3) < distanceSquared;
                else if ((point2.X > point1.X) == (point2.X < point3.X))
                    return DistanceFromLineSqrd(point2, point1, point3) < distanceSquared;
                else
                    return DistanceFromLineSqrd(point3, point1, point2) < distanceSquared;
            }
            else
            {
                if ((point1.Y > point2.Y) == (point1.Y < point3.Y))
                    return DistanceFromLineSqrd(point1, point2, point3) < distanceSquared;
                else if ((point2.Y > point1.Y) == (point2.Y < point3.Y))
                    return DistanceFromLineSqrd(point2, point1, point3) < distanceSquared;
                else
                    return DistanceFromLineSqrd(point3, point1, point2) < distanceSquared;
            }
        }

        private static bool PointsAreClose(IntPoint pt1, IntPoint pt2, double distSqrd)
        {
            double dx = (double)pt1.X - pt2.X;
            double dy = (double)pt1.Y - pt2.Y;
            return ((dx * dx) + (dy * dy) <= distSqrd);
        }

        private static OutputPoint ExcludeOp(OutputPoint op)
        {
            OutputPoint result = op.Prev;
            result.Next = op.Next;
            op.Next.Prev = result;
            result.Index = 0;
            return result;
        }

        public static Polygon CleanPolygon(Polygon path, double distance = 1.415)
        {
            //distance = proximity in units/pixels below which vertices will be stripped. 
            //Default ~= sqrt(2) so when adjacent vertices or semi-adjacent vertices have 
            //both x & y coords within 1 unit, then the second vertex will be stripped.

            int cnt = path.Count;

            if (cnt == 0) return new Polygon();

            OutputPoint[] outputPoints = new OutputPoint[cnt];
            for (int i = 0; i < cnt; ++i) outputPoints[i] = new OutputPoint();

            for (int i = 0; i < cnt; ++i)
            {
                outputPoints[i].Point = path[i];
                outputPoints[i].Next = outputPoints[(i + 1) % cnt];
                outputPoints[i].Next.Prev = outputPoints[i];
                outputPoints[i].Index = 0;
            }

            double distSqrd = distance * distance;
            OutputPoint op = outputPoints[0];
            while (op.Index == 0 && op.Next != op.Prev)
            {
                if (PointsAreClose(op.Point, op.Prev.Point, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else if (PointsAreClose(op.Prev.Point, op.Next.Point, distSqrd))
                {
                    ExcludeOp(op.Next);
                    op = ExcludeOp(op);
                    cnt -= 2;
                }
                else if (SlopesNearCollinear(op.Prev.Point, op.Point, op.Next.Point, distSqrd))
                {
                    op = ExcludeOp(op);
                    cnt--;
                }
                else
                {
                    op.Index = 1;
                    op = op.Next;
                }
            }

            if (cnt < 3) cnt = 0;
            Polygon result = new Polygon(cnt);
            for (int i = 0; i < cnt; ++i)
            {
                result.Add(op.Point);
                op = op.Next;
            }
            outputPoints = null;
            return result;
        }

        public static PolygonPath CleanPolygons(PolygonPath polys,
            double distance = 1.415)
        {
            PolygonPath result = new PolygonPath(polys.Count);
            for (int i = 0; i < polys.Count; i++)
                result.Add(CleanPolygon(polys[i], distance));
            return result;
        }

        internal static PolygonPath Minkowski(Polygon pattern, Polygon path, bool isSum, bool isClosed)
        {
            int delta = (isClosed ? 1 : 0);
            int polyCnt = pattern.Count;
            int pathCnt = path.Count;
            PolygonPath result = new PolygonPath(pathCnt);
            if (isSum)
                for (int i = 0; i < pathCnt; i++)
                {
                    Polygon p = new Polygon(polyCnt);
                    foreach (IntPoint ip in pattern)
                        p.Add(new IntPoint(path[i].X + ip.X, path[i].Y + ip.Y));
                    result.Add(p);
                }
            else
                for (int i = 0; i < pathCnt; i++)
                {
                    Polygon p = new Polygon(polyCnt);
                    foreach (IntPoint ip in pattern)
                        p.Add(new IntPoint(path[i].X - ip.X, path[i].Y - ip.Y));
                    result.Add(p);
                }

            PolygonPath quads = new PolygonPath((pathCnt + delta) * (polyCnt + 1));
            for (int i = 0; i < pathCnt - 1 + delta; i++)
                for (int j = 0; j < polyCnt; j++)
                {
                    Polygon quad = new Polygon(4);
                    quad.Add(result[i % pathCnt][j % polyCnt]);
                    quad.Add(result[(i + 1) % pathCnt][j % polyCnt]);
                    quad.Add(result[(i + 1) % pathCnt][(j + 1) % polyCnt]);
                    quad.Add(result[i % pathCnt][(j + 1) % polyCnt]);
                    if (!Orientation(quad)) quad.Reverse();
                    quads.Add(quad);
                }
            return quads;
        }

        public static PolygonPath MinkowskiSum(Polygon pattern, Polygon path, bool pathIsClosed)
        {
            PolygonPath paths = Minkowski(pattern, path, true, pathIsClosed);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolygonKind.Subject);
            c.Execute(ClipOperation.Union, paths, PolygonFillType.NonZero, PolygonFillType.NonZero);
            return paths;
        }

        private static Polygon TranslatePath(Polygon path, IntPoint delta)
        {
            Polygon outPath = new Polygon(path.Count);
            for (int i = 0; i < path.Count; i++)
                outPath.Add(new IntPoint(path[i].X + delta.X, path[i].Y + delta.Y));
            return outPath;
        }

        public static PolygonPath MinkowskiSum(Polygon pattern, PolygonPath paths, bool pathIsClosed)
        {
            PolygonPath solution = new PolygonPath();
            Clipper c = new Clipper();
            for (int i = 0; i < paths.Count; ++i)
            {
                PolygonPath tmp = Minkowski(pattern, paths[i], true, pathIsClosed);
                c.AddPaths(tmp, PolygonKind.Subject);
                if (pathIsClosed)
                {
                    Polygon path = TranslatePath(paths[i], pattern[0]);
                    c.AddPath(path, PolygonKind.Clip, true);
                }
            }
            c.Execute(ClipOperation.Union, solution,
              PolygonFillType.NonZero, PolygonFillType.NonZero);
            return solution;
        }

        public static PolygonPath MinkowskiDiff(Polygon poly1, Polygon poly2)
        {
            PolygonPath paths = Minkowski(poly1, poly2, false, true);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolygonKind.Subject);
            c.Execute(ClipOperation.Union, paths, PolygonFillType.NonZero, PolygonFillType.NonZero);
            return paths;
        }

        public static PolygonPath PolyTreeToPaths(PolygonTree polytree)
        {

            var result = new PolygonPath { Capacity = polytree.Children.Count };
            AddPolyNodeToPaths(polytree, NodeType.Any, result);
            return result;
        }

        internal static void AddPolyNodeToPaths(PolygonNode polynode, NodeType nt, PolygonPath paths)
        {
            bool match = true;
            switch (nt)
            {
                case NodeType.Open: return;
                case NodeType.Closed: match = !polynode.IsOpen; break;
                default: break;
            }

            if (polynode.Polygon.Count > 0 && match)
                paths.Add(polynode.Polygon);
            foreach (PolygonNode pn in polynode.Children)
                AddPolyNodeToPaths(pn, nt, paths);
        }

        public static PolygonPath OpenPathsFromPolyTree(PolygonTree polytree)
        {
            var result = new PolygonPath { Capacity = polytree.Children.Count };
            result.AddRange(from node in polytree.Children where node.IsOpen select node.Polygon);
            return result;
        }

        public static PolygonPath ClosedPathsFromPolyTree(PolygonTree polytree)
        {
            var result = new PolygonPath { Capacity = polytree.Children.Count };
            AddPolyNodeToPaths(polytree, NodeType.Closed, result);
            return result;
        }
    }
}
