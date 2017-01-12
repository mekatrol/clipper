using System;
using System.Collections.Generic;
using System.Linq;
using Clipper;

namespace Visualizer
{
    public static class BoundaryBuilder
    {
        public static IList<Edge> BuildPolygonBoundary(Polygon polygon, PolygonKind polygonKind)
        {
            // Create a populated list of edges setting initial edge values.
            var edges = Enumerable
                .Range(0, polygon.Count)
                .Select(i => new Edge
                {
                    Current = polygon[i],
                    Kind = polygonKind
                })
                .ToList();

            // Initialise boundary edge links.
            for (var i = 0; i < polygon.Count; i++)
            {
                var prevI = i == 0 ? polygon.Count - 1 : i - 1;
                var nextI = (i + 1) % polygon.Count;

                edges[i].Next = edges[nextI];
                edges[i].Prev = edges[prevI];
            }

            // Initialise bottom, top, dx
            foreach (var edge in edges)
            {
                InitialiseGeometry(edge);
            }

            // Initialise minima, maxima, intermediate flags
            foreach (var edge in edges)
            {
                edge.EndIsLocalMinima = EndIsLocalMinima(edge);
                edge.EndIsLocalMaxima = EndIsLocalMaxima(edge);
                edge.IsIntermediate = !(edge.EndIsLocalMinima || edge.EndIsLocalMaxima);
            }

            return edges;
        }

        public static IList<LocalMinima> BuildLml(Edge boundaryStart)
        {
            var localMinimaList = new List<LocalMinima>();

            Edge firstLocalMinimaEdge = null;
            var edge = boundaryStart;

            while (true)
            {
                if (!edge.EndIsLocalMinima)
                {
                    edge = edge.Next;
                    continue;
                }

                if (edge == firstLocalMinimaEdge)
                {
                    break;
                }

                if (firstLocalMinimaEdge == null)
                {
                    firstLocalMinimaEdge = edge;
                }

                // Local minima found
                var localMinima = new LocalMinima
                {
                    // This is a local minima, so the bottom of this edge
                    // (the start vertex of the next edge) is the minima vertex.
                    Y = edge.Bottom.Y
                };

                // Add to the LML list
                localMinimaList.Add(localMinima);

                // Build left and right bounds, returns next edge after
                // the forward bound.
                var nextForwardEdge = BuildMinimaBounds(localMinima, edge);

                // Move to the edge after right maxima
                edge = nextForwardEdge;
            }

            return localMinimaList;
        }

        private static Edge BuildMinimaBounds(LocalMinima localMinima, Edge edge)
        {
            var leftIsForward = edge.Dx > edge.Next.Dx;

            // Assign left and right bounds
            localMinima.LeftBound = leftIsForward ? edge.Next : edge;
            localMinima.RightBound = leftIsForward ? edge : edge.Next;

            // Build left and right bounds
            var nextLeft = BuildBound(localMinima.LeftBound, leftIsForward);
            var nextRight = BuildBound(localMinima.RightBound, !leftIsForward);

            // Return the next edge to continue search 
            return leftIsForward ? nextLeft : nextRight;
        }

        internal static Edge BuildBound(Edge edge, bool isForward)
        {
            // Helper functions to get the LM relative edge based on bound direction.
            Func<Edge, Edge> boundNext = e => isForward ? e.Next : e.Prev;

            // Continue until local maxima
            var lmlNext = boundNext(edge);
            var prev = edge;

            while (isForward ? !edge.EndIsLocalMaxima : !lmlNext.EndIsLocalMaxima)
            {
                // Link the LML next edge.
                edge.LmlNext = lmlNext;

                // If the edge is horizontal then we want the bottom
                // and top X values to follow the LML bound direction.
                // Non horizontal edges will be OK because we set bottom and 
                // top based on Y, which naturally follow the LML direction.
                if (edge.IsHorizontal)
                {
                    // And not left to right.
                    if (edge.Top.X != edge.LmlNext.Bottom.X)
                    {
                        SwapX(edge);
                    }
                }

                prev = edge;
                edge = edge.LmlNext;
                lmlNext = boundNext(edge);
            }

            // If the edge is horizontal then we want the bottom
            // and top X values to follow the LML bound direction.
            // Non horizontal edges will be OK because we set bottom and 
            // top based on Y, which naturally follow the LML direction.
            if (edge.IsHorizontal)
            {
                // And not left to right.
                if (prev.Current.X != edge.Current.X)
                {
                    SwapX(edge);
                }
            }

            return lmlNext;
        }

        public static void InitialiseGeometry(Edge edge)
        {
            // Set bottom and top based on Y coordinate.
            if (edge.Current.Y <= edge.Next.Current.Y)
            {
                edge.Bottom = edge.Current;
                edge.Top = edge.Next.Current;
            }
            else
            {
                edge.Top = edge.Current;
                edge.Bottom = edge.Next.Current;
            }

            SetDx(edge);
        }

        public static void SetDx(Edge edge)
        {
            edge.Delta = new IntPoint(
                edge.Top.X - edge.Bottom.X,
                edge.Top.Y - edge.Bottom.Y);

            // Is the edge horizontal?
            if (edge.Delta.Y == 0)
            {
                edge.Dx = GeometryHelper.GetDxSignedLength(edge.Bottom, edge.Top);

                edge.IsHorizontal = true;
                edge.IsVertical = false;
            }
            else
            {
                edge.Dx = (double)edge.Delta.X / edge.Delta.Y;

                edge.IsHorizontal = false;
                edge.IsVertical = edge.Delta.X == 0;
            }
        }

        private static void SwapX(Edge edge)
        {
            var tmpX = edge.Bottom.X;
            edge.Bottom.X = edge.Top.X;
            edge.Top.X = tmpX;
        }

        public static bool EndIsLocalMinima(Edge edge)
        {
            // RULE: No left bound has a bottom horizontal edge (any such edges 
            //       are shifted to the right bound).

            // To end as a local minima:
            //   1. The start edge cannot be horizontal.
            //   2. the edge1.Current higher than edge2.Current
            //   3. the edge2.Current lower than edge2.Next
            if (edge.IsHorizontal)
            {
                return false;
            }

            var edge1 = edge;
            var edge2 = edge.Next;

            // Move edge 2 past any horizontals.
            while (edge2.IsHorizontal && edge.Next != edge1)
            {
                edge2 = edge2.Next;
            }

            return
                edge1.Current.Y > edge2.Current.Y &&
                edge2.Current.Y < edge2.Next.Current.Y;
        }

        public static bool EndIsLocalMaxima(Edge edge)
        {
            var edge1 = edge;
            var edge2 = edge.Next;

            // RULE: No right bound has a top horizontal edge (any such edges 
            //       are shifted to the left bound).

            // To end as a local maxima:
            //   1. The end edge cannot be horizontal.
            //   2. the edge1.Current lower than edge2.Current
            //   3. the edge2.Current higher than edge2.Next
            if (edge1.IsHorizontal)
            {
                return false;
            }

            // Move edge 1 before any horizontals.
            while (edge2.IsHorizontal && edge2.Next != edge1)
            {
                edge2 = edge2.Next;
            }

            return
                edge1.Current.Y < edge2.Current.Y &&
                edge2.Current.Y > edge2.Next.Current.Y;
        }
    }
}
