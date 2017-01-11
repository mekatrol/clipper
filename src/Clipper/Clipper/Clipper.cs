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
* between old (legacy) and new code.                                           *
*                                                                              *
*******************************************************************************/

//use_lines: Enables open path clipping. Adds a very minor cost to performance.
#define use_lines

using System;
using System.Collections.Generic;
using System.Linq;

namespace Clipper
{
    public class ClipperBase
    {
        internal LocalMinima _minimaList;
        internal LocalMinima _currentLocalMinima;
        internal List<List<Edge>> _edges = new List<List<Edge>>();
        internal Scanbeam _scanbeam;
        internal List<OutputPolygon> _outputPolygons;
        internal Edge _activeEdges;
        internal bool _useFullRange;
        internal bool _hasOpenPaths;

        public bool PreserveCollinear { get; set; }

        internal ClipperBase() //constructor (nb: no external instantiation)
        {
            _minimaList = null;
            _currentLocalMinima = null;
            _useFullRange = false;
            _hasOpenPaths = false;
        }

        public virtual void Clear()
        {
            _currentLocalMinima = null;
            _minimaList = null;
            _edges.Clear();
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

        private Edge ProcessBound(Edge E, bool LeftBoundIsForward)
        {
            Edge EStart, Result = E;
            Edge Horz;

            if (Result.OutIndex == ClippingHelper.Skip)
            {
                //check if there are edges beyond the skip edge in the bound and if so
                //create another LocMin and calling ProcessBound once more ...
                E = Result;
                if (LeftBoundIsForward)
                {
                    while (E.Top.Y == E.Next.Bottom.Y) E = E.Next;
                    while (E != Result && E.Dx == ClippingHelper.Horizontal) E = E.Prev;
                }
                else
                {
                    while (E.Top.Y == E.Prev.Bottom.Y) E = E.Prev;
                    while (E != Result && E.Dx == ClippingHelper.Horizontal) E = E.Next;
                }
                if (E == Result)
                {
                    if (LeftBoundIsForward) Result = E.Next;
                    else Result = E.Prev;
                }
                else
                {
                    //there are more edges in the bound beyond result starting with E
                    if (LeftBoundIsForward)
                        E = Result.Next;
                    else
                        E = Result.Prev;
                    LocalMinima locMin = new LocalMinima();
                    locMin.Next = null;
                    locMin.Y = E.Bottom.Y;
                    locMin.LeftBound = null;
                    locMin.RightBound = E;
                    E.WindDelta = 0;
                    Result = ProcessBound(E, LeftBoundIsForward);
                    InsertLocalMinima(locMin);
                }
                return Result;
            }

            if (E.Dx == ClippingHelper.Horizontal)
            {
                //We need to be careful with open paths because this may not be a
                //true local minima (ie E may be following a skip edge).
                //Also, consecutive horz. edges may start heading left before going right.
                if (LeftBoundIsForward) EStart = E.Prev;
                else EStart = E.Next;
                if (EStart.Dx == ClippingHelper.Horizontal) //ie an adjoining horizontal skip edge
                {
                    if (EStart.Bottom.X != E.Bottom.X && EStart.Top.X != E.Bottom.X)
                        ReverseHorizontal(E);
                }
                else if (EStart.Bottom.X != E.Bottom.X)
                    ReverseHorizontal(E);
            }

            EStart = E;
            if (LeftBoundIsForward)
            {
                while (Result.Top.Y == Result.Next.Bottom.Y && Result.Next.OutIndex != ClippingHelper.Skip)
                    Result = Result.Next;
                if (Result.Dx == ClippingHelper.Horizontal && Result.Next.OutIndex != ClippingHelper.Skip)
                {
                    //nb: at the top of a bound, horizontals are added to the bound
                    //only when the preceding edge attaches to the horizontal's left vertex
                    //unless a Skip edge is encountered when that becomes the top divide
                    Horz = Result;
                    while (Horz.Prev.Dx == ClippingHelper.Horizontal) Horz = Horz.Prev;
                    if (Horz.Prev.Top.X > Result.Next.Top.X) Result = Horz.Prev;
                }
                while (E != Result)
                {
                    E.NextInLml = E.Next;
                    if (E.Dx == ClippingHelper.Horizontal && E != EStart && E.Bottom.X != E.Prev.Top.X)
                        ReverseHorizontal(E);
                    E = E.Next;
                }
                if (E.Dx == ClippingHelper.Horizontal && E != EStart && E.Bottom.X != E.Prev.Top.X)
                    ReverseHorizontal(E);
                Result = Result.Next; //move to the edge just beyond current bound
            }
            else
            {
                while (Result.Top.Y == Result.Prev.Bottom.Y && Result.Prev.OutIndex != ClippingHelper.Skip)
                    Result = Result.Prev;
                if (Result.Dx == ClippingHelper.Horizontal && Result.Prev.OutIndex != ClippingHelper.Skip)
                {
                    Horz = Result;
                    while (Horz.Next.Dx == ClippingHelper.Horizontal) Horz = Horz.Next;
                    if (Horz.Next.Top.X == Result.Prev.Top.X ||
                        Horz.Next.Top.X > Result.Prev.Top.X) Result = Horz.Next;
                }

                while (E != Result)
                {
                    E.NextInLml = E.Prev;
                    if (E.Dx == ClippingHelper.Horizontal && E != EStart && E.Bottom.X != E.Next.Top.X)
                        ReverseHorizontal(E);
                    E = E.Prev;
                }
                if (E.Dx == ClippingHelper.Horizontal && E != EStart && E.Bottom.X != E.Next.Top.X)
                    ReverseHorizontal(E);
                Result = Result.Prev; //move to the edge just beyond current bound
            }
            return Result;
        }
        //------------------------------------------------------------------------------


        public bool AddPath(Polygon pg, PolygonKind polygonKind, bool Closed)
        {
#if use_lines
            if (!Closed && polygonKind == PolygonKind.Clip)
                throw new ClipperException("AddPath: Open paths must be subject.");
#else
      if (!Closed)
        throw new ClipperException("AddPath: Open paths have been disabled.");
#endif

            int highI = (int)pg.Count - 1;
            if (Closed) while (highI > 0 && (pg[highI] == pg[0])) --highI;
            while (highI > 0 && (pg[highI] == pg[highI - 1])) --highI;
            if ((Closed && highI < 2) || (!Closed && highI < 1)) return false;

            //create a new edge array ...
            List<Edge> edges = new List<Edge>(highI + 1);
            for (int i = 0; i <= highI; i++) edges.Add(new Edge());

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
                if (edge.Current == edge.Next.Current && (Closed || edge.Next != eStart))
                {
                    if (edge == edge.Next) break;
                    if (edge == eStart) eStart = edge.Next;
                    edge = RemoveEdge(edge);
                    eLoopStop = edge;
                    continue;
                }
                if (edge.Prev == edge.Next)
                    break; //only two vertices
                else if (Closed && GeometryHelper.SlopesEqual(edge.Prev.Current, edge.Current, edge.Next.Current, _useFullRange) &&
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
                if ((edge == eLoopStop) || (!Closed && edge.Next == eStart)) break;
            }

            if ((!Closed && (edge == edge.Next)) || (Closed && (edge.Prev == edge.Next)))
                return false;

            if (!Closed)
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

            //Totally flat paths must be handled differently when adding them
            //to LocalMinima list to avoid endless loops etc ...
            if (isFlat)
            {
                if (Closed) return false;
                edge.Prev.OutIndex = ClippingHelper.Skip;
                LocalMinima locMin = new LocalMinima();
                locMin.Next = null;
                locMin.Y = edge.Bottom.Y;
                locMin.LeftBound = null;
                locMin.RightBound = edge;
                locMin.RightBound.Side = EdgeSide.Right;
                locMin.RightBound.WindDelta = 0;
                for (;;)
                {
                    if (edge.Bottom.X != edge.Prev.Top.X) ReverseHorizontal(edge);
                    if (edge.Next.OutIndex == ClippingHelper.Skip) break;
                    edge.NextInLml = edge.Next;
                    edge = edge.Next;
                }
                InsertLocalMinima(locMin);
                _edges.Add(edges);
                return true;
            }

            _edges.Add(edges);
            bool leftBoundIsForward;
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

                if (!Closed) localMinima.LeftBound.WindDelta = 0;
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
        //------------------------------------------------------------------------------

        public bool AddPaths(PolygonPath ppg, PolygonKind polygonKind)
        {
            bool result = false;
            for (int i = 0; i < ppg.Count; ++i)
                if (AddPath(ppg[i], polygonKind, ppg[i].IsClosed)) result = true;
            return result;
        }
        //------------------------------------------------------------------------------

        internal bool Pt2IsBetweenPt1AndPt3(IntPoint pt1, IntPoint pt2, IntPoint pt3)
        {
            if ((pt1 == pt3) || (pt1 == pt2) || (pt3 == pt2)) return false;
            else if (pt1.X != pt3.X) return (pt2.X > pt1.X) == (pt2.X < pt3.X);
            else return (pt2.Y > pt1.Y) == (pt2.Y < pt3.Y);
        }
        //------------------------------------------------------------------------------

        Edge RemoveEdge(Edge e)
        {
            //removes edge from double_linked_list (but without removing from memory)
            e.Prev.Next = e.Next;
            e.Next.Prev = e.Prev;
            Edge result = e.Next;
            e.Prev = null; //flag as removed (see ClipperBase.Clear)
            return result;
        }
        //------------------------------------------------------------------------------


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
        //------------------------------------------------------------------------------

        internal Boolean PopLocalMinima(long Y, out LocalMinima current)
        {
            current = _currentLocalMinima;
            if (_currentLocalMinima != null && _currentLocalMinima.Y == Y)
            {
                _currentLocalMinima = _currentLocalMinima.Next;
                return true;
            }
            return false;
        }
        //------------------------------------------------------------------------------

        private void ReverseHorizontal(Edge e)
        {
            //swap horizontal edges' top and bottom x's so they follow the natural
            //progression of the bounds - ie so their xbots will align with the
            //adjoining lower edge. [Helpful in the ProcessHorizontal() method.]
            GeometryHelper.Swap(ref e.Top.X, ref e.Bottom.X);
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        public static IntRect GetBounds(PolygonPath paths)
        {
            int i = 0, cnt = paths.Count;
            while (i < cnt && paths[i].Count == 0) i++;
            if (i == cnt) return new IntRect(0, 0, 0, 0);
            IntRect result = new IntRect();
            result.Left = paths[i][0].X;
            result.Right = result.Left;
            result.Top = paths[i][0].Y;
            result.Bottom = result.Top;
            for (; i < cnt; i++)
                for (int j = 0; j < paths[i].Count; j++)
                {
                    if (paths[i][j].X < result.Left) result.Left = paths[i][j].X;
                    else if (paths[i][j].X > result.Right) result.Right = paths[i][j].X;
                    if (paths[i][j].Y < result.Top) result.Top = paths[i][j].Y;
                    else if (paths[i][j].Y > result.Bottom) result.Bottom = paths[i][j].Y;
                }
            return result;
        }
        //------------------------------------------------------------------------------

        internal void InsertScanbeam(long Y)
        {
            //single-linked list: sorted descending, ignoring dups.
            if (_scanbeam == null)
            {
                _scanbeam = new Scanbeam();
                _scanbeam.Next = null;
                _scanbeam.Y = Y;
            }
            else if (Y > _scanbeam.Y)
            {
                Scanbeam newSb = new Scanbeam();
                newSb.Y = Y;
                newSb.Next = _scanbeam;
                _scanbeam = newSb;
            }
            else
            {
                Scanbeam sb2 = _scanbeam;
                while (sb2.Next != null && (Y <= sb2.Next.Y)) sb2 = sb2.Next;
                if (Y == sb2.Y) return; //ie ignores duplicates
                Scanbeam newSb = new Scanbeam();
                newSb.Y = Y;
                newSb.Next = sb2.Next;
                sb2.Next = newSb;
            }
        }
        //------------------------------------------------------------------------------

        internal Boolean PopScanbeam(out long Y)
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
        //------------------------------------------------------------------------------

        internal Boolean LocalMinimaPending()
        {
            return (_currentLocalMinima != null);
        }
        //------------------------------------------------------------------------------

        internal OutputPolygon CreateOutRec()
        {
            OutputPolygon result = new OutputPolygon();
            result.Index = ClippingHelper.Unassigned;
            result.IsHole = false;
            result.IsOpen = false;
            result.FirstLeft = null;
            result.Points = null;
            result.BottomPoint = null;
            result.PolygonNode = null;
            _outputPolygons.Add(result);
            result.Index = _outputPolygons.Count - 1;
            return result;
        }
        //------------------------------------------------------------------------------

        internal void DisposeOutRec(int index)
        {
            OutputPolygon outputPolygon = _outputPolygons[index];
            outputPolygon.Points = null;
            outputPolygon = null;
            _outputPolygons[index] = null;
        }
        //------------------------------------------------------------------------------

        internal void UpdateEdgeIntoAEL(ref Edge edge)
        {
            if (edge.NextInLml == null)
                throw new ClipperException("UpdateEdgeIntoAEL: invalid call");
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
        //------------------------------------------------------------------------------

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
                _activeEdges = edge1;
            else if (edge2.PrevInAel == null)
                _activeEdges = edge2;
        }
        //------------------------------------------------------------------------------

        internal void DeleteFromAEL(Edge e)
        {
            Edge AelPrev = e.PrevInAel;
            Edge AelNext = e.NextInAel;
            if (AelPrev == null && AelNext == null && (e != _activeEdges))
                return; //already deleted
            if (AelPrev != null)
                AelPrev.NextInAel = AelNext;
            else _activeEdges = AelNext;
            if (AelNext != null)
                AelNext.PrevInAel = AelPrev;
            e.NextInAel = null;
            e.PrevInAel = null;
        }
        //------------------------------------------------------------------------------
    } //end ClipperBase

    public class Clipper : ClipperBase
    {
        //InitOptions that can be passed to the constructor ...
        public const int ioReverseSolution = 1;
        public const int ioStrictlySimple = 2;
        public const int ioPreserveCollinear = 4;

        private ClipOperation _mClipOperation;
        private Maxima m_Maxima;
        private Edge m_SortedEdges;
        private readonly IntersectionList _intersectList = new IntersectionList();
        private bool m_ExecuteLocked;
        private PolygonFillType m_ClipFillType;
        private PolygonFillType m_SubjFillType;
        private List<Join> m_Joins;
        private List<Join> m_GhostJoins;
        private bool m_UsingPolyTree;

        public Clipper(int InitOptions = 0) : base() //constructor
        {
            _scanbeam = null;
            m_Maxima = null;
            _activeEdges = null;
            m_SortedEdges = null;
            m_ExecuteLocked = false;
            m_UsingPolyTree = false;
            _outputPolygons = new List<OutputPolygon>();
            m_Joins = new List<Join>();
            m_GhostJoins = new List<Join>();
            ReverseSolution = (ioReverseSolution & InitOptions) != 0;
            StrictlySimple = (ioStrictlySimple & InitOptions) != 0;
            PreserveCollinear = (ioPreserveCollinear & InitOptions) != 0;
        }
        //------------------------------------------------------------------------------

        private void InsertMaxima(long X)
        {
            //double-linked list: sorted ascending, ignoring dups.
            Maxima newMax = new Maxima();
            newMax.X = X;
            if (m_Maxima == null)
            {
                m_Maxima = newMax;
                m_Maxima.Next = null;
                m_Maxima.Prev = null;
            }
            else if (X < m_Maxima.X)
            {
                newMax.Next = m_Maxima;
                newMax.Prev = null;
                m_Maxima = newMax;
            }
            else
            {
                Maxima m = m_Maxima;
                while (m.Next != null && (X >= m.Next.X)) m = m.Next;
                if (X == m.X) return; //ie ignores duplicates (& CG to clean up newMax)
                                      //insert newMax between m and m.Next ...
                newMax.Next = m.Next;
                newMax.Prev = m;
                if (m.Next != null) m.Next.Prev = newMax;
                m.Next = newMax;
            }
        }
        //------------------------------------------------------------------------------

        public bool ReverseSolution
        {
            get;
            set;
        }
        //------------------------------------------------------------------------------

        public bool StrictlySimple
        {
            get;
            set;
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipOperation clipOperation, PolygonPath solution,
            PolygonFillType FillType = PolygonFillType.EvenOdd)
        {
            return Execute(clipOperation, solution, FillType, FillType);
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipOperation clipOperation, PolygonTree polytree,
            PolygonFillType FillType = PolygonFillType.EvenOdd)
        {
            return Execute(clipOperation, polytree, FillType, FillType);
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipOperation clipOperation, PolygonPath solution,
            PolygonFillType subjFillType, PolygonFillType clipFillType)
        {
            if (m_ExecuteLocked) return false;
            if (_hasOpenPaths) throw
              new ClipperException("Error: PolygonTree struct is needed for open path clipping.");

            m_ExecuteLocked = true;
            solution.Clear();
            m_SubjFillType = subjFillType;
            m_ClipFillType = clipFillType;
            _mClipOperation = clipOperation;
            m_UsingPolyTree = false;
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
                m_ExecuteLocked = false;
            }
            return succeeded;
        }
        //------------------------------------------------------------------------------

        public bool Execute(ClipOperation clipOperation, PolygonTree polytree,
            PolygonFillType subjFillType, PolygonFillType clipFillType)
        {
            if (m_ExecuteLocked) return false;
            m_ExecuteLocked = true;
            m_SubjFillType = subjFillType;
            m_ClipFillType = clipFillType;
            _mClipOperation = clipOperation;
            m_UsingPolyTree = true;
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
                m_ExecuteLocked = false;
            }
            return succeeded;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        private bool ExecuteInternal()
        {
            try
            {
                Reset();
                m_SortedEdges = null;
                m_Maxima = null;

                long botY, topY;
                if (!PopScanbeam(out botY)) return false;
                InsertLocalMinimaIntoAEL(botY);
                while (PopScanbeam(out topY) || LocalMinimaPending())
                {
                    ProcessHorizontals();
                    m_GhostJoins.Clear();
                    if (!ProcessIntersections(topY)) return false;
                    ProcessEdgesAtTopOfScanbeam(topY);
                    botY = topY;
                    InsertLocalMinimaIntoAEL(botY);
                }

                //fix orientations ...
                foreach (OutputPolygon outRec in _outputPolygons)
                {
                    if (outRec.Points == null || outRec.IsOpen) continue;
                    if ((outRec.IsHole ^ ReverseSolution) == (Area(outRec) > 0))
                        ReversePolyPtLinks(outRec.Points);
                }

                JoinCommonEdges();

                foreach (OutputPolygon outRec in _outputPolygons)
                {
                    if (outRec.Points == null)
                        continue;
                    else if (outRec.IsOpen)
                        FixupOutPolyline(outRec);
                    else
                        FixupOutPolygon(outRec);
                }

                if (StrictlySimple) DoSimplePolygons();
                return true;
            }
            //catch { return false; }
            finally
            {
                m_Joins.Clear();
                m_GhostJoins.Clear();
            }
        }
        //------------------------------------------------------------------------------

        private void DisposeAllPolyPts()
        {
            for (int i = 0; i < _outputPolygons.Count; ++i) DisposeOutRec(i);
            _outputPolygons.Clear();
        }
        //------------------------------------------------------------------------------

        private void AddJoin(OutputPoint Op1, OutputPoint Op2, IntPoint OffPt)
        {
            Join j = new Join();
            j.OutPoint1 = Op1;
            j.OutPoint2 = Op2;
            j.Offset = OffPt;
            m_Joins.Add(j);
        }
        //------------------------------------------------------------------------------

        private void AddGhostJoin(OutputPoint Op, IntPoint OffPt)
        {
            Join j = new Join();
            j.OutPoint1 = Op;
            j.Offset = OffPt;
            m_GhostJoins.Add(j);
        }
        //------------------------------------------------------------------------------

        private void InsertLocalMinimaIntoAEL(long botY)
        {
            LocalMinima lm;
            while (PopLocalMinima(botY, out lm))
            {
                Edge leftBound = lm.LeftBound;
                Edge rightBound = lm.RightBound;

                OutputPoint Op1 = null;
                if (leftBound == null)
                {
                    InsertEdgeIntoAEL(rightBound, null);
                    SetWindingCount(rightBound);
                    if (IsContributing(rightBound))
                        Op1 = AddOutPt(rightBound, rightBound.Bottom);
                }
                else if (rightBound == null)
                {
                    InsertEdgeIntoAEL(leftBound, null);
                    SetWindingCount(leftBound);
                    if (IsContributing(leftBound))
                        Op1 = AddOutPt(leftBound, leftBound.Bottom);
                    InsertScanbeam(leftBound.Top.Y);
                }
                else
                {
                    InsertEdgeIntoAEL(leftBound, null);
                    InsertEdgeIntoAEL(rightBound, leftBound);
                    SetWindingCount(leftBound);
                    rightBound.WindCount = leftBound.WindCount;
                    rightBound.WindCount2 = leftBound.WindCount2;
                    if (IsContributing(leftBound))
                        Op1 = AddLocalMinPoly(leftBound, rightBound, leftBound.Bottom);
                    InsertScanbeam(leftBound.Top.Y);
                }

                if (rightBound != null)
                {
                    if (rightBound.IsHorizontal)
                    {
                        if (rightBound.NextInLml != null)
                            InsertScanbeam(rightBound.NextInLml.Top.Y);
                        AddEdgeToSEL(rightBound);
                    }
                    else
                        InsertScanbeam(rightBound.Top.Y);
                }

                if (leftBound == null || rightBound == null) continue;

                //if output polygons share an Edge with a horizontal rb, they'll need joining later ...
                if (Op1 != null && rightBound.IsHorizontal &&
                  m_GhostJoins.Count > 0 && rightBound.WindDelta != 0)
                {
                    for (int i = 0; i < m_GhostJoins.Count; i++)
                    {
                        //if the horizontal Rb and a 'ghost' horizontal overlap, then convert
                        //the 'ghost' join to a real join ready for later ...
                        Join j = m_GhostJoins[i];
                        if (HorzSegmentsOverlap(j.OutPoint1.Point.X, j.Offset.X, rightBound.Bottom.X, rightBound.Top.X))
                            AddJoin(j.OutPoint1, Op1, j.Offset);
                    }
                }

                if (leftBound.OutIndex >= 0 && leftBound.PrevInAel != null &&
                  leftBound.PrevInAel.Current.X == leftBound.Bottom.X &&
                  leftBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(leftBound.PrevInAel.Current, leftBound.PrevInAel.Top, leftBound.Current, leftBound.Top, _useFullRange) &&
                  leftBound.WindDelta != 0 && leftBound.PrevInAel.WindDelta != 0)
                {
                    OutputPoint Op2 = AddOutPt(leftBound.PrevInAel, leftBound.Bottom);
                    AddJoin(Op1, Op2, leftBound.Top);
                }

                if (leftBound.NextInAel != rightBound)
                {

                    if (rightBound.OutIndex >= 0 && rightBound.PrevInAel.OutIndex >= 0 && GeometryHelper.SlopesEqual(rightBound.PrevInAel.Current, rightBound.PrevInAel.Top, rightBound.Current, rightBound.Top, _useFullRange) &&
                      rightBound.WindDelta != 0 && rightBound.PrevInAel.WindDelta != 0)
                    {
                        OutputPoint Op2 = AddOutPt(rightBound.PrevInAel, rightBound.Bottom);
                        AddJoin(Op1, Op2, rightBound.Top);
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
        //------------------------------------------------------------------------------

        private void InsertEdgeIntoAEL(Edge edge, Edge startEdge)
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
        //----------------------------------------------------------------------

        private bool E2InsertsBeforeE1(Edge e1, Edge e2)
        {
            if (e2.Current.X == e1.Current.X)
            {
                if (e2.Top.Y > e1.Top.Y)
                    return e2.Top.X < TopX(e1, e2.Top.Y);
                else return e1.Top.X > TopX(e2, e1.Top.Y);
            }
            else return e2.Current.X < e1.Current.X;
        }
        //------------------------------------------------------------------------------

        private bool IsEvenOddFillType(Edge edge)
        {
            if (edge.Kind == PolygonKind.Subject)
                return m_SubjFillType == PolygonFillType.EvenOdd;
            else
                return m_ClipFillType == PolygonFillType.EvenOdd;
        }
        //------------------------------------------------------------------------------

        private bool IsEvenOddAltFillType(Edge edge)
        {
            if (edge.Kind == PolygonKind.Subject)
                return m_ClipFillType == PolygonFillType.EvenOdd;
            else
                return m_SubjFillType == PolygonFillType.EvenOdd;
        }
        //------------------------------------------------------------------------------

        private bool IsContributing(Edge edge)
        {
            PolygonFillType pft, pft2;
            if (edge.Kind == PolygonKind.Subject)
            {
                pft = m_SubjFillType;
                pft2 = m_ClipFillType;
            }
            else
            {
                pft = m_ClipFillType;
                pft2 = m_SubjFillType;
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
        //------------------------------------------------------------------------------

        private void SetWindingCount(Edge edge)
        {
            Edge e = edge.PrevInAel;
            //find the edge of the same polytype that immediately preceeds 'edge' in AEL
            while (e != null && ((e.Kind != edge.Kind) || (e.WindDelta == 0))) e = e.PrevInAel;
            if (e == null)
            {
                PolygonFillType pft;
                pft = (edge.Kind == PolygonKind.Subject ? m_SubjFillType : m_ClipFillType);
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
        //------------------------------------------------------------------------------

        private void AddEdgeToSEL(Edge edge)
        {
            //SEL pointers in PEdge are use to build transient lists of horizontal edges.
            //However, since we don't need to worry about processing order, all additions
            //are made to the front of the list ...
            if (m_SortedEdges == null)
            {
                m_SortedEdges = edge;
                edge.PrevInSel = null;
                edge.NextInSel = null;
            }
            else
            {
                edge.NextInSel = m_SortedEdges;
                edge.PrevInSel = null;
                m_SortedEdges.PrevInSel = edge;
                m_SortedEdges = edge;
            }
        }
        //------------------------------------------------------------------------------

        internal Boolean PopEdgeFromSEL(out Edge e)
        {
            //Pop edge from front of SEL (ie SEL is a FILO list)
            e = m_SortedEdges;
            if (e == null) return false;
            Edge oldE = e;
            m_SortedEdges = e.NextInSel;
            if (m_SortedEdges != null) m_SortedEdges.PrevInSel = null;
            oldE.NextInSel = null;
            oldE.PrevInSel = null;
            return true;
        }
        //------------------------------------------------------------------------------

        private void CopyAELToSEL()
        {
            Edge e = _activeEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSel = e.PrevInAel;
                e.NextInSel = e.NextInAel;
                e = e.NextInAel;
            }
        }
        //------------------------------------------------------------------------------

        private void SwapPositionsInSEL(Edge edge1, Edge edge2)
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
                m_SortedEdges = edge1;
            else if (edge2.PrevInSel == null)
                m_SortedEdges = edge2;
        }
        //------------------------------------------------------------------------------


        private void AddLocalMaxPoly(Edge e1, Edge e2, IntPoint pt)
        {
            AddOutPt(e1, pt);
            if (e2.WindDelta == 0) AddOutPt(e2, pt);
            if (e1.OutIndex == e2.OutIndex)
            {
                e1.OutIndex = ClippingHelper.Unassigned;
                e2.OutIndex = ClippingHelper.Unassigned;
            }
            else if (e1.OutIndex < e2.OutIndex)
                AppendPolygon(e1, e2);
            else
                AppendPolygon(e2, e1);
        }
        //------------------------------------------------------------------------------

        private OutputPoint AddLocalMinPoly(Edge e1, Edge e2, IntPoint pt)
        {
            OutputPoint result;
            Edge e, prevE;
            if (e2.IsHorizontal || (e1.Dx > e2.Dx))
            {
                result = AddOutPt(e1, pt);
                e2.OutIndex = e1.OutIndex;
                e1.Side = EdgeSide.Left;
                e2.Side = EdgeSide.Right;
                e = e1;
                if (e.PrevInAel == e2)
                    prevE = e2.PrevInAel;
                else
                    prevE = e.PrevInAel;
            }
            else
            {
                result = AddOutPt(e2, pt);
                e1.OutIndex = e2.OutIndex;
                e1.Side = EdgeSide.Right;
                e2.Side = EdgeSide.Left;
                e = e2;
                if (e.PrevInAel == e1)
                    prevE = e1.PrevInAel;
                else
                    prevE = e.PrevInAel;
            }

            if (prevE != null && prevE.OutIndex >= 0)
            {
                long xPrev = TopX(prevE, pt.Y);
                long xE = TopX(e, pt.Y);
                if ((xPrev == xE) && (e.WindDelta != 0) && (prevE.WindDelta != 0) && GeometryHelper.SlopesEqual(new IntPoint(xPrev, pt.Y), prevE.Top, new IntPoint(xE, pt.Y), e.Top, _useFullRange))
                {
                    OutputPoint outputPoint = AddOutPt(prevE, pt);
                    AddJoin(result, outputPoint, e.Top);
                }
            }
            return result;
        }
        //------------------------------------------------------------------------------

        private OutputPoint AddOutPt(Edge e, IntPoint pt)
        {
            if (e.OutIndex < 0)
            {
                OutputPolygon outputPolygon = CreateOutRec();
                outputPolygon.IsOpen = (e.WindDelta == 0);
                OutputPoint newOp = new OutputPoint();
                outputPolygon.Points = newOp;
                newOp.Index = outputPolygon.Index;
                newOp.Point = pt;
                newOp.Next = newOp;
                newOp.Prev = newOp;
                if (!outputPolygon.IsOpen)
                    SetHoleState(e, outputPolygon);
                e.OutIndex = outputPolygon.Index; //nb: do this after SetZ !
                return newOp;
            }
            else
            {
                OutputPolygon outputPolygon = _outputPolygons[e.OutIndex];
                //OutputPolygon.Points is the 'Left-most' point & OutputPolygon.Points.Prev is the 'Right-most'
                OutputPoint op = outputPolygon.Points;
                bool ToFront = (e.Side == EdgeSide.Left);
                if (ToFront && pt == op.Point) return op;
                else if (!ToFront && pt == op.Prev.Point) return op.Prev;

                OutputPoint newOp = new OutputPoint();
                newOp.Index = outputPolygon.Index;
                newOp.Point = pt;
                newOp.Next = op;
                newOp.Prev = op.Prev;
                newOp.Prev.Next = newOp;
                op.Prev = newOp;
                if (ToFront) outputPolygon.Points = newOp;
                return newOp;
            }
        }
        //------------------------------------------------------------------------------

        private OutputPoint GetLastOutPt(Edge e)
        {
            OutputPolygon outputPolygon = _outputPolygons[e.OutIndex];
            if (e.Side == EdgeSide.Left)
                return outputPolygon.Points;
            else
                return outputPolygon.Points.Prev;
        }
        //------------------------------------------------------------------------------

        internal void SwapPoints(ref IntPoint pt1, ref IntPoint pt2)
        {
            IntPoint tmp = new IntPoint(pt1);
            pt1 = pt2;
            pt2 = tmp;
        }
        //------------------------------------------------------------------------------

        private bool HorzSegmentsOverlap(long seg1a, long seg1b, long seg2a, long seg2b)
        {
            if (seg1a > seg1b) GeometryHelper.Swap(ref seg1a, ref seg1b);
            if (seg2a > seg2b) GeometryHelper.Swap(ref seg2a, ref seg2b);
            return (seg1a < seg2b) && (seg2a < seg1b);
        }
        //------------------------------------------------------------------------------

        private void SetHoleState(Edge e, OutputPolygon outputPolygon)
        {
            Edge e2 = e.PrevInAel;
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
        //------------------------------------------------------------------------------

        private double GetDx(IntPoint pt1, IntPoint pt2)
        {
            if (pt1.Y == pt2.Y) return ClippingHelper.Horizontal;
            else return (double)(pt2.X - pt1.X) / (pt2.Y - pt1.Y);
        }
        //---------------------------------------------------------------------------

        private bool FirstIsBottomPt(OutputPoint btmPt1, OutputPoint btmPt2)
        {
            OutputPoint p = btmPt1.Prev;
            while ((p.Point == btmPt1.Point) && (p != btmPt1)) p = p.Prev;
            double dx1p = Math.Abs(GetDx(btmPt1.Point, p.Point));
            p = btmPt1.Next;
            while ((p.Point == btmPt1.Point) && (p != btmPt1)) p = p.Next;
            double dx1n = Math.Abs(GetDx(btmPt1.Point, p.Point));

            p = btmPt2.Prev;
            while ((p.Point == btmPt2.Point) && (p != btmPt2)) p = p.Prev;
            double dx2p = Math.Abs(GetDx(btmPt2.Point, p.Point));
            p = btmPt2.Next;
            while ((p.Point == btmPt2.Point) && (p != btmPt2)) p = p.Next;
            double dx2n = Math.Abs(GetDx(btmPt2.Point, p.Point));

            if (Math.Max(dx1p, dx1n) == Math.Max(dx2p, dx2n) &&
              Math.Min(dx1p, dx1n) == Math.Min(dx2p, dx2n))
                return Area(btmPt1) > 0; //if otherwise identical use orientation
            else
                return (dx1p >= dx2p && dx1p >= dx2n) || (dx1n >= dx2p && dx1n >= dx2n);
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        private OutputPolygon GetLowermostRec(OutputPolygon outRec1, OutputPolygon outRec2)
        {
            //work out which polygon fragment has the correct hole state ...
            if (outRec1.BottomPoint == null)
                outRec1.BottomPoint = GetBottomPt(outRec1.Points);
            if (outRec2.BottomPoint == null)
                outRec2.BottomPoint = GetBottomPt(outRec2.Points);
            OutputPoint bPt1 = outRec1.BottomPoint;
            OutputPoint bPt2 = outRec2.BottomPoint;
            if (bPt1.Point.Y > bPt2.Point.Y) return outRec1;
            else if (bPt1.Point.Y < bPt2.Point.Y) return outRec2;
            else if (bPt1.Point.X < bPt2.Point.X) return outRec1;
            else if (bPt1.Point.X > bPt2.Point.X) return outRec2;
            else if (bPt1.Next == bPt1) return outRec2;
            else if (bPt2.Next == bPt2) return outRec1;
            else if (FirstIsBottomPt(bPt1, bPt2)) return outRec1;
            else return outRec2;
        }
        //------------------------------------------------------------------------------

        bool OutRec1RightOfOutRec2(OutputPolygon outRec1, OutputPolygon outRec2)
        {
            do
            {
                outRec1 = outRec1.FirstLeft;
                if (outRec1 == outRec2) return true;
            } while (outRec1 != null);
            return false;
        }
        //------------------------------------------------------------------------------

        private OutputPolygon GetOutRec(int idx)
        {
            OutputPolygon outrec = _outputPolygons[idx];
            while (outrec != _outputPolygons[outrec.Index])
                outrec = _outputPolygons[outrec.Index];
            return outrec;
        }
        //------------------------------------------------------------------------------

        private void AppendPolygon(Edge e1, Edge e2)
        {
            OutputPolygon outRec1 = _outputPolygons[e1.OutIndex];
            OutputPolygon outRec2 = _outputPolygons[e2.OutIndex];

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

            //join e2 poly onto e1 poly and delete pointers to e2 ...
            if (e1.Side == EdgeSide.Left)
            {
                if (e2.Side == EdgeSide.Left)
                {
                    //z y x a b c
                    ReversePolyPtLinks(p2_lft);
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
                if (e2.Side == EdgeSide.Right)
                {
                    //a b c z y x
                    ReversePolyPtLinks(p2_lft);
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

            int OKIdx = e1.OutIndex;
            int ObsoleteIdx = e2.OutIndex;

            e1.OutIndex = ClippingHelper.Unassigned; //nb: safe because we only get here via AddLocalMaxPoly
            e2.OutIndex = ClippingHelper.Unassigned;

            Edge e = _activeEdges;
            while (e != null)
            {
                if (e.OutIndex == ObsoleteIdx)
                {
                    e.OutIndex = OKIdx;
                    e.Side = e1.Side;
                    break;
                }
                e = e.NextInAel;
            }
            outRec2.Index = outRec1.Index;
        }
        //------------------------------------------------------------------------------

        private void ReversePolyPtLinks(OutputPoint pp)
        {
            if (pp == null) return;
            OutputPoint pp1;
            OutputPoint pp2;
            pp1 = pp;
            do
            {
                pp2 = pp1.Next;
                pp1.Next = pp1.Prev;
                pp1.Prev = pp2;
                pp1 = pp2;
            } while (pp1 != pp);
        }
        //------------------------------------------------------------------------------

        private static void SwapSides(Edge edge1, Edge edge2)
        {
            EdgeSide side = edge1.Side;
            edge1.Side = edge2.Side;
            edge2.Side = side;
        }
        //------------------------------------------------------------------------------

        private static void SwapPolyIndexes(Edge edge1, Edge edge2)
        {
            int outIdx = edge1.OutIndex;
            edge1.OutIndex = edge2.OutIndex;
            edge2.OutIndex = outIdx;
        }
        //------------------------------------------------------------------------------

        private void IntersectEdges(Edge e1, Edge e2, IntPoint pt)
        {
            //e1 will be to the left of e2 BELOW the intersection. Therefore e1 is before
            //e2 in AEL except when e1 is being inserted at the intersection point ...

            bool e1Contributing = (e1.OutIndex >= 0);
            bool e2Contributing = (e2.OutIndex >= 0);

#if use_lines
            //if either edge is on an OPEN path ...
            if (e1.WindDelta == 0 || e2.WindDelta == 0)
            {
                //ignore subject-subject open path intersections UNLESS they
                //are both open paths, AND they are both 'contributing maximas' ...
                if (e1.WindDelta == 0 && e2.WindDelta == 0) return;
                //if intersecting a subj line with a subj poly ...
                else if (e1.Kind == e2.Kind &&
                  e1.WindDelta != e2.WindDelta && _mClipOperation == ClipOperation.Union)
                {
                    if (e1.WindDelta == 0)
                    {
                        if (e2Contributing)
                        {
                            AddOutPt(e1, pt);
                            if (e1Contributing) e1.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                    else
                    {
                        if (e1Contributing)
                        {
                            AddOutPt(e2, pt);
                            if (e2Contributing) e2.OutIndex = ClippingHelper.Unassigned;
                        }
                    }
                }
                else if (e1.Kind != e2.Kind)
                {
                    if ((e1.WindDelta == 0) && Math.Abs(e2.WindCount) == 1 &&
                      (_mClipOperation != ClipOperation.Union || e2.WindCount2 == 0))
                    {
                        AddOutPt(e1, pt);
                        if (e1Contributing) e1.OutIndex = ClippingHelper.Unassigned;
                    }
                    else if ((e2.WindDelta == 0) && (Math.Abs(e1.WindCount) == 1) &&
                      (_mClipOperation != ClipOperation.Union || e1.WindCount2 == 0))
                    {
                        AddOutPt(e2, pt);
                        if (e2Contributing) e2.OutIndex = ClippingHelper.Unassigned;
                    }
                }
                return;
            }
#endif

            //update winding counts...
            //assumes that e1 will be to the Right of e2 ABOVE the intersection
            if (e1.Kind == e2.Kind)
            {
                if (IsEvenOddFillType(e1))
                {
                    int oldE1WindCnt = e1.WindCount;
                    e1.WindCount = e2.WindCount;
                    e2.WindCount = oldE1WindCnt;
                }
                else
                {
                    if (e1.WindCount + e2.WindDelta == 0) e1.WindCount = -e1.WindCount;
                    else e1.WindCount += e2.WindDelta;
                    if (e2.WindCount - e1.WindDelta == 0) e2.WindCount = -e2.WindCount;
                    else e2.WindCount -= e1.WindDelta;
                }
            }
            else
            {
                if (!IsEvenOddFillType(e2)) e1.WindCount2 += e2.WindDelta;
                else e1.WindCount2 = (e1.WindCount2 == 0) ? 1 : 0;
                if (!IsEvenOddFillType(e1)) e2.WindCount2 -= e1.WindDelta;
                else e2.WindCount2 = (e2.WindCount2 == 0) ? 1 : 0;
            }

            PolygonFillType e1FillType, e2FillType, e1FillType2, e2FillType2;
            if (e1.Kind == PolygonKind.Subject)
            {
                e1FillType = m_SubjFillType;
                e1FillType2 = m_ClipFillType;
            }
            else
            {
                e1FillType = m_ClipFillType;
                e1FillType2 = m_SubjFillType;
            }
            if (e2.Kind == PolygonKind.Subject)
            {
                e2FillType = m_SubjFillType;
                e2FillType2 = m_ClipFillType;
            }
            else
            {
                e2FillType = m_ClipFillType;
                e2FillType2 = m_SubjFillType;
            }

            int e1Wc, e2Wc;
            switch (e1FillType)
            {
                case PolygonFillType.Positive: e1Wc = e1.WindCount; break;
                case PolygonFillType.Negative: e1Wc = -e1.WindCount; break;
                default: e1Wc = Math.Abs(e1.WindCount); break;
            }
            switch (e2FillType)
            {
                case PolygonFillType.Positive: e2Wc = e2.WindCount; break;
                case PolygonFillType.Negative: e2Wc = -e2.WindCount; break;
                default: e2Wc = Math.Abs(e2.WindCount); break;
            }

            if (e1Contributing && e2Contributing)
            {
                if ((e1Wc != 0 && e1Wc != 1) || (e2Wc != 0 && e2Wc != 1) ||
                  (e1.Kind != e2.Kind && _mClipOperation != ClipOperation.Xor))
                {
                    AddLocalMaxPoly(e1, e2, pt);
                }
                else
                {
                    AddOutPt(e1, pt);
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if (e1Contributing)
            {
                if (e2Wc == 0 || e2Wc == 1)
                {
                    AddOutPt(e1, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }

            }
            else if (e2Contributing)
            {
                if (e1Wc == 0 || e1Wc == 1)
                {
                    AddOutPt(e2, pt);
                    SwapSides(e1, e2);
                    SwapPolyIndexes(e1, e2);
                }
            }
            else if ((e1Wc == 0 || e1Wc == 1) && (e2Wc == 0 || e2Wc == 1))
            {
                //neither edge is currently contributing ...
                long e1Wc2, e2Wc2;
                switch (e1FillType2)
                {
                    case PolygonFillType.Positive: e1Wc2 = e1.WindCount2; break;
                    case PolygonFillType.Negative: e1Wc2 = -e1.WindCount2; break;
                    default: e1Wc2 = Math.Abs(e1.WindCount2); break;
                }
                switch (e2FillType2)
                {
                    case PolygonFillType.Positive: e2Wc2 = e2.WindCount2; break;
                    case PolygonFillType.Negative: e2Wc2 = -e2.WindCount2; break;
                    default: e2Wc2 = Math.Abs(e2.WindCount2); break;
                }

                if (e1.Kind != e2.Kind)
                {
                    AddLocalMinPoly(e1, e2, pt);
                }
                else if (e1Wc == 1 && e2Wc == 1)
                    switch (_mClipOperation)
                    {
                        case ClipOperation.Intersection:
                            if (e1Wc2 > 0 && e2Wc2 > 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipOperation.Union:
                            if (e1Wc2 <= 0 && e2Wc2 <= 0)
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipOperation.Difference:
                            if (((e1.Kind == PolygonKind.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((e1.Kind == PolygonKind.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                                AddLocalMinPoly(e1, e2, pt);
                            break;
                        case ClipOperation.Xor:
                            AddLocalMinPoly(e1, e2, pt);
                            break;
                    }
                else
                    SwapSides(e1, e2);
            }
        }
        //------------------------------------------------------------------------------

        private void DeleteFromSEL(Edge e)
        {
            Edge SelPrev = e.PrevInSel;
            Edge SelNext = e.NextInSel;
            if (SelPrev == null && SelNext == null && (e != m_SortedEdges))
                return; //already deleted
            if (SelPrev != null)
                SelPrev.NextInSel = SelNext;
            else m_SortedEdges = SelNext;
            if (SelNext != null)
                SelNext.PrevInSel = SelPrev;
            e.NextInSel = null;
            e.PrevInSel = null;
        }
        //------------------------------------------------------------------------------

        private void ProcessHorizontals()
        {
            Edge horzEdge; //m_SortedEdges;
            while (PopEdgeFromSEL(out horzEdge))
                ProcessHorizontal(horzEdge);
        }
        //------------------------------------------------------------------------------

        void GetHorzDirection(Edge HorzEdge, out EdgeDirection Dir, out long Left, out long Right)
        {
            if (HorzEdge.Bottom.X < HorzEdge.Top.X)
            {
                Left = HorzEdge.Bottom.X;
                Right = HorzEdge.Top.X;
                Dir = EdgeDirection.LeftToRight;
            }
            else
            {
                Left = HorzEdge.Top.X;
                Right = HorzEdge.Bottom.X;
                Dir = EdgeDirection.RightToLeft;
            }
        }
        //------------------------------------------------------------------------

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

            Maxima currMax = m_Maxima;
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
                Edge e = GetNextInAEL(horzEdge, dir);
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
                        Edge eNextHorz = m_SortedEdges;
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
                        DeleteFromAEL(horzEdge);
                        DeleteFromAEL(eMaxPair);
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
                    Edge eNext = GetNextInAEL(e, dir);
                    SwapPositionsInAel(horzEdge, e);
                    e = eNext;
                } //end while(edge != null)

                //Break out of loop if HorzEdge.NextInLML is not also horizontal ...
                if (horzEdge.NextInLml == null || !horzEdge.NextInLml.IsHorizontal) break;

                UpdateEdgeIntoAEL(ref horzEdge);
                if (horzEdge.OutIndex >= 0) AddOutPt(horzEdge, horzEdge.Bottom);
                GetHorzDirection(horzEdge, out dir, out horzLeft, out horzRight);

            } //end for (;;)

            if (horzEdge.OutIndex >= 0 && op1 == null)
            {
                op1 = GetLastOutPt(horzEdge);
                Edge eNextHorz = m_SortedEdges;
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

                    UpdateEdgeIntoAEL(ref horzEdge);
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
                    UpdateEdgeIntoAEL(ref horzEdge);
            }
            else
            {
                if (horzEdge.OutIndex >= 0) AddOutPt(horzEdge, horzEdge.Top);
                DeleteFromAEL(horzEdge);
            }
        }
        //------------------------------------------------------------------------------

        private Edge GetNextInAEL(Edge e, EdgeDirection edgeDirection)
        {
            return edgeDirection == EdgeDirection.LeftToRight ? e.NextInAel : e.PrevInAel;
        }
        //------------------------------------------------------------------------------

        private bool IsMinima(Edge e)
        {
            return e != null && (e.Prev.NextInLml != e) && (e.Next.NextInLml != e);
        }
        //------------------------------------------------------------------------------

        private bool IsMaxima(Edge e, double Y)
        {
            return (e != null && e.Top.Y == Y && e.NextInLml == null);
        }
        //------------------------------------------------------------------------------

        private bool IsIntermediate(Edge e, double Y)
        {
            return (e.Top.Y == Y && e.NextInLml != null);
        }
        //------------------------------------------------------------------------------

        internal Edge GetMaximaPair(Edge e)
        {
            if ((e.Next.Top == e.Top) && e.Next.NextInLml == null)
                return e.Next;
            else if ((e.Prev.Top == e.Top) && e.Prev.NextInLml == null)
                return e.Prev;
            else
                return null;
        }
        //------------------------------------------------------------------------------

        internal Edge GetMaximaPairEx(Edge e)
        {
            //as above but returns null if MaxPair isn't in AEL (unless it's horizontal)
            Edge result = GetMaximaPair(e);
            if (result == null || result.OutIndex == ClippingHelper.Skip ||
              ((result.NextInAel == result.PrevInAel) && !result.IsHorizontal)) return null;
            return result;
        }
        //------------------------------------------------------------------------------

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
                m_SortedEdges = null;
                _intersectList.Clear();
                throw new ClipperException("ProcessIntersections error");
            }
            m_SortedEdges = null;
            return true;
        }
        //------------------------------------------------------------------------------

        private void BuildIntersectList(long topY)
        {
            if (_activeEdges == null) return;

            //prepare for sorting ...
            Edge e = _activeEdges;
            m_SortedEdges = e;
            while (e != null)
            {
                e.PrevInSel = e.PrevInAel;
                e.NextInSel = e.NextInAel;
                e.Current.X = TopX(e, topY);
                e = e.NextInAel;
            }

            //bubblesort ...
            bool isModified = true;
            while (isModified && m_SortedEdges != null)
            {
                isModified = false;
                e = m_SortedEdges;
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

                        SwapPositionsInSEL(e, eNext);
                        isModified = true;
                    }
                    else
                        e = eNext;
                }
                if (e.PrevInSel != null) e.PrevInSel.NextInSel = null;
                else break;
            }
            m_SortedEdges = null;
        }
        //------------------------------------------------------------------------------

        private bool EdgesAdjacent(IntersectNode inode)
        {
            return (inode.Edge1.NextInSel == inode.Edge2) ||
              (inode.Edge1.PrevInSel == inode.Edge2);
        }
        //------------------------------------------------------------------------------

        private static int IntersectNodeSort(IntersectNode node1, IntersectNode node2)
        {
            //the following typecast is safe because the differences in Point.Y will
            //be limited to the height of the scanbeam.
            return (int)(node2.Point.Y - node1.Point.Y);
        }
        //------------------------------------------------------------------------------

        private bool FixupIntersectionOrder()
        {
            //pre-condition: intersections are sorted bottom-most first.
            //Now it's crucial that intersections are made only between adjacent edges,
            //so to ensure this the order of intersections may need adjusting ...
            _intersectList.Sort();

            CopyAELToSEL();
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
                SwapPositionsInSEL(_intersectList[i].Edge1, _intersectList[i].Edge2);
            }
            return true;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        internal static long Round(double value)
        {
            return value < 0 ? (long)(value - 0.5) : (long)(value + 0.5);
        }
        //------------------------------------------------------------------------------

        private static long TopX(Edge edge, long currentY)
        {
            if (currentY == edge.Top.Y)
                return edge.Top.X;
            return edge.Bottom.X + Round(edge.Dx * (currentY - edge.Bottom.Y));
        }
        //------------------------------------------------------------------------------

        private void IntersectPoint(Edge edge1, Edge edge2, out IntPoint ip)
        {
            ip = new IntPoint();
            double b1, b2;
            //nb: with very large coordinate values, it's possible for SlopesEqual() to 
            //return false but for the edge.Dx value be equal due to double precision rounding.
            if (edge1.Dx == edge2.Dx)
            {
                ip.Y = edge1.Current.Y;
                ip.X = TopX(edge1, ip.Y);
                return;
            }

            if (edge1.Delta.X == 0)
            {
                ip.X = edge1.Bottom.X;
                if (edge2.IsHorizontal)
                {
                    ip.Y = edge2.Bottom.Y;
                }
                else
                {
                    b2 = edge2.Bottom.Y - (edge2.Bottom.X / edge2.Dx);
                    ip.Y = Round(ip.X / edge2.Dx + b2);
                }
            }
            else if (edge2.Delta.X == 0)
            {
                ip.X = edge2.Bottom.X;
                if (edge1.IsHorizontal)
                {
                    ip.Y = edge1.Bottom.Y;
                }
                else
                {
                    b1 = edge1.Bottom.Y - (edge1.Bottom.X / edge1.Dx);
                    ip.Y = Round(ip.X / edge1.Dx + b1);
                }
            }
            else
            {
                b1 = edge1.Bottom.X - edge1.Bottom.Y * edge1.Dx;
                b2 = edge2.Bottom.X - edge2.Bottom.Y * edge2.Dx;
                double q = (b2 - b1) / (edge1.Dx - edge2.Dx);
                ip.Y = Round(q);
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    ip.X = Round(edge1.Dx * q + b1);
                else
                    ip.X = Round(edge2.Dx * q + b2);
            }

            if (ip.Y < edge1.Top.Y || ip.Y < edge2.Top.Y)
            {
                if (edge1.Top.Y > edge2.Top.Y)
                    ip.Y = edge1.Top.Y;
                else
                    ip.Y = edge2.Top.Y;
                if (Math.Abs(edge1.Dx) < Math.Abs(edge2.Dx))
                    ip.X = TopX(edge1, ip.Y);
                else
                    ip.X = TopX(edge2, ip.Y);
            }
            //finally, don't allow 'ip' to be BELOW curr.Y (ie bottom of scanbeam) ...
            if (ip.Y > edge1.Current.Y)
            {
                ip.Y = edge1.Current.Y;
                //better to use the more vertical edge to derive X ...
                if (Math.Abs(edge1.Dx) > Math.Abs(edge2.Dx))
                    ip.X = TopX(edge2, ip.Y);
                else
                    ip.X = TopX(edge1, ip.Y);
            }
        }
        //------------------------------------------------------------------------------

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
                        UpdateEdgeIntoAEL(ref e);
                        if (e.OutIndex >= 0)
                        {
                            AddOutPt(e, e.Bottom);
                        }
                        AddEdgeToSEL(e);
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
            m_Maxima = null;

            //4. Promote intermediate vertices ...
            e = _activeEdges;
            while (e != null)
            {
                if (IsIntermediate(e, topY))
                {
                    OutputPoint op = null;
                    if (e.OutIndex >= 0)
                        op = AddOutPt(e, e.Top);
                    UpdateEdgeIntoAEL(ref e);

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
        //------------------------------------------------------------------------------

        private void DoMaxima(Edge e)
        {
            Edge eMaxPair = GetMaximaPairEx(e);
            if (eMaxPair == null)
            {
                if (e.OutIndex >= 0)
                    AddOutPt(e, e.Top);
                DeleteFromAEL(e);
                return;
            }

            Edge eNext = e.NextInAel;
            while (eNext != null && eNext != eMaxPair)
            {
                IntersectEdges(e, eNext, e.Top);
                SwapPositionsInAel(e, eNext);
                eNext = e.NextInAel;
            }

            if (e.OutIndex == ClippingHelper.Unassigned && eMaxPair.OutIndex == ClippingHelper.Unassigned)
            {
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
            else if (e.OutIndex >= 0 && eMaxPair.OutIndex >= 0)
            {
                if (e.OutIndex >= 0) AddLocalMaxPoly(e, eMaxPair, e.Top);
                DeleteFromAEL(e);
                DeleteFromAEL(eMaxPair);
            }
#if use_lines
            else if (e.WindDelta == 0)
            {
                if (e.OutIndex >= 0)
                {
                    AddOutPt(e, e.Top);
                    e.OutIndex = ClippingHelper.Unassigned;
                }
                DeleteFromAEL(e);

                if (eMaxPair.OutIndex >= 0)
                {
                    AddOutPt(eMaxPair, e.Top);
                    eMaxPair.OutIndex = ClippingHelper.Unassigned;
                }
                DeleteFromAEL(eMaxPair);
            }
#endif
            else throw new ClipperException("DoMaxima error");
        }
        //------------------------------------------------------------------------------

        public static void ReversePaths(PolygonPath polys)
        {
            foreach (var poly in polys) { poly.Reverse(); }
        }
        //------------------------------------------------------------------------------

        public static bool Orientation(Polygon poly)
        {
            return Area(poly) >= 0;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        bool GetOverlap(long a1, long a2, long b1, long b2, out long Left, out long Right)
        {
            if (a1 < a2)
            {
                if (b1 < b2) { Left = Math.Max(a1, b1); Right = Math.Min(a2, b2); }
                else { Left = Math.Max(a1, b2); Right = Math.Min(a2, b1); }
            }
            else
            {
                if (b1 < b2) { Left = Math.Max(a2, b1); Right = Math.Min(a1, b2); }
                else { Left = Math.Max(a2, b2); Right = Math.Min(a1, b1); }
            }
            return Left < Right;
        }
        //------------------------------------------------------------------------------

        bool JoinHorz(OutputPoint op1, OutputPoint op1b, OutputPoint op2, OutputPoint op2b,
          IntPoint Pt, bool DiscardLeft)
        {
            EdgeDirection Dir1 = (op1.Point.X > op1b.Point.X ?
              EdgeDirection.RightToLeft : EdgeDirection.LeftToRight);
            EdgeDirection Dir2 = (op2.Point.X > op2b.Point.X ?
              EdgeDirection.RightToLeft : EdgeDirection.LeftToRight);
            if (Dir1 == Dir2) return false;

            //When DiscardLeft, we want Op1b to be on the Left of Op1, otherwise we
            //want Op1b to be on the Right. (And likewise with Op2 and Op2b.)
            //So, to facilitate this while inserting Op1b and Op2b ...
            //when DiscardLeft, make sure we're AT or RIGHT of Point before adding Op1b,
            //otherwise make sure we're AT or LEFT of Point. (Likewise with Op2b.)
            if (Dir1 == EdgeDirection.LeftToRight)
            {
                while (op1.Next.Point.X <= Pt.X &&
                  op1.Next.Point.X >= op1.Point.X && op1.Next.Point.Y == Pt.Y)
                    op1 = op1.Next;
                if (DiscardLeft && (op1.Point.X != Pt.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, !DiscardLeft);
                if (op1b.Point != Pt)
                {
                    op1 = op1b;
                    op1.Point = Pt;
                    op1b = DupOutPt(op1, !DiscardLeft);
                }
            }
            else
            {
                while (op1.Next.Point.X >= Pt.X &&
                  op1.Next.Point.X <= op1.Point.X && op1.Next.Point.Y == Pt.Y)
                    op1 = op1.Next;
                if (!DiscardLeft && (op1.Point.X != Pt.X)) op1 = op1.Next;
                op1b = DupOutPt(op1, DiscardLeft);
                if (op1b.Point != Pt)
                {
                    op1 = op1b;
                    op1.Point = Pt;
                    op1b = DupOutPt(op1, DiscardLeft);
                }
            }

            if (Dir2 == EdgeDirection.LeftToRight)
            {
                while (op2.Next.Point.X <= Pt.X &&
                  op2.Next.Point.X >= op2.Point.X && op2.Next.Point.Y == Pt.Y)
                    op2 = op2.Next;
                if (DiscardLeft && (op2.Point.X != Pt.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, !DiscardLeft);
                if (op2b.Point != Pt)
                {
                    op2 = op2b;
                    op2.Point = Pt;
                    op2b = DupOutPt(op2, !DiscardLeft);
                };
            }
            else
            {
                while (op2.Next.Point.X >= Pt.X &&
                  op2.Next.Point.X <= op2.Point.X && op2.Next.Point.Y == Pt.Y)
                    op2 = op2.Next;
                if (!DiscardLeft && (op2.Point.X != Pt.X)) op2 = op2.Next;
                op2b = DupOutPt(op2, DiscardLeft);
                if (op2b.Point != Pt)
                {
                    op2 = op2b;
                    op2.Point = Pt;
                    op2b = DupOutPt(op2, DiscardLeft);
                };
            };

            if ((Dir1 == EdgeDirection.LeftToRight) == DiscardLeft)
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
        //------------------------------------------------------------------------------

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
                //Op1 -. Op1b & Op2 -. Op2b are the extremites of the horizontal edges
                if (!GetOverlap(op1.Point.X, op1b.Point.X, op2.Point.X, op2b.Point.X, out Left, out Right))
                    return false;

                //DiscardLeftSide: when overlapping edges are joined, a spike will created
                //which needs to be cleaned up. However, we don't want Op1 or Op2 caught up
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
        //----------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        //See "The Point in Polygon Problem for Arbitrary Polygons" by Hormann & Agathos
        //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.88.5498&rep=rep1&type=pdf
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
        //------------------------------------------------------------------------------

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
        //----------------------------------------------------------------------

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
        //----------------------------------------------------------------------

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
        //----------------------------------------------------------------------

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
        //----------------------------------------------------------------------

        private static OutputPolygon ParseFirstLeft(OutputPolygon FirstLeft)
        {
            while (FirstLeft != null && FirstLeft.Points == null)
                FirstLeft = FirstLeft.FirstLeft;
            return FirstLeft;
        }
        //------------------------------------------------------------------------------

        private void JoinCommonEdges()
        {
            for (int i = 0; i < m_Joins.Count; i++)
            {
                Join join = m_Joins[i];

                OutputPolygon outRec1 = GetOutRec(join.OutPoint1.Index);
                OutputPolygon outRec2 = GetOutRec(join.OutPoint2.Index);

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
                        //outRec1 contains outRec2 ...
                        outRec2.IsHole = !outRec1.IsHole;
                        outRec2.FirstLeft = outRec1;

                        if (m_UsingPolyTree) FixupFirstLefts2(outRec2, outRec1);

                        if ((outRec2.IsHole ^ ReverseSolution) == (Area(outRec2) > 0))
                            ReversePolyPtLinks(outRec2.Points);

                    }
                    else if (Poly2ContainsPoly1(outRec1.Points, outRec2.Points))
                    {
                        //outRec2 contains outRec1 ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec1.IsHole = !outRec2.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;
                        outRec1.FirstLeft = outRec2;

                        if (m_UsingPolyTree) FixupFirstLefts2(outRec1, outRec2);

                        if ((outRec1.IsHole ^ ReverseSolution) == (Area(outRec1) > 0))
                            ReversePolyPtLinks(outRec1.Points);
                    }
                    else
                    {
                        //the 2 polygons are completely separate ...
                        outRec2.IsHole = outRec1.IsHole;
                        outRec2.FirstLeft = outRec1.FirstLeft;

                        //fixup FirstLeft pointers that may need reassigning to OutRec2
                        if (m_UsingPolyTree) FixupFirstLefts1(outRec1, outRec2);
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
                    if (m_UsingPolyTree) FixupFirstLefts3(outRec2, outRec1);
                }
            }
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
                                if (m_UsingPolyTree) FixupFirstLefts2(outrec2, outrec);
                            }
                            else
                              if (Poly2ContainsPoly1(outrec.Points, outrec2.Points))
                            {
                                //OutRec1 is contained by OutRec2 ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec.IsHole = !outrec2.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                                outrec.FirstLeft = outrec2;
                                if (m_UsingPolyTree) FixupFirstLefts2(outrec, outrec2);
                            }
                            else
                            {
                                //the 2 polygons are separate ...
                                outrec2.IsHole = outrec.IsHole;
                                outrec2.FirstLeft = outrec.FirstLeft;
                                if (m_UsingPolyTree) FixupFirstLefts1(outrec, outrec2);
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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        internal double Area(OutputPolygon outputPolygon)
        {
            return Area(outputPolygon.Points);
        }
        //------------------------------------------------------------------------------

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

        //------------------------------------------------------------------------------
        // SimplifyPolygon functions ...
        // Convert self-intersecting polygons into simple polygons
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        private static double DistanceSqrd(IntPoint pt1, IntPoint pt2)
        {
            double dx = ((double)pt1.X - pt2.X);
            double dy = ((double)pt1.Y - pt2.Y);
            return (dx * dx + dy * dy);
        }
        //------------------------------------------------------------------------------

        private static double DistanceFromLineSqrd(IntPoint pt, IntPoint ln1, IntPoint ln2)
        {
            //The equation of a line in general form (Ax + By + C = 0)
            //given 2 points (x¹,y¹) & (x²,y²) is ...
            //(y¹ - y²)x + (x² - x¹)y + (y² - y¹)x¹ - (x² - x¹)y¹ = 0
            //A = (y¹ - y²); B = (x² - x¹); C = (y² - y¹)x¹ - (x² - x¹)y¹
            //perpendicular distance of point (x³,y³) = (Ax³ + By³ + C)/Sqrt(A² + B²)
            //see http://en.wikipedia.org/wiki/Perpendicular_distance
            double A = ln1.Y - ln2.Y;
            double B = ln2.X - ln1.X;
            double C = A * ln1.X + B * ln1.Y;
            C = A * pt.X + B * pt.Y - C;
            return (C * C) / (A * A + B * B);
        }
        //---------------------------------------------------------------------------

        private static bool SlopesNearCollinear(IntPoint pt1,
            IntPoint pt2, IntPoint pt3, double distSqrd)
        {
            //this function is more accurate when the point that's GEOMETRICALLY 
            //between the other 2 points is the one that's tested for distance.  
            //nb: with 'spikes', either pt1 or pt3 is geometrically between the other pts                    
            if (Math.Abs(pt1.X - pt2.X) > Math.Abs(pt1.Y - pt2.Y))
            {
                if ((pt1.X > pt2.X) == (pt1.X < pt3.X))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.X > pt1.X) == (pt2.X < pt3.X))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
            else
            {
                if ((pt1.Y > pt2.Y) == (pt1.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt1, pt2, pt3) < distSqrd;
                else if ((pt2.Y > pt1.Y) == (pt2.Y < pt3.Y))
                    return DistanceFromLineSqrd(pt2, pt1, pt3) < distSqrd;
                else
                    return DistanceFromLineSqrd(pt3, pt1, pt2) < distSqrd;
            }
        }
        //------------------------------------------------------------------------------

        private static bool PointsAreClose(IntPoint pt1, IntPoint pt2, double distSqrd)
        {
            double dx = (double)pt1.X - pt2.X;
            double dy = (double)pt1.Y - pt2.Y;
            return ((dx * dx) + (dy * dy) <= distSqrd);
        }
        //------------------------------------------------------------------------------

        private static OutputPoint ExcludeOp(OutputPoint op)
        {
            OutputPoint result = op.Prev;
            result.Next = op.Next;
            op.Next.Prev = result;
            result.Index = 0;
            return result;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        public static PolygonPath CleanPolygons(PolygonPath polys,
            double distance = 1.415)
        {
            PolygonPath result = new PolygonPath(polys.Count);
            for (int i = 0; i < polys.Count; i++)
                result.Add(CleanPolygon(polys[i], distance));
            return result;
        }
        //------------------------------------------------------------------------------

        internal static PolygonPath Minkowski(Polygon pattern, Polygon path, bool IsSum, bool IsClosed)
        {
            int delta = (IsClosed ? 1 : 0);
            int polyCnt = pattern.Count;
            int pathCnt = path.Count;
            PolygonPath result = new PolygonPath(pathCnt);
            if (IsSum)
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
        //------------------------------------------------------------------------------

        public static PolygonPath MinkowskiSum(Polygon pattern, Polygon path, bool pathIsClosed)
        {
            PolygonPath paths = Minkowski(pattern, path, true, pathIsClosed);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolygonKind.Subject);
            c.Execute(ClipOperation.Union, paths, PolygonFillType.NonZero, PolygonFillType.NonZero);
            return paths;
        }
        //------------------------------------------------------------------------------

        private static Polygon TranslatePath(Polygon path, IntPoint delta)
        {
            Polygon outPath = new Polygon(path.Count);
            for (int i = 0; i < path.Count; i++)
                outPath.Add(new IntPoint(path[i].X + delta.X, path[i].Y + delta.Y));
            return outPath;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        public static PolygonPath MinkowskiDiff(Polygon poly1, Polygon poly2)
        {
            PolygonPath paths = Minkowski(poly1, poly2, false, true);
            Clipper c = new Clipper();
            c.AddPaths(paths, PolygonKind.Subject);
            c.Execute(ClipOperation.Union, paths, PolygonFillType.NonZero, PolygonFillType.NonZero);
            return paths;
        }
        //------------------------------------------------------------------------------


        public static PolygonPath PolyTreeToPaths(PolygonTree polytree)
        {

            var result = new PolygonPath { Capacity = polytree.Children.Count };
            AddPolyNodeToPaths(polytree, NodeType.Any, result);
            return result;
        }
        //------------------------------------------------------------------------------

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
        //------------------------------------------------------------------------------

        public static PolygonPath OpenPathsFromPolyTree(PolygonTree polytree)
        {
            var result = new PolygonPath { Capacity = polytree.Children.Count };
            result.AddRange(from node in polytree.Children where node.IsOpen select node.Polygon);
            return result;
        }
        //------------------------------------------------------------------------------

        public static PolygonPath ClosedPathsFromPolyTree(PolygonTree polytree)
        {
            var result = new PolygonPath { Capacity = polytree.Children.Count };
            AddPolyNodeToPaths(polytree, NodeType.Closed, result);
            return result;
        }
        //------------------------------------------------------------------------------

    } //end Clipper

    public class ClipperOffset
    {
        private PolygonPath m_destPolys;
        private Polygon m_srcPoly;
        private Polygon m_destPoly;
        private List<DoublePoint> m_normals = new List<DoublePoint>();
        private double m_delta, m_sinA, m_sin, m_cos;
        private double m_miterLim, m_StepsPerRad;

        private IntPoint m_lowest;
        private PolygonNode _mPolygonNodes = new PolygonNode();

        public double ArcTolerance { get; set; }
        public double MiterLimit { get; set; }

        private const double two_pi = Math.PI * 2;
        private const double def_arc_tolerance = 0.25;

        public ClipperOffset(
          double miterLimit = 2.0, double arcTolerance = def_arc_tolerance)
        {
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
            m_lowest.X = -1;
        }
        //------------------------------------------------------------------------------

        public void Clear()
        {
            _mPolygonNodes.Children.Clear();
            m_lowest.X = -1;
        }
        //------------------------------------------------------------------------------

        internal static long Round(double value)
        {
            return value < 0 ? (long)(value - 0.5) : (long)(value + 0.5);
        }
        //------------------------------------------------------------------------------

        public void AddPath(Polygon path, JoinType joinType, EndType endType)
        {
            int highI = path.Count - 1;
            if (highI < 0) return;
            PolygonNode newNode = new PolygonNode();
            newNode.JoinType = joinType;
            newNode.EndType = endType;

            //strip duplicate points from path and also get index to the lowest point ...
            if (endType == EndType.ClosedLine || endType == EndType.ClosedPolygon)
                while (highI > 0 && path[0] == path[highI]) highI--;
            newNode.Polygon.Capacity = highI + 1;
            newNode.Polygon.Add(path[0]);
            int j = 0, k = 0;
            for (int i = 1; i <= highI; i++)
                if (newNode.Polygon[j] != path[i])
                {
                    j++;
                    newNode.Polygon.Add(path[i]);
                    if (path[i].Y > newNode.Polygon[k].Y ||
                      (path[i].Y == newNode.Polygon[k].Y &&
                      path[i].X < newNode.Polygon[k].X)) k = j;
                }
            if (endType == EndType.ClosedPolygon && j < 2) return;

            _mPolygonNodes.AddChild(newNode);

            //if this path's lowest point is lower than all the others then update m_lowest
            if (endType != EndType.ClosedPolygon) return;
            if (m_lowest.X < 0)
                m_lowest = new IntPoint(_mPolygonNodes.Children.Count - 1, k);
            else
            {
                IntPoint ip = _mPolygonNodes.Children[(int)m_lowest.X].Polygon[(int)m_lowest.Y];
                if (newNode.Polygon[k].Y > ip.Y ||
                  (newNode.Polygon[k].Y == ip.Y &&
                  newNode.Polygon[k].X < ip.X))
                    m_lowest = new IntPoint(_mPolygonNodes.Children.Count - 1, k);
            }
        }
        //------------------------------------------------------------------------------

        public void AddPaths(PolygonPath paths, JoinType joinType, EndType endType)
        {
            foreach (Polygon p in paths)
                AddPath(p, joinType, endType);
        }
        //------------------------------------------------------------------------------

        private void FixOrientations()
        {
            //fixup orientations of all closed paths if the orientation of the
            //closed path with the lowermost vertex is wrong ...
            if (m_lowest.X >= 0 &&
              !Clipper.Orientation(_mPolygonNodes.Children[(int)m_lowest.X].Polygon))
            {
                for (int i = 0; i < _mPolygonNodes.Children.Count; i++)
                {
                    PolygonNode node = _mPolygonNodes.Children[i];
                    if (node.EndType == EndType.ClosedPolygon ||
                      (node.EndType == EndType.ClosedLine &&
                      Clipper.Orientation(node.Polygon)))
                        node.Polygon.Reverse();
                }
            }
            else
            {
                for (int i = 0; i < _mPolygonNodes.Children.Count; i++)
                {
                    PolygonNode node = _mPolygonNodes.Children[i];
                    if (node.EndType == EndType.ClosedLine &&
                      !Clipper.Orientation(node.Polygon))
                        node.Polygon.Reverse();
                }
            }
        }
        //------------------------------------------------------------------------------

        internal static DoublePoint GetUnitNormal(IntPoint pt1, IntPoint pt2)
        {
            double dx = (pt2.X - pt1.X);
            double dy = (pt2.Y - pt1.Y);
            if ((dx == 0) && (dy == 0)) return new DoublePoint();

            double f = 1 * 1.0 / Math.Sqrt(dx * dx + dy * dy);
            dx *= f;
            dy *= f;

            return new DoublePoint(dy, -dx);
        }
        //------------------------------------------------------------------------------

        private void DoOffset(double delta)
        {
            m_destPolys = new PolygonPath();
            m_delta = delta;

            //if Zero offset, just copy any CLOSED polygons to m_p and return ...
            if (GeometryHelper.NearZero(delta))
            {
                m_destPolys.Capacity = _mPolygonNodes.Children.Count;
                for (int i = 0; i < _mPolygonNodes.Children.Count; i++)
                {
                    PolygonNode node = _mPolygonNodes.Children[i];
                    if (node.EndType == EndType.ClosedPolygon)
                        m_destPolys.Add(node.Polygon);
                }
                return;
            }

            //see offset_triginometry3.svg in the documentation folder ...
            if (MiterLimit > 2) m_miterLim = 2 / (MiterLimit * MiterLimit);
            else m_miterLim = 0.5;

            double y;
            if (ArcTolerance <= 0.0)
                y = def_arc_tolerance;
            else if (ArcTolerance > Math.Abs(delta) * def_arc_tolerance)
                y = Math.Abs(delta) * def_arc_tolerance;
            else
                y = ArcTolerance;
            //see offset_triginometry2.svg in the documentation folder ...
            double steps = Math.PI / Math.Acos(1 - y / Math.Abs(delta));
            m_sin = Math.Sin(two_pi / steps);
            m_cos = Math.Cos(two_pi / steps);
            m_StepsPerRad = steps / two_pi;
            if (delta < 0.0) m_sin = -m_sin;

            m_destPolys.Capacity = _mPolygonNodes.Children.Count * 2;
            for (int i = 0; i < _mPolygonNodes.Children.Count; i++)
            {
                PolygonNode node = _mPolygonNodes.Children[i];
                m_srcPoly = node.Polygon;

                int len = m_srcPoly.Count;

                if (len == 0 || (delta <= 0 && (len < 3 ||
                  node.EndType != EndType.ClosedPolygon)))
                    continue;

                m_destPoly = new Polygon();

                if (len == 1)
                {
                    if (node.JoinType == JoinType.Round)
                    {
                        double X = 1.0, Y = 0.0;
                        for (int j = 1; j <= steps; j++)
                        {
                            m_destPoly.Add(new IntPoint(
                              Round(m_srcPoly[0].X + X * delta),
                              Round(m_srcPoly[0].Y + Y * delta)));
                            double X2 = X;
                            X = X * m_cos - m_sin * Y;
                            Y = X2 * m_sin + Y * m_cos;
                        }
                    }
                    else
                    {
                        double X = -1.0, Y = -1.0;
                        for (int j = 0; j < 4; ++j)
                        {
                            m_destPoly.Add(new IntPoint(
                              Round(m_srcPoly[0].X + X * delta),
                              Round(m_srcPoly[0].Y + Y * delta)));
                            if (X < 0) X = 1;
                            else if (Y < 0) Y = 1;
                            else X = -1;
                        }
                    }
                    m_destPolys.Add(m_destPoly);
                    continue;
                }

                //build m_normals ...
                m_normals.Clear();
                m_normals.Capacity = len;
                for (int j = 0; j < len - 1; j++)
                    m_normals.Add(GetUnitNormal(m_srcPoly[j], m_srcPoly[j + 1]));
                if (node.EndType == EndType.ClosedLine ||
                  node.EndType == EndType.ClosedPolygon)
                    m_normals.Add(GetUnitNormal(m_srcPoly[len - 1], m_srcPoly[0]));
                else
                    m_normals.Add(new DoublePoint(m_normals[len - 2]));

                if (node.EndType == EndType.ClosedPolygon)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.JoinType);
                    m_destPolys.Add(m_destPoly);
                }
                else if (node.EndType == EndType.ClosedLine)
                {
                    int k = len - 1;
                    for (int j = 0; j < len; j++)
                        OffsetPoint(j, ref k, node.JoinType);
                    m_destPolys.Add(m_destPoly);
                    m_destPoly = new Polygon();
                    //re-build m_normals ...
                    DoublePoint n = m_normals[len - 1];
                    for (int j = len - 1; j > 0; j--)
                        m_normals[j] = new DoublePoint(-m_normals[j - 1].X, -m_normals[j - 1].Y);
                    m_normals[0] = new DoublePoint(-n.X, -n.Y);
                    k = 0;
                    for (int j = len - 1; j >= 0; j--)
                        OffsetPoint(j, ref k, node.JoinType);
                    m_destPolys.Add(m_destPoly);
                }
                else
                {
                    int k = 0;
                    for (int j = 1; j < len - 1; ++j)
                        OffsetPoint(j, ref k, node.JoinType);

                    IntPoint pt1;
                    if (node.EndType == EndType.OpenButt)
                    {
                        int j = len - 1;
                        pt1 = new IntPoint((long)Round(m_srcPoly[j].X + m_normals[j].X *
                          delta), (long)Round(m_srcPoly[j].Y + m_normals[j].Y * delta));
                        m_destPoly.Add(pt1);
                        pt1 = new IntPoint((long)Round(m_srcPoly[j].X - m_normals[j].X *
                          delta), (long)Round(m_srcPoly[j].Y - m_normals[j].Y * delta));
                        m_destPoly.Add(pt1);
                    }
                    else
                    {
                        int j = len - 1;
                        k = len - 2;
                        m_sinA = 0;
                        m_normals[j] = new DoublePoint(-m_normals[j].X, -m_normals[j].Y);
                        if (node.EndType == EndType.OpenSquare)
                            DoSquare(j, k);
                        else
                            DoRound(j, k);
                    }

                    //re-build m_normals ...
                    for (int j = len - 1; j > 0; j--)
                        m_normals[j] = new DoublePoint(-m_normals[j - 1].X, -m_normals[j - 1].Y);

                    m_normals[0] = new DoublePoint(-m_normals[1].X, -m_normals[1].Y);

                    k = len - 1;
                    for (int j = k - 1; j > 0; --j)
                        OffsetPoint(j, ref k, node.JoinType);

                    if (node.EndType == EndType.OpenButt)
                    {
                        pt1 = new IntPoint((long)Round(m_srcPoly[0].X - m_normals[0].X * delta),
                          (long)Round(m_srcPoly[0].Y - m_normals[0].Y * delta));
                        m_destPoly.Add(pt1);
                        pt1 = new IntPoint((long)Round(m_srcPoly[0].X + m_normals[0].X * delta),
                          (long)Round(m_srcPoly[0].Y + m_normals[0].Y * delta));
                        m_destPoly.Add(pt1);
                    }
                    else
                    {
                        k = 1;
                        m_sinA = 0;
                        if (node.EndType == EndType.OpenSquare)
                            DoSquare(0, 1);
                        else
                            DoRound(0, 1);
                    }
                    m_destPolys.Add(m_destPoly);
                }
            }
        }
        //------------------------------------------------------------------------------

        public void Execute(ref PolygonPath solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);
            //now clean up 'corners' ...
            Clipper clpr = new Clipper();
            clpr.AddPaths(m_destPolys, PolygonKind.Subject);
            if (delta > 0)
            {
                clpr.Execute(ClipOperation.Union, solution,
                  PolygonFillType.Positive, PolygonFillType.Positive);
            }
            else
            {
                IntRect r = Clipper.GetBounds(m_destPolys);
                Polygon outer = new Polygon(4);

                outer.Add(new IntPoint(r.Left - 10, r.Bottom + 10));
                outer.Add(new IntPoint(r.Right + 10, r.Bottom + 10));
                outer.Add(new IntPoint(r.Right + 10, r.Top - 10));
                outer.Add(new IntPoint(r.Left - 10, r.Top - 10));

                clpr.AddPath(outer, PolygonKind.Subject, true);
                clpr.ReverseSolution = true;
                clpr.Execute(ClipOperation.Union, solution, PolygonFillType.Negative, PolygonFillType.Negative);
                if (solution.Count > 0) solution.RemoveAt(0);
            }
        }
        //------------------------------------------------------------------------------

        public void Execute(ref PolygonTree solution, double delta)
        {
            solution.Clear();
            FixOrientations();
            DoOffset(delta);

            //now clean up 'corners' ...
            Clipper clpr = new Clipper();
            clpr.AddPaths(m_destPolys, PolygonKind.Subject);
            if (delta > 0)
            {
                clpr.Execute(ClipOperation.Union, solution,
                  PolygonFillType.Positive, PolygonFillType.Positive);
            }
            else
            {
                IntRect r = ClipperBase.GetBounds(m_destPolys);
                Polygon outer = new Polygon(4);

                outer.Add(new IntPoint(r.Left - 10, r.Bottom + 10));
                outer.Add(new IntPoint(r.Right + 10, r.Bottom + 10));
                outer.Add(new IntPoint(r.Right + 10, r.Top - 10));
                outer.Add(new IntPoint(r.Left - 10, r.Top - 10));

                clpr.AddPath(outer, PolygonKind.Subject, true);
                clpr.ReverseSolution = true;
                clpr.Execute(ClipOperation.Union, solution, PolygonFillType.Negative, PolygonFillType.Negative);
                //remove the outer PolygonNode rectangle ...
                if (solution.Children.Count == 1 && solution.Children[0].Children.Count > 0)
                {
                    var outerNode = solution.Children[0];
                    solution.Children.Capacity = outerNode.Children.Count;
                    solution.Children[0] = outerNode.Children[0];
                    solution.Children[0].Parent = solution;
                    for (int i = 1; i < outerNode.Children.Count; i++)
                        solution.AddChild(outerNode.Children[i]);
                }
                else
                    solution.Clear();
            }
        }
        //------------------------------------------------------------------------------

        void OffsetPoint(int j, ref int k, JoinType jointype)
        {
            //cross product ...
            m_sinA = (m_normals[k].X * m_normals[j].Y - m_normals[j].X * m_normals[k].Y);

            if (Math.Abs(m_sinA * m_delta) < 1.0)
            {
                //dot product ...
                double cosA = (m_normals[k].X * m_normals[j].X + m_normals[j].Y * m_normals[k].Y);
                if (cosA > 0) // angle ==> 0 degrees
                {
                    m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[k].X * m_delta),
                      Round(m_srcPoly[j].Y + m_normals[k].Y * m_delta)));
                    return;
                }
                //else angle ==> 180 degrees   
            }
            else if (m_sinA > 1.0) m_sinA = 1.0;
            else if (m_sinA < -1.0) m_sinA = -1.0;

            if (m_sinA * m_delta < 0)
            {
                m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[k].X * m_delta),
                  Round(m_srcPoly[j].Y + m_normals[k].Y * m_delta)));
                m_destPoly.Add(m_srcPoly[j]);
                m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + m_normals[j].X * m_delta),
                  Round(m_srcPoly[j].Y + m_normals[j].Y * m_delta)));
            }
            else
                switch (jointype)
                {
                    case JoinType.Miter:
                        {
                            double r = 1 + (m_normals[j].X * m_normals[k].X +
                              m_normals[j].Y * m_normals[k].Y);
                            if (r >= m_miterLim) DoMiter(j, k, r); else DoSquare(j, k);
                            break;
                        }
                    case JoinType.Square: DoSquare(j, k); break;
                    case JoinType.Round: DoRound(j, k); break;
                }
            k = j;
        }
        //------------------------------------------------------------------------------

        internal void DoSquare(int j, int k)
        {
            double dx = Math.Tan(Math.Atan2(m_sinA,
                m_normals[k].X * m_normals[j].X + m_normals[k].Y * m_normals[j].Y) / 4);
            m_destPoly.Add(new IntPoint(
                Round(m_srcPoly[j].X + m_delta * (m_normals[k].X - m_normals[k].Y * dx)),
                Round(m_srcPoly[j].Y + m_delta * (m_normals[k].Y + m_normals[k].X * dx))));
            m_destPoly.Add(new IntPoint(
                Round(m_srcPoly[j].X + m_delta * (m_normals[j].X + m_normals[j].Y * dx)),
                Round(m_srcPoly[j].Y + m_delta * (m_normals[j].Y - m_normals[j].X * dx))));
        }
        //------------------------------------------------------------------------------

        internal void DoMiter(int j, int k, double r)
        {
            double q = m_delta / r;
            m_destPoly.Add(new IntPoint(Round(m_srcPoly[j].X + (m_normals[k].X + m_normals[j].X) * q),
                Round(m_srcPoly[j].Y + (m_normals[k].Y + m_normals[j].Y) * q)));
        }
        //------------------------------------------------------------------------------

        internal void DoRound(int j, int k)
        {
            double a = Math.Atan2(m_sinA,
            m_normals[k].X * m_normals[j].X + m_normals[k].Y * m_normals[j].Y);
            int steps = Math.Max((int)Round(m_StepsPerRad * Math.Abs(a)), 1);

            double X = m_normals[k].X, Y = m_normals[k].Y, X2;
            for (int i = 0; i < steps; ++i)
            {
                m_destPoly.Add(new IntPoint(
                    Round(m_srcPoly[j].X + X * m_delta),
                    Round(m_srcPoly[j].Y + Y * m_delta)));
                X2 = X;
                X = X * m_cos - m_sin * Y;
                Y = X2 * m_sin + Y * m_cos;
            }
            m_destPoly.Add(new IntPoint(
            Round(m_srcPoly[j].X + m_normals[j].X * m_delta),
            Round(m_srcPoly[j].Y + m_normals[j].Y * m_delta)));
        }
        //------------------------------------------------------------------------------
    }

    class ClipperException : Exception
    {
        public ClipperException(string description) : base(description) { }
    }
    //------------------------------------------------------------------------------
} //end ClipperLib namespace
