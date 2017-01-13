using System.Collections.Generic;
using System.Linq;
using Clipper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class VattiClippingTests
    {
        private const double Scale = GeometryHelper.PolygonScaleConstant;
        private const double ScaleInverse = GeometryHelper.PolygonScaleInverseConstant;
        private const double AreaScale = GeometryHelper.PolygonAreaScaleConstant;
        private const double AreaScaleInverse = GeometryHelper.PolygonAreaScaleInverseConstant;

        [TestInitialize]
        public void InitializeTest()
        {
        }

        [TestMethod]
        public void TestPolygonSimplifySelfIntersecting()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(00.0, 00.0),
                        new DoublePoint(10.0, 10.0),
                        new DoublePoint(10.0, 00.0),
                        new DoublePoint(00.0, 10.0)
                    }.Select(p => new IntPoint(p * Scale))));

            const double expectedArea = 50.0;

            // The self intersecting polygon has equal and opposite areas, so resultant area will be zero.
            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse));

            // Simplify the polygon
            var solution = new PolygonPath();
            ClippingHelper.SimplifyPolygon(subject, solution);

            // The simplified non self intersecting polygon has valid area.
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - expectedArea));

            // Self intersecting simplified into two polygons.
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(3, polygon.Count);
            polygon.OrderBottomLeftFirst();
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(00.0 * Scale, polygon[0].X); Assert.AreEqual(00.0 * Scale, polygon[0].Y);
            Assert.AreEqual(05.0 * Scale, polygon[1].X); Assert.AreEqual(05.0 * Scale, polygon[1].Y);
            Assert.AreEqual(00.0 * Scale, polygon[2].X); Assert.AreEqual(10.0 * Scale, polygon[2].Y);

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(10.0 * Scale, polygon[0].X); Assert.AreEqual(00.0 * Scale, polygon[0].Y);
            Assert.AreEqual(10.0 * Scale, polygon[1].X); Assert.AreEqual(10.0 * Scale, polygon[1].Y);
            Assert.AreEqual(05.0 * Scale, polygon[2].X); Assert.AreEqual(05.0 * Scale, polygon[2].Y);
        }

        [TestMethod]
        public void TestUnion1()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0.0, 0.0),
                        new DoublePoint(1.0, 0.0),
                        new DoublePoint(1.0, 1.0),
                        new DoublePoint(0.0, 1.0)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                    new Polygon(
                    new[]
                    {
                        new DoublePoint(1.0, 0.0),
                        new DoublePoint(2.0, 0.0),
                        new DoublePoint(2.0, 1.0),
                        new DoublePoint(1.0, 1.0)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 2.0));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            polygon.OrderBottomLeftFirst();
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(0.0 * Scale, polygon[0].X); Assert.AreEqual(0.0 * Scale, polygon[0].Y);
            Assert.AreEqual(2.0 * Scale, polygon[1].X); Assert.AreEqual(0.0 * Scale, polygon[1].Y);
            Assert.AreEqual(2.0 * Scale, polygon[2].X); Assert.AreEqual(1.0 * Scale, polygon[2].Y);
            Assert.AreEqual(0.0 * Scale, polygon[3].X); Assert.AreEqual(1.0 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnion2()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(00, 00),
                        new DoublePoint(10, 00),
                        new DoublePoint(10, 10),
                        new DoublePoint(00, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(10, 00),
                        new DoublePoint(20, 00),
                        new DoublePoint(20, 10),
                        new DoublePoint(10, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 100.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 200.0));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            polygon.OrderBottomLeftFirst();
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(00 * Scale, polygon[0].X); Assert.AreEqual(00 * Scale, polygon[0].Y);
            Assert.AreEqual(20 * Scale, polygon[1].X); Assert.AreEqual(00 * Scale, polygon[1].Y);
            Assert.AreEqual(20 * Scale, polygon[2].X); Assert.AreEqual(10 * Scale, polygon[2].Y);
            Assert.AreEqual(00 * Scale, polygon[3].X); Assert.AreEqual(10 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnion3()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(00, 00),
                        new DoublePoint(10, 00),
                        new DoublePoint(10, 10),
                        new DoublePoint(00, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(05, 00),
                        new DoublePoint(15, 00),
                        new DoublePoint(15, 10),
                        new DoublePoint(05, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 100.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 150.0));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            polygon.OrderBottomLeftFirst();
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(00 * Scale, polygon[0].X); Assert.AreEqual(00 * Scale, polygon[0].Y);
            Assert.AreEqual(15 * Scale, polygon[1].X); Assert.AreEqual(00 * Scale, polygon[1].Y);
            Assert.AreEqual(15 * Scale, polygon[2].X); Assert.AreEqual(10 * Scale, polygon[2].Y);
            Assert.AreEqual(00 * Scale, polygon[3].X); Assert.AreEqual(10 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnion4()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+0.0, +0.0),
                        new DoublePoint(+5.0, +0.0),
                        new DoublePoint(+5.0, +5.0),
                        new DoublePoint(+0.0, +5.0)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+2.5, -2.5),
                        new DoublePoint(+7.5, -2.5),
                        new DoublePoint(+7.5, +2.5),
                        new DoublePoint(+2.5, +2.5)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 25.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 25.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            // Area is (2 * 50 * 50) - (1 * 25 * 25)
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - ((2 * 5.0 * 5.0) - (2.5 * 2.5))));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(8, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+2.5 * Scale, polygon[0].X); Assert.AreEqual(-2.5 * Scale, polygon[0].Y);
            Assert.AreEqual(+7.5 * Scale, polygon[1].X); Assert.AreEqual(-2.5 * Scale, polygon[1].Y);
            Assert.AreEqual(+7.5 * Scale, polygon[2].X); Assert.AreEqual(+2.5 * Scale, polygon[2].Y);
            Assert.AreEqual(+5.0 * Scale, polygon[3].X); Assert.AreEqual(+2.5 * Scale, polygon[3].Y);
            Assert.AreEqual(+5.0 * Scale, polygon[4].X); Assert.AreEqual(+5.0 * Scale, polygon[4].Y);
            Assert.AreEqual(+0.0 * Scale, polygon[5].X); Assert.AreEqual(+5.0 * Scale, polygon[5].Y);
            Assert.AreEqual(+0.0 * Scale, polygon[6].X); Assert.AreEqual(+0.0 * Scale, polygon[6].Y);
            Assert.AreEqual(+2.5 * Scale, polygon[7].X); Assert.AreEqual(+0.0 * Scale, polygon[7].Y);
        }

        [TestMethod]
        public void TestUnion5()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0000, 0000),
                        new DoublePoint(1000, 0000),
                        new DoublePoint(1000, 1000),
                        new DoublePoint(0000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(2000, 0000),
                        new DoublePoint(3000, 0000),
                        new DoublePoint(3000, 1000),
                        new DoublePoint(2000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 2000000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(2000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(3000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(3000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(2000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(0000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(1000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(1000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(0000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnion6()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0000, 0000),
                        new DoublePoint(1000, 0000),
                        new DoublePoint(1000, 1000),
                        new DoublePoint(0000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, null, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(0000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(1000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(1000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(0000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnion7()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0.0, 0.0),
                        new DoublePoint(1.0, 0.0),
                        new DoublePoint(1.0, 1.0),
                        new DoublePoint(0.0, 1.0)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                    new[]
                    {
                        new DoublePoint(2.0, 0.0),
                        new DoublePoint(3.0, 0.0),
                        new DoublePoint(3.0, 1.0),
                        new DoublePoint(2.0, 1.0)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.AreEqual(2, solution.Count);
            var area = solution.Area * AreaScaleInverse;
            Assert.IsTrue(GeometryHelper.NearZero(area - 2.0));

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(2.0 * Scale, polygon[0].X); Assert.AreEqual(0.0 * Scale, polygon[0].Y);
            Assert.AreEqual(3.0 * Scale, polygon[1].X); Assert.AreEqual(0.0 * Scale, polygon[1].Y);
            Assert.AreEqual(3.0 * Scale, polygon[2].X); Assert.AreEqual(1.0 * Scale, polygon[2].Y);
            Assert.AreEqual(2.0 * Scale, polygon[3].X); Assert.AreEqual(1.0 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestIntersection1()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+00, +00),
                        new DoublePoint(+50, +00),
                        new DoublePoint(+50, +50),
                        new DoublePoint(+00, +50)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+25, -25),
                        new DoublePoint(+75, -25),
                        new DoublePoint(+75, +25),
                        new DoublePoint(+25, +25)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 2500.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2500.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            // Area is (25 * 25)
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - (25 * 25)));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+25 * Scale, polygon[0].X); Assert.AreEqual(+00 * Scale, polygon[0].Y);
            Assert.AreEqual(+50 * Scale, polygon[1].X); Assert.AreEqual(+00 * Scale, polygon[1].Y);
            Assert.AreEqual(+50 * Scale, polygon[2].X); Assert.AreEqual(+25 * Scale, polygon[2].Y);
            Assert.AreEqual(+25 * Scale, polygon[3].X); Assert.AreEqual(+25 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestIntersection2()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(00, 00),
                        new DoublePoint(10, 00),
                        new DoublePoint(10, 10),
                        new DoublePoint(00, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(05, 00),
                        new DoublePoint(15, 00),
                        new DoublePoint(15, 10),
                        new DoublePoint(05, 10)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 100.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 50.0));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(05 * Scale, polygon[0].X); Assert.AreEqual(00 * Scale, polygon[0].Y);
            Assert.AreEqual(10 * Scale, polygon[1].X); Assert.AreEqual(00 * Scale, polygon[1].Y);
            Assert.AreEqual(10 * Scale, polygon[2].X); Assert.AreEqual(10 * Scale, polygon[2].Y);
            Assert.AreEqual(05 * Scale, polygon[3].X); Assert.AreEqual(10 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestIntersectionNonOverlapping()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0, 0),
                        new DoublePoint(1, 0),
                        new DoublePoint(1, 1),
                        new DoublePoint(0, 1)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(2, 0),
                        new DoublePoint(3, 0),
                        new DoublePoint(3, 1),
                        new DoublePoint(2, 1)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area - AreaScale));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area - AreaScale));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area));
            Assert.AreEqual(0, solution.Count);
        }

        [TestMethod]
        public void TestXor()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+00, +00),
                        new DoublePoint(+50, +00),
                        new DoublePoint(+50, +50),
                        new DoublePoint(+00, +50)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+25, -25),
                        new DoublePoint(+75, -25),
                        new DoublePoint(+75, +25),
                        new DoublePoint(+25, +25)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 2500.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2500.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));

            // Area is:
            // 1: the two original polygon path areas (2 * 50 * 50)
            // 2: less the overlap area from one of the original polygon paths (1 * 25 * 25)
            // 3: less the solution area (which has a reverse winding order and therefore a negative area)
            // (2 * 50 * 50) - (1 * 25 * 25) - (25 * 25)
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - ((2 * 50 * 50) - (1 * 25 * 25) - (25 * 25))));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(8, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+25 * Scale, polygon[0].X); Assert.AreEqual(-25 * Scale, polygon[0].Y);
            Assert.AreEqual(+75 * Scale, polygon[1].X); Assert.AreEqual(-25 * Scale, polygon[1].Y);
            Assert.AreEqual(+75 * Scale, polygon[2].X); Assert.AreEqual(+25 * Scale, polygon[2].Y);
            Assert.AreEqual(+50 * Scale, polygon[3].X); Assert.AreEqual(+25 * Scale, polygon[3].Y);
            Assert.AreEqual(+50 * Scale, polygon[4].X); Assert.AreEqual(+50 * Scale, polygon[4].Y);
            Assert.AreEqual(+00 * Scale, polygon[5].X); Assert.AreEqual(+50 * Scale, polygon[5].Y);
            Assert.AreEqual(+00 * Scale, polygon[6].X); Assert.AreEqual(+00 * Scale, polygon[6].Y);
            Assert.AreEqual(+25 * Scale, polygon[7].X); Assert.AreEqual(+00 * Scale, polygon[7].Y);

            polygon = solution[1];
            polygon.Reverse();
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+25 * Scale, polygon[0].X); Assert.AreEqual(+00 * Scale, polygon[0].Y);
            Assert.AreEqual(+50 * Scale, polygon[1].X); Assert.AreEqual(+00 * Scale, polygon[1].Y);
            Assert.AreEqual(+50 * Scale, polygon[2].X); Assert.AreEqual(+25 * Scale, polygon[2].Y);
            Assert.AreEqual(+25 * Scale, polygon[3].X); Assert.AreEqual(+25 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestXorNonOverlapping()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0000, 0000),
                        new DoublePoint(1000, 0000),
                        new DoublePoint(1000, 1000),
                        new DoublePoint(0000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(2000, 0000),
                        new DoublePoint(3000, 0000),
                        new DoublePoint(3000, 1000),
                        new DoublePoint(2000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 2000000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(2000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(3000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(3000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(2000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(0000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(1000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(1000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(0000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestDifference()
        {
            var subject = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+00, +00),
                    new DoublePoint(+50, +00),
                    new DoublePoint(+50, +50),
                    new DoublePoint(+00, +50)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+25, -25),
                    new DoublePoint(+75, -25),
                    new DoublePoint(+75, +25),
                    new DoublePoint(+25, +25)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 2500.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2500.0));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            // Area is:
            // 1: the original source polygon path area (50 * 50)
            // 2: less the overlap area from the operation polygon (1 * 25 * 25)
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - ((50 * 50) - (25 * 25))));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(6, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+00 * Scale, polygon[0].X); Assert.AreEqual(+00 * Scale, polygon[0].Y);
            Assert.AreEqual(+25 * Scale, polygon[1].X); Assert.AreEqual(+00 * Scale, polygon[1].Y);
            Assert.AreEqual(+25 * Scale, polygon[2].X); Assert.AreEqual(+25 * Scale, polygon[2].Y);
            Assert.AreEqual(+50 * Scale, polygon[3].X); Assert.AreEqual(+25 * Scale, polygon[3].Y);
            Assert.AreEqual(+50 * Scale, polygon[4].X); Assert.AreEqual(+50 * Scale, polygon[4].Y);
            Assert.AreEqual(+00 * Scale, polygon[5].X); Assert.AreEqual(+50 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestDifferenceNonOverlapping()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(0000, 0000),
                        new DoublePoint(1000, 0000),
                        new DoublePoint(1000, 1000),
                        new DoublePoint(0000, 1000)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(2000, 0000),
                    new DoublePoint(3000, 0000),
                    new DoublePoint(3000, 1000),
                    new DoublePoint(2000, 1000)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(0000 * Scale, polygon[0].X); Assert.AreEqual(0000 * Scale, polygon[0].Y);
            Assert.AreEqual(1000 * Scale, polygon[1].X); Assert.AreEqual(0000 * Scale, polygon[1].Y);
            Assert.AreEqual(1000 * Scale, polygon[2].X); Assert.AreEqual(1000 * Scale, polygon[2].Y);
            Assert.AreEqual(0000 * Scale, polygon[3].X); Assert.AreEqual(1000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestContains1()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+00, +00),
                        new DoublePoint(+10, +00),
                        new DoublePoint(+10, +10),
                        new DoublePoint(+00, +10)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+02, +02),
                        new DoublePoint(+08, +02),
                        new DoublePoint(+08, +08),
                        new DoublePoint(+02, +08)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 36.0));

            Assert.IsTrue(ClippingHelper.Contains(subject, clip));
        }

        [TestMethod]
        public void TestContains2()
        {
            var subject = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+00, +00),
                    new DoublePoint(+10, +00),
                    new DoublePoint(+10, +10),
                    new DoublePoint(+00, +10)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+12, +12),
                    new DoublePoint(+18, +12),
                    new DoublePoint(+18, +18),
                    new DoublePoint(+12, +18)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 36.0));

            Assert.IsFalse(ClippingHelper.Contains(subject, clip));
        }

        [TestMethod]
        public void TestContains3()
        {
            var subject = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+00, +00),
                    new DoublePoint(+10, +00),
                    new DoublePoint(+10, +10),
                    new DoublePoint(+00, +10)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+00, +00),
                    new DoublePoint(+10, +00),
                    new DoublePoint(+10, +10),
                    new DoublePoint(+00, +10)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 100.0));

            Assert.IsTrue(ClippingHelper.Contains(subject, clip));
        }

        [TestMethod]
        public void TestContains4()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+00, +00),
                        new DoublePoint(+10, +00),
                        new DoublePoint(+10, +10),
                        new DoublePoint(+00, +10)
                    }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+00, +00),
                        new DoublePoint(+10, +00),
                        new DoublePoint(+11, +10),
                        new DoublePoint(+00, +10)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 100.0));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 105.0));

            Assert.IsFalse(ClippingHelper.Contains(subject, clip));
        }

        [TestMethod]
        public void TestSimplify1()
        {
            var path = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+00.00, +10.00),
                    new DoublePoint(-05.00, -10.00),
                    new DoublePoint(+10.00, +05.00),
                    new DoublePoint(-10.00, +05.00),
                    new DoublePoint(+05.00, -10.00)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(ClippingHelper.SimplifyPolygon(path, solution));

            Assert.AreEqual(5, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-01.25 * Scale, polygon[0].X); Assert.AreEqual(+05.00 * Scale, polygon[0].Y);
            Assert.AreEqual(+01.25 * Scale, polygon[1].X); Assert.AreEqual(+05.00 * Scale, polygon[1].Y);
            Assert.AreEqual(+00.00 * Scale, polygon[2].X); Assert.AreEqual(+10.00 * Scale, polygon[2].Y);

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(3, polygon.Count);
            Assert.AreEqual(-03.00 * Scale, polygon[0].X); Assert.AreEqual(-02.00 * Scale, polygon[0].Y);
            Assert.AreEqual(-01.25 * Scale, polygon[1].X); Assert.AreEqual(+05.00 * Scale, polygon[1].Y);
            Assert.AreEqual(-10.00 * Scale, polygon[2].X); Assert.AreEqual(+05.00 * Scale, polygon[2].Y);

            polygon = solution[2];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            Assert.AreEqual(+05.00 * Scale, polygon[0].X); Assert.AreEqual(-10.00 * Scale, polygon[0].Y);
            Assert.AreEqual(+03.00 * Scale, polygon[1].X); Assert.AreEqual(-02.00 * Scale, polygon[1].Y);
            Assert.AreEqual(+00.00 * Scale, polygon[2].X); Assert.AreEqual(-05.00 * Scale, polygon[2].Y);

            polygon = solution[3];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            Assert.AreEqual(+03.00 * Scale, polygon[0].X); Assert.AreEqual(-02.00 * Scale, polygon[0].Y);
            Assert.AreEqual(+10.00 * Scale, polygon[1].X); Assert.AreEqual(+05.00 * Scale, polygon[1].Y);
            Assert.AreEqual(+01.25 * Scale, polygon[2].X); Assert.AreEqual(+05.00 * Scale, polygon[2].Y);

            polygon = solution[4];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            Assert.AreEqual(-05.00 * Scale, polygon[0].X); Assert.AreEqual(-10.00 * Scale, polygon[0].Y);
            Assert.AreEqual(+00.00 * Scale, polygon[1].X); Assert.AreEqual(-05.00 * Scale, polygon[1].Y);
            Assert.AreEqual(-03.00 * Scale, polygon[2].X); Assert.AreEqual(-02.00 * Scale, polygon[2].Y);
        }

        [TestMethod]
        public void TestSingleEdgeClip()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+0000, +0000),
                        new DoublePoint(+1000, +0000),
                        new DoublePoint(+1000, +1000),
                        new DoublePoint(+0000, +1000)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, null, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(1, solution.Count);

            // First polygon is subject
            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero(polygon.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(+1000 * Scale, polygon[0].Y);
            Assert.AreEqual(+0000 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(+0000 * Scale, polygon[2].X); Assert.AreEqual(+0000 * Scale, polygon[2].Y);
            Assert.AreEqual(+1000 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestUnionNoClipPolygon()
        {
            var subject = new PolygonPath(
                new Polygon(
                    new[]
                    {
                        new DoublePoint(+0000, -0500),
                        new DoublePoint(+0000, -0500),
                        new DoublePoint(+0500, -0500),
                        new DoublePoint(+0500, +0000),
                        new DoublePoint(+0500, +0500),
                        new DoublePoint(+0000, +0500),
                        new DoublePoint(-0500, +0500),
                        new DoublePoint(-0500, +0500),
                        new DoublePoint(-0500, +0000),
                        new DoublePoint(-0500, -0500),
                        new DoublePoint(+0000, -0500),
                        new DoublePoint(+0000, -0500)
                    }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, null, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestDiamond()
        {
            var subject = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+0100, +0000),
                    new DoublePoint(-0250, +0250),
                    new DoublePoint(-0500, +0000),
                    new DoublePoint(-0250, -0250)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(-0100, +0000),
                    new DoublePoint(+0250, -0250),
                    new DoublePoint(+0500, +0000),
                    new DoublePoint(+0250, +0250)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 150000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 150000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 285714.2857));
            Assert.AreEqual(1, solution.Count);
            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+500 * Scale, polygon[0].X); Assert.AreEqual(+000 * Scale, polygon[0].Y);
            Assert.AreEqual(+250 * Scale, polygon[1].X); Assert.AreEqual(+250 * Scale, polygon[1].Y);
            Assert.AreEqual(+000 * Scale, polygon[2].X); Assert.IsTrue(GeometryHelper.NearZero(+071.4285714 * Scale - polygon[2].Y));
            Assert.AreEqual(-250 * Scale, polygon[3].X); Assert.AreEqual(+250 * Scale, polygon[3].Y);
            Assert.AreEqual(-500 * Scale, polygon[4].X); Assert.AreEqual(+000 * Scale, polygon[4].Y);
            Assert.AreEqual(-250 * Scale, polygon[5].X); Assert.AreEqual(-250 * Scale, polygon[5].Y);
            Assert.AreEqual(+000 * Scale, polygon[6].X); Assert.IsTrue(GeometryHelper.NearZero(-071.4285714 * Scale - polygon[6].Y - 0));
            Assert.AreEqual(+250 * Scale, polygon[7].X); Assert.AreEqual(-250 * Scale, polygon[7].Y);

            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 14285.71428));
            Assert.AreEqual(1, solution.Count);
            polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+100 * Scale, polygon[0].X); Assert.AreEqual(+000 * Scale, polygon[0].Y);
            Assert.AreEqual(+000 * Scale, polygon[1].X); Assert.IsTrue(GeometryHelper.NearZero(+071.4285714 * Scale - polygon[1].Y));
            Assert.AreEqual(-100 * Scale, polygon[2].X); Assert.AreEqual(+000 * Scale, polygon[2].Y);
            Assert.AreEqual(+000 * Scale, polygon[3].X); Assert.IsTrue(GeometryHelper.NearZero(-071.4285714 * Scale - polygon[3].Y));


            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 135714.28571));
            Assert.AreEqual(1, solution.Count);
            polygon = solution[0];
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+000 * Scale, polygon[0].X); Assert.IsTrue(GeometryHelper.NearZero(-071.4285714 * Scale - polygon[0].Y));
            Assert.AreEqual(-100 * Scale, polygon[1].X); Assert.AreEqual(+000 * Scale, polygon[1].Y);
            Assert.AreEqual(+000 * Scale, polygon[2].X); Assert.IsTrue(GeometryHelper.NearZero(+071.4285714 * Scale - polygon[2].Y));
            Assert.AreEqual(-250 * Scale, polygon[3].X); Assert.AreEqual(+250 * Scale, polygon[3].Y);
            Assert.AreEqual(-500 * Scale, polygon[4].X); Assert.AreEqual(+000 * Scale, polygon[4].Y);
            Assert.AreEqual(-250 * Scale, polygon[5].X); Assert.AreEqual(-250 * Scale, polygon[5].Y);

            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 271428.57142));
            Assert.AreEqual(2, solution.Count);
            polygon = solution[0];
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+500 * Scale, polygon[0].X); Assert.AreEqual(+000 * Scale, polygon[0].Y);
            Assert.AreEqual(+250 * Scale, polygon[1].X); Assert.AreEqual(+250 * Scale, polygon[1].Y);
            Assert.AreEqual(+000 * Scale, polygon[2].X); Assert.IsTrue(GeometryHelper.NearZero(+071.4285714 * Scale - polygon[2].Y));
            Assert.AreEqual(+100 * Scale, polygon[3].X); Assert.AreEqual(+000 * Scale, polygon[3].Y);
            Assert.AreEqual(+000 * Scale, polygon[4].X); Assert.IsTrue(GeometryHelper.NearZero(-071.4285714 * Scale - polygon[4].Y));
            Assert.AreEqual(+250 * Scale, polygon[5].X); Assert.AreEqual(-250 * Scale, polygon[5].Y);
            polygon = solution[1];
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+000 * Scale, polygon[0].X); Assert.IsTrue(GeometryHelper.NearZero(-071.4285714 * Scale - polygon[0].Y));
            Assert.AreEqual(-100 * Scale, polygon[1].X); Assert.AreEqual(+000 * Scale, polygon[1].Y);
            Assert.AreEqual(+000 * Scale, polygon[2].X); Assert.IsTrue(GeometryHelper.NearZero(+071.4285714 * Scale - polygon[2].Y));
            Assert.AreEqual(-250 * Scale, polygon[3].X); Assert.AreEqual(+250 * Scale, polygon[3].Y);
            Assert.AreEqual(-500 * Scale, polygon[4].X); Assert.AreEqual(+000 * Scale, polygon[4].Y);
            Assert.AreEqual(-250 * Scale, polygon[5].X); Assert.AreEqual(-250 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestDiamondUnionSingleTouchVertex()
        {
            var subject = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(+1000, +0500),
                    new DoublePoint(+0500, +1000),
                    new DoublePoint(-1000, +0500),
                    new DoublePoint(+0000, +0000)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(
                new[]
                {
                    new DoublePoint(-0500, -1000),
                    new DoublePoint(+1000, -0500),
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(-1000, -0500)
                }.Select(p => new IntPoint(p * Scale))));

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 2000000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(+0500 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(+0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0000 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(-0500 * Scale, polygon[0].Y);
            Assert.AreEqual(+0000 * Scale, polygon[1].X); Assert.AreEqual(+0000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(-0500 * Scale, polygon[3].X); Assert.AreEqual(-1000 * Scale, polygon[3].Y);

            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area));
            Assert.AreEqual(0, solution.Count);

            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1000000));
            Assert.AreEqual(1, solution.Count);

            polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(+0500 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(+0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0000 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);

            solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));
            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 2000000));
            Assert.AreEqual(2, solution.Count);
            polygon = solution[0];
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(+0500 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(+0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0000 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);

            polygon = solution[1];
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(-0500 * Scale, polygon[0].Y);
            Assert.AreEqual(+0000 * Scale, polygon[1].X); Assert.AreEqual(+0000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(-0500 * Scale, polygon[3].X); Assert.AreEqual(-1000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestMergeWithHole1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var merge1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var merge2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0250, +0250),
                    new DoublePoint(+0250, -0250),
                    new DoublePoint(-0250, -0250),
                    new DoublePoint(-0250, +0250)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(subject1);
            var clip = new PolygonPath(new[] { merge1, merge2 });

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 750000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 750000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0250 * Scale, polygon[0].X); Assert.AreEqual(-0250 * Scale, polygon[0].Y);
            Assert.AreEqual(-0250 * Scale, polygon[1].X); Assert.AreEqual(+0250 * Scale, polygon[1].Y);
            Assert.AreEqual(+0250 * Scale, polygon[2].X); Assert.AreEqual(+0250 * Scale, polygon[2].Y);
            Assert.AreEqual(+0250 * Scale, polygon[3].X); Assert.AreEqual(-0250 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestMergeWithHole2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0000, +1000),
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(+0900, +0000),
                    new DoublePoint(+0900, +1000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0200),
                    new DoublePoint(+0200, +0200),
                    new DoublePoint(+0200, +0800),
                    new DoublePoint(+0400, +0800)
                }.Select(p => new IntPoint(p * Scale)));

            var merge1 = new Polygon(
                new[]
                {
                    new DoublePoint(+1000, +1000),
                    new DoublePoint(+0100, +1000),
                    new DoublePoint(+0100, +0000),
                    new DoublePoint(+1000, +0000)
                }.Select(p => new IntPoint(p * Scale)));

            var merge2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0600, +0200),
                    new DoublePoint(+0600, +0800),
                    new DoublePoint(+0800, +0800),
                    new DoublePoint(+0800, +0200)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(new[] { merge1, merge2 });

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (900 * 1000 - 200 * 600)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - (900 * 1000 - 200 * 600)));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - (800 * 1000 - 200 * 600 - 200 * 600)));
            Assert.AreEqual(3, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0900 * Scale, polygon[0].X); Assert.AreEqual(+1000 * Scale, polygon[0].Y);
            Assert.AreEqual(+0100 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(+0100 * Scale, polygon[2].X); Assert.AreEqual(+0000 * Scale, polygon[2].Y);
            Assert.AreEqual(+0900 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0600 * Scale, polygon[0].X); Assert.AreEqual(+0200 * Scale, polygon[0].Y);
            Assert.AreEqual(+0600 * Scale, polygon[1].X); Assert.AreEqual(+0800 * Scale, polygon[1].Y);
            Assert.AreEqual(+0800 * Scale, polygon[2].X); Assert.AreEqual(+0800 * Scale, polygon[2].Y);
            Assert.AreEqual(+0800 * Scale, polygon[3].X); Assert.AreEqual(+0200 * Scale, polygon[3].Y);

            polygon = solution[2];
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0200 * Scale, polygon[0].X); Assert.AreEqual(+0200 * Scale, polygon[0].Y);
            Assert.AreEqual(+0200 * Scale, polygon[1].X); Assert.AreEqual(+0800 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0800 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(+0200 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestWithHoleUnion()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(+0000, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1270000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000, polygonPoints[0].X); Assert.AreEqual(+0000, polygonPoints[0].Y);
            Assert.AreEqual(+0500, polygonPoints[1].X); Assert.AreEqual(+0000, polygonPoints[1].Y);
            Assert.AreEqual(+0500, polygonPoints[2].X); Assert.AreEqual(+0500, polygonPoints[2].Y);
            Assert.AreEqual(-0500, polygonPoints[3].X); Assert.AreEqual(+0500, polygonPoints[3].Y);
            Assert.AreEqual(-0500, polygonPoints[4].X); Assert.AreEqual(-0500, polygonPoints[4].Y);
            Assert.AreEqual(+0000, polygonPoints[5].X); Assert.AreEqual(-0500, polygonPoints[5].Y);
            Assert.AreEqual(+0000, polygonPoints[6].X); Assert.AreEqual(-1000, polygonPoints[6].Y);
            Assert.AreEqual(+1000, polygonPoints[7].X); Assert.AreEqual(-1000, polygonPoints[7].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(+0000 * Scale, polygon[3].Y);
            Assert.AreEqual(+0000 * Scale, polygon[4].X); Assert.AreEqual(+0000 * Scale, polygon[4].Y);
            Assert.AreEqual(+0000 * Scale, polygon[5].X); Assert.AreEqual(-0400 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestWith2HoleUnion1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(-1000, +1000),
                    new DoublePoint(-1000, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +1000)
                }.Select(p => new IntPoint(p * Scale)));

            var clip2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(new[] { clip1, clip2 });

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - (4000000 - 640000)));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 3360000));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000 * Scale, polygon[0].X); Assert.AreEqual(+1000 * Scale, polygon[0].Y);
            Assert.AreEqual(-1000 * Scale, polygon[1].X); Assert.AreEqual(+1000 * Scale, polygon[1].Y);
            Assert.AreEqual(-1000 * Scale, polygon[2].X); Assert.AreEqual(-1000 * Scale, polygon[2].Y);
            Assert.AreEqual(+1000 * Scale, polygon[3].X); Assert.AreEqual(-1000 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestWith2HoleUnion2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var clip2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(new[] { clip1, clip2 });

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - (1000000 - 640000)));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestWithHoleUnionSmall()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0.05, +0.05),
                    new DoublePoint(-0.05, -0.05),
                    new DoublePoint(+0.05, -0.05),
                    new DoublePoint(+0.05, +0.05)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0.04, +0.04),
                    new DoublePoint(+0.04, -0.04),
                    new DoublePoint(-0.04, -0.04),
                    new DoublePoint(-0.04, +0.04)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0.00, +0.00),
                    new DoublePoint(+0.00, -0.10),
                    new DoublePoint(+0.10, -0.10),
                    new DoublePoint(+0.10, +0.00)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (0.0100 - 0.0064)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 0.0100));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 0.0127));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0.10 * Scale, polygon[0].X); Assert.AreEqual(+0.00 * Scale, polygon[0].Y);
            Assert.AreEqual(+0.05 * Scale, polygon[1].X); Assert.AreEqual(+0.00 * Scale, polygon[1].Y);
            Assert.AreEqual(+0.05 * Scale, polygon[2].X); Assert.AreEqual(+0.05 * Scale, polygon[2].Y);
            Assert.AreEqual(-0.05 * Scale, polygon[3].X); Assert.AreEqual(+0.05 * Scale, polygon[3].Y);
            Assert.AreEqual(-0.05 * Scale, polygon[4].X); Assert.AreEqual(-0.05 * Scale, polygon[4].Y);
            Assert.AreEqual(+0.00 * Scale, polygon[5].X); Assert.AreEqual(-0.05 * Scale, polygon[5].Y);
            Assert.AreEqual(+0.00 * Scale, polygon[6].X); Assert.AreEqual(-0.10 * Scale, polygon[6].Y);
            Assert.AreEqual(+0.10 * Scale, polygon[7].X); Assert.AreEqual(-0.10 * Scale, polygon[7].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0.04 * Scale, polygon[0].X); Assert.AreEqual(-0.04 * Scale, polygon[0].Y);
            Assert.AreEqual(-0.04 * Scale, polygon[1].X); Assert.AreEqual(+0.04 * Scale, polygon[1].Y);
            Assert.AreEqual(+0.04 * Scale, polygon[2].X); Assert.AreEqual(+0.04 * Scale, polygon[2].Y);
            Assert.AreEqual(+0.04 * Scale, polygon[3].X); Assert.AreEqual(+0.00 * Scale, polygon[3].Y);
            Assert.AreEqual(+0.00 * Scale, polygon[4].X); Assert.AreEqual(+0.00 * Scale, polygon[4].Y);
            Assert.AreEqual(+0.00 * Scale, polygon[5].X); Assert.AreEqual(-0.04 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestWithHoleSubtractLargeAndSmall1()
        {
            // 5000 metres (as mm)
            const int large = 5000 * 1000;

            // 40 microns (as mm)
            const double small = 40 * 0.001;

            var subjectPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(-large, +large),
                    new DoublePoint(-large, -large),
                    new DoublePoint(+large, -large),
                    new DoublePoint(+large, +large)
                }.Select(p => new IntPoint(p * Scale)));

            var clipPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(-small, +small),
                    new DoublePoint(-small, -small),
                    new DoublePoint(+small, -small),
                    new DoublePoint(+small, +small)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(subjectPolygon);
            var clip = new PolygonPath(clipPolygon);

            const double subjectArea = 2.0 * large * 2.0 * large;
            const double clipArea = 2.0 * small * 2.0 * small;
            const double solutionArea = subjectArea - clipArea;

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - subjectArea));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - clipArea));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - solutionArea));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.IsTrue(GeometryHelper.NearZero(polygon.Area * AreaScaleInverse - subjectArea));
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+large * Scale, polygon[0].X); Assert.AreEqual(+large * Scale, polygon[0].Y);
            Assert.AreEqual(-large * Scale, polygon[1].X); Assert.AreEqual(+large * Scale, polygon[1].Y);
            Assert.AreEqual(-large * Scale, polygon[2].X); Assert.AreEqual(-large * Scale, polygon[2].Y);
            Assert.AreEqual(+large * Scale, polygon[3].X); Assert.AreEqual(-large * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero(polygon.Area * AreaScaleInverse + clipArea));
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-small * Scale, polygon[0].X); Assert.AreEqual(-small * Scale, polygon[0].Y);
            Assert.AreEqual(-small * Scale, polygon[1].X); Assert.AreEqual(+small * Scale, polygon[1].Y);
            Assert.AreEqual(+small * Scale, polygon[2].X); Assert.AreEqual(+small * Scale, polygon[2].Y);
            Assert.AreEqual(+small * Scale, polygon[3].X); Assert.AreEqual(-small * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestWithHoleSubtractLargeAndSmall2()
        {
            // 5000 metres (as mm)
            const double large = 5000 * 1000;

            // 40 microns (as mm)
            const double small = 40 * 0.001;

            var subjectPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(     0,      0),
                    new DoublePoint(+large,      0),
                    new DoublePoint(+large, +large),
                    new DoublePoint(     0, +large)
                }.Select(p => new IntPoint(p * Scale)));

            var clipPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(-small, +small),
                    new DoublePoint(-small, -small),
                    new DoublePoint(+small, -small),
                    new DoublePoint(+small, +small)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(subjectPolygon);
            var clip = new PolygonPath(clipPolygon);

            const double subjectArea = large * large;
            const double clipArea = 2.0 * small * 2.0 * small;
            const double solutionArea = subjectArea - clipArea / 4.0;

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - subjectArea));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - clipArea));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - solutionArea));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.IsTrue(GeometryHelper.NearZero(polygon.Area * AreaScaleInverse - subjectArea));
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+large * Scale, polygon[0].X); Assert.AreEqual(+large * Scale, polygon[0].Y);
            Assert.AreEqual(0 * Scale, polygon[1].X); Assert.AreEqual(+large * Scale, polygon[1].Y);
            Assert.AreEqual(0 * Scale, polygon[2].X); Assert.AreEqual(+small * Scale, polygon[2].Y);
            Assert.AreEqual(+small * Scale, polygon[3].X); Assert.AreEqual(+small * Scale, polygon[3].Y);
            Assert.AreEqual(+small * Scale, polygon[4].X); Assert.AreEqual(0 * Scale, polygon[4].Y);
            Assert.AreEqual(+large * Scale, polygon[5].X); Assert.AreEqual(0 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestWithHoleUnionLargeAndSmall()
        {
            // 5000 metres (as mm)
            const double large = 5000 * 1000;

            // 40 microns (as mm)
            const double small = 40 * 0.001;

            var subjectPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(     0,      0),
                    new DoublePoint(+large,      0),
                    new DoublePoint(+large, +large),
                    new DoublePoint(     0, +large)
                }.Select(p => new IntPoint(p * Scale)));

            var clipPolygon = new Polygon(
                new[]
                {
                    new DoublePoint(-small, +small),
                    new DoublePoint(-small, -small),
                    new DoublePoint(+small, -small),
                    new DoublePoint(+small, +small)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(subjectPolygon);
            var clip = new PolygonPath(clipPolygon);

            const double subjectArea = large * large;
            const double clipArea = 2.0 * small * 2.0 * small;
            const double solutionArea = subjectArea + 3.0 * clipArea / 4.0;

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - subjectArea));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - clipArea));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - solutionArea));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.IsTrue(GeometryHelper.NearZero(polygon.Area * AreaScaleInverse - solutionArea));
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+small, polygonPoints[0].X); Assert.AreEqual(0, polygonPoints[0].Y);
            Assert.AreEqual(+large, polygonPoints[1].X); Assert.AreEqual(0, polygonPoints[1].Y);
            Assert.AreEqual(+large, polygonPoints[2].X); Assert.AreEqual(+large, polygonPoints[2].Y);
            Assert.AreEqual(0, polygonPoints[3].X); Assert.AreEqual(+large, polygonPoints[3].Y);
            Assert.AreEqual(0, polygonPoints[4].X); Assert.AreEqual(+small, polygonPoints[4].Y);
            Assert.AreEqual(-small, polygonPoints[5].X); Assert.AreEqual(+small, polygonPoints[5].Y);
            Assert.AreEqual(-small, polygonPoints[6].X); Assert.AreEqual(-small, polygonPoints[6].Y);
            Assert.AreEqual(+small, polygonPoints[7].X); Assert.AreEqual(-small, polygonPoints[7].Y);
        }

        [TestMethod]
        public void TestWithHoleUnion10MetreX10Metre()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-050000, +050000),
                    new DoublePoint(-050000, -050000),
                    new DoublePoint(+050000, -050000),
                    new DoublePoint(+050000, +050000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+040000, +040000),
                    new DoublePoint(+040000, -040000),
                    new DoublePoint(-040000, -040000),
                    new DoublePoint(-040000, +040000)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+000000, +000000),
                    new DoublePoint(+000000, -100000),
                    new DoublePoint(+100000, -100000),
                    new DoublePoint(+100000, +000000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            const double subject1Area = 100000.0 * 100000.0;
            const double subject2Area = 080000.0 * 080000.0;
            const double clip1Area = 100000.0 * 100000.0;

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (subject1Area - subject2Area)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - clip1Area));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 12699999999.999998));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+100000, polygonPoints[0].X); Assert.AreEqual(+000000, polygonPoints[0].Y);
            Assert.AreEqual(+050000, polygonPoints[1].X); Assert.AreEqual(+000000, polygonPoints[1].Y);
            Assert.AreEqual(+050000, polygonPoints[2].X); Assert.AreEqual(+050000, polygonPoints[2].Y);
            Assert.AreEqual(-050000, polygonPoints[3].X); Assert.AreEqual(+050000, polygonPoints[3].Y);
            Assert.AreEqual(-050000, polygonPoints[4].X); Assert.AreEqual(-050000, polygonPoints[4].Y);
            Assert.AreEqual(+000000, polygonPoints[5].X); Assert.AreEqual(-050000, polygonPoints[5].Y);
            Assert.AreEqual(+000000, polygonPoints[6].X); Assert.AreEqual(-100000, polygonPoints[6].Y);
            Assert.AreEqual(+100000, polygonPoints[7].X); Assert.AreEqual(-100000, polygonPoints[7].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-40000, polygonPoints[0].X); Assert.AreEqual(-40000, polygonPoints[0].Y);
            Assert.AreEqual(-40000, polygonPoints[1].X); Assert.AreEqual(+40000, polygonPoints[1].Y);
            Assert.AreEqual(+40000, polygonPoints[2].X); Assert.AreEqual(+40000, polygonPoints[2].Y);
            Assert.AreEqual(+40000, polygonPoints[3].X); Assert.AreEqual(+00000, polygonPoints[3].Y);
            Assert.AreEqual(+00000, polygonPoints[4].X); Assert.AreEqual(+00000, polygonPoints[4].Y);
            Assert.AreEqual(+00000, polygonPoints[5].X); Assert.AreEqual(-40000, polygonPoints[5].Y);
        }

        [TestMethod]
        public void TestWithHoleIntersection1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(+0000, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 90000));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0000 * Scale, polygon[0].Y);
            Assert.AreEqual(+0400 * Scale, polygon[1].X); Assert.AreEqual(+0000 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(-0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0000 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
            Assert.AreEqual(+0000 * Scale, polygon[4].X); Assert.AreEqual(-0500 * Scale, polygon[4].Y);
            Assert.AreEqual(+0500 * Scale, polygon[5].X); Assert.AreEqual(-0500 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestWithHoleIntersection2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0450, +0450),
                    new DoublePoint(-0450, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0450)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2102500));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 262500));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0450 * Scale, polygon[0].Y);
            Assert.AreEqual(-0450 * Scale, polygon[1].X); Assert.AreEqual(+0450 * Scale, polygon[1].Y);
            Assert.AreEqual(-0450 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestWithHoleSubtract1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(+0000, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 270000));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(10, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0000 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(-0400 * Scale, polygon[1].Y);
            Assert.AreEqual(-0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(+0400 * Scale, polygon[3].Y);
            Assert.AreEqual(+0400 * Scale, polygon[4].X); Assert.AreEqual(+0000 * Scale, polygon[4].Y);
            Assert.AreEqual(+0500 * Scale, polygon[5].X); Assert.AreEqual(+0000 * Scale, polygon[5].Y);
            Assert.AreEqual(+0500 * Scale, polygon[6].X); Assert.AreEqual(+0500 * Scale, polygon[6].Y);
            Assert.AreEqual(-0500 * Scale, polygon[7].X); Assert.AreEqual(+0500 * Scale, polygon[7].Y);
            Assert.AreEqual(-0500 * Scale, polygon[8].X); Assert.AreEqual(-0500 * Scale, polygon[8].Y);
            Assert.AreEqual(+0000 * Scale, polygon[9].X); Assert.AreEqual(-0500 * Scale, polygon[9].Y);
        }

        [TestMethod]
        public void TestWithHoleSubtract2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0450, +0450),
                    new DoublePoint(-0450, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0450)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2102500));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Difference, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 97500));
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(6, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0450 * Scale, polygon[0].X); Assert.AreEqual(+0450 * Scale, polygon[0].Y);
            Assert.AreEqual(+0500 * Scale, polygon[1].X); Assert.AreEqual(+0450 * Scale, polygon[1].Y);
            Assert.AreEqual(+0500 * Scale, polygon[2].X); Assert.AreEqual(+0500 * Scale, polygon[2].Y);
            Assert.AreEqual(-0500 * Scale, polygon[3].X); Assert.AreEqual(+0500 * Scale, polygon[3].Y);
            Assert.AreEqual(-0500 * Scale, polygon[4].X); Assert.AreEqual(-0500 * Scale, polygon[4].Y);
            Assert.AreEqual(-0450 * Scale, polygon[5].X); Assert.AreEqual(-0500 * Scale, polygon[5].Y);
        }

        [TestMethod]
        public void TestWithHoleXor1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+0000, +0000),
                    new DoublePoint(+0000, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0000)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 1000000));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1180000));
            Assert.AreEqual(3, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygonPoints.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000, polygonPoints[0].X); Assert.AreEqual(+0000, polygonPoints[0].Y);
            Assert.AreEqual(+0500, polygonPoints[1].X); Assert.AreEqual(+0000, polygonPoints[1].Y);
            Assert.AreEqual(+0500, polygonPoints[2].X); Assert.AreEqual(+0500, polygonPoints[2].Y);
            Assert.AreEqual(-0500, polygonPoints[3].X); Assert.AreEqual(+0500, polygonPoints[3].Y);
            Assert.AreEqual(-0500, polygonPoints[4].X); Assert.AreEqual(-0500, polygonPoints[4].Y);
            Assert.AreEqual(+0000, polygonPoints[5].X); Assert.AreEqual(-0500, polygonPoints[5].Y);
            Assert.AreEqual(+0000, polygonPoints[6].X); Assert.AreEqual(-1000, polygonPoints[6].Y);
            Assert.AreEqual(+1000, polygonPoints[7].X); Assert.AreEqual(-1000, polygonPoints[7].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygonPoints.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0000, polygonPoints[0].X); Assert.AreEqual(-0500, polygonPoints[0].Y);
            Assert.AreEqual(+0000, polygonPoints[1].X); Assert.AreEqual(-0400, polygonPoints[1].Y);
            Assert.AreEqual(-0400, polygonPoints[2].X); Assert.AreEqual(-0400, polygonPoints[2].Y);
            Assert.AreEqual(-0400, polygonPoints[3].X); Assert.AreEqual(+0400, polygonPoints[3].Y);
            Assert.AreEqual(+0400, polygonPoints[4].X); Assert.AreEqual(+0400, polygonPoints[4].Y);
            Assert.AreEqual(+0400, polygonPoints[5].X); Assert.AreEqual(+0000, polygonPoints[5].Y);
            Assert.AreEqual(+0500, polygonPoints[6].X); Assert.AreEqual(+0000, polygonPoints[6].Y);
            Assert.AreEqual(+0500, polygonPoints[7].X); Assert.AreEqual(-0500, polygonPoints[7].Y);

            polygon = solution[2];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygonPoints.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0400, polygonPoints[0].X); Assert.AreEqual(+0000, polygonPoints[0].Y);
            Assert.AreEqual(+0000, polygonPoints[1].X); Assert.AreEqual(+0000, polygonPoints[1].Y);
            Assert.AreEqual(+0000, polygonPoints[2].X); Assert.AreEqual(-0400, polygonPoints[2].Y);
            Assert.AreEqual(+0400, polygonPoints[3].X); Assert.AreEqual(-0400, polygonPoints[3].Y);
        }

        [TestMethod]
        public void TestWithHoleXor2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0450, +0450),
                    new DoublePoint(-0450, -1000),
                    new DoublePoint(+1000, -1000),
                    new DoublePoint(+1000, +0450)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 2102500));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Xor, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 1937500));
            Assert.AreEqual(3, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(8, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+1000, polygonPoints[0].X); Assert.AreEqual(+0450, polygonPoints[0].Y);
            Assert.AreEqual(+0500, polygonPoints[1].X); Assert.AreEqual(+0450, polygonPoints[1].Y);
            Assert.AreEqual(+0500, polygonPoints[2].X); Assert.AreEqual(+0500, polygonPoints[2].Y);
            Assert.AreEqual(-0500, polygonPoints[3].X); Assert.AreEqual(+0500, polygonPoints[3].Y);
            Assert.AreEqual(-0500, polygonPoints[4].X); Assert.AreEqual(-0500, polygonPoints[4].Y);
            Assert.AreEqual(-0450, polygonPoints[5].X); Assert.AreEqual(-0500, polygonPoints[5].Y);
            Assert.AreEqual(-0450, polygonPoints[6].X); Assert.AreEqual(-1000, polygonPoints[6].Y);
            Assert.AreEqual(+1000, polygonPoints[7].X); Assert.AreEqual(-1000, polygonPoints[7].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0450, polygonPoints[0].X); Assert.AreEqual(-0500, polygonPoints[0].Y);
            Assert.AreEqual(-0450, polygonPoints[1].X); Assert.AreEqual(+0450, polygonPoints[1].Y);
            Assert.AreEqual(+0500, polygonPoints[2].X); Assert.AreEqual(+0450, polygonPoints[2].Y);
            Assert.AreEqual(+0500, polygonPoints[3].X); Assert.AreEqual(-0500, polygonPoints[3].Y);

            polygon = solution[2];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0400, polygonPoints[0].X); Assert.AreEqual(+0400, polygonPoints[0].Y);
            Assert.AreEqual(-0400, polygonPoints[1].X); Assert.AreEqual(+0400, polygonPoints[1].Y);
            Assert.AreEqual(-0400, polygonPoints[2].X); Assert.AreEqual(-0400, polygonPoints[2].Y);
            Assert.AreEqual(+0400, polygonPoints[3].X); Assert.AreEqual(-0400, polygonPoints[3].Y);
        }

        [TestMethod]
        public void TestSelfIntersectingWithHoleUnion()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(+03, +03),
                    new DoublePoint(+08, +08),
                    new DoublePoint(+06, +08),
                    new DoublePoint(+06, +03),
                    new DoublePoint(+02, +07),
                    new DoublePoint(+01, +04)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+02, +04),
                    new DoublePoint(+02, +05),
                    new DoublePoint(+03, +05),
                    new DoublePoint(+03, +04)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon(
                new[]
                {
                    new DoublePoint(+01, +07),
                    new DoublePoint(+01, +03),
                    new DoublePoint(+08, +03),
                    new DoublePoint(+08, +08),
                    new DoublePoint(+01, +08)
                }.Select(p => new IntPoint(p * Scale)));

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - 6));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area * AreaScaleInverse - 35));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - 10.5));
            Assert.AreEqual(3, solution.Count);
            var polygon = solution[0];
            Assert.AreEqual(6, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+6.0 * Scale, polygon[0].X); Assert.AreEqual(+6.0 * Scale, polygon[0].Y);
            Assert.AreEqual(+8.0 * Scale, polygon[1].X); Assert.AreEqual(+8.0 * Scale, polygon[1].Y);
            Assert.AreEqual(+6.0 * Scale, polygon[2].X); Assert.AreEqual(+8.0 * Scale, polygon[2].Y);
            Assert.AreEqual(+6.0 * Scale, polygon[3].X); Assert.AreEqual(+6.0 * Scale, polygon[3].Y);
            Assert.AreEqual(+4.5 * Scale, polygon[4].X); Assert.AreEqual(+4.5 * Scale, polygon[4].Y);
            Assert.AreEqual(+6.0 * Scale, polygon[5].X); Assert.AreEqual(+3.0 * Scale, polygon[5].Y);

            polygon = solution[1];
            Assert.AreEqual(4, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+04.5 * Scale, polygon[0].X); Assert.AreEqual(+04.5 * Scale, polygon[0].Y);
            Assert.AreEqual(+02.0 * Scale, polygon[1].X); Assert.AreEqual(+07.0 * Scale, polygon[1].Y);
            Assert.AreEqual(+01.0 * Scale, polygon[2].X); Assert.AreEqual(+04.0 * Scale, polygon[2].Y);
            Assert.AreEqual(+03.0 * Scale, polygon[3].X); Assert.AreEqual(+03.0 * Scale, polygon[3].Y);

            polygon = solution[2];
            Assert.AreEqual(4, polygon.Count);
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+02.0 * Scale, polygon[0].X); Assert.AreEqual(+04.0 * Scale, polygon[0].Y);
            Assert.AreEqual(+02.0 * Scale, polygon[1].X); Assert.AreEqual(+05.0 * Scale, polygon[1].Y);
            Assert.AreEqual(+03.0 * Scale, polygon[2].X); Assert.AreEqual(+05.0 * Scale, polygon[2].Y);
            Assert.AreEqual(+03.0 * Scale, polygon[3].X); Assert.AreEqual(+04.0 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestPolygonSimplify()
        {
            var polygon = new Polygon(
                 new[]
                 {
                    new DoublePoint(+00000000, -05000000),
                    new DoublePoint(+00000000, -05000000),
                    new DoublePoint(+05000000, -05000000),
                    new DoublePoint(+05000000, +00000000),
                    new DoublePoint(+05000000, +05000000),
                    new DoublePoint(+00000000, +05000000),
                    new DoublePoint(-05000000, +05000000),
                    new DoublePoint(-05000000, +05000000),
                    new DoublePoint(-05000000, +00000000),
                    new DoublePoint(-05000000, -05000000),
                    new DoublePoint(-05000000, -05000000),
                    new DoublePoint(+00000000, -05000000),
                    new DoublePoint(+00000000, -05000000)
                 }.Select(p => new IntPoint(p * Scale)));

            Assert.AreEqual(100000000000000, polygon.Area * AreaScaleInverse);

            polygon.Simplify();
            Assert.AreEqual(4, polygon.Count);
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(100000000000000, polygon.Area * AreaScaleInverse);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+05000000 * Scale, polygon[0].X); Assert.AreEqual(-05000000 * Scale, polygon[0].Y);
            Assert.AreEqual(+05000000 * Scale, polygon[1].X); Assert.AreEqual(+05000000 * Scale, polygon[1].Y);
            Assert.AreEqual(-05000000 * Scale, polygon[2].X); Assert.AreEqual(+05000000 * Scale, polygon[2].Y);
            Assert.AreEqual(-05000000 * Scale, polygon[3].X); Assert.AreEqual(-05000000 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestEmptyUnion1()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(+0400, +0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            var clip1 = new Polygon();

            var subject = new PolygonPath(new[] { subject1, subject2 });
            var clip = new PolygonPath(clip1);

            Assert.IsTrue(GeometryHelper.NearZero(subject.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.IsTrue(GeometryHelper.NearZero(clip.Area));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area * AreaScaleInverse - (1000000 - 640000)));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestEmptyUnion2()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(-0400, +0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(+0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            Assert.IsTrue(GeometryHelper.NearZero(subject1.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(subject2.Area * AreaScaleInverse - 640000));

            var subject = new PolygonPath(
                new[]
                {
                    subject1,
                    subject2
                });

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, null, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area - (subject1.Area - subject2.Area)));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }

        [TestMethod]
        public void TestEmptyUnion3()
        {
            var subject1 = new Polygon(
                new[]
                {
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, +0500),
                    new DoublePoint(-0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, -0500),
                    new DoublePoint(+0500, +0500)
                }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(
                new[]
                {
                    new DoublePoint(-0400, +0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(-0400, -0400),
                    new DoublePoint(+0300, -0400),
                    new DoublePoint(-0300, -0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(+0400, -0400),
                    new DoublePoint(+0400, +0400)
                }.Select(p => new IntPoint(p * Scale)));

            Assert.IsTrue(GeometryHelper.NearZero(subject1.Area * AreaScaleInverse - 1000000));
            Assert.IsTrue(GeometryHelper.NearZero(subject2.Area * AreaScaleInverse - 640000));

            var subject = new PolygonPath(
                new[]
                {
                    subject1,
                    subject2
                });

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, null, solution));

            Assert.IsTrue(GeometryHelper.NearZero(solution.Area - (subject1.Area - subject2.Area)));
            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            var polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.CounterClockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(+0500 * Scale, polygon[0].X); Assert.AreEqual(+0500 * Scale, polygon[0].Y);
            Assert.AreEqual(-0500 * Scale, polygon[1].X); Assert.AreEqual(+0500 * Scale, polygon[1].Y);
            Assert.AreEqual(-0500 * Scale, polygon[2].X); Assert.AreEqual(-0500 * Scale, polygon[2].Y);
            Assert.AreEqual(+0500 * Scale, polygon[3].X); Assert.AreEqual(-0500 * Scale, polygon[3].Y);

            polygon = solution[1];
            polygonPoints = FromScaledPolygon(polygon);
            Assert.AreEqual(4, polygon.Count);
            Assert.AreEqual(PolygonOrientation.Clockwise, polygon.Orientation);
            Assert.AreEqual(PolygonOrientation.Clockwise, GeometryHelper.GetOrientation(polygonPoints));
            Assert.AreEqual(-0400 * Scale, polygon[0].X); Assert.AreEqual(-0400 * Scale, polygon[0].Y);
            Assert.AreEqual(-0400 * Scale, polygon[1].X); Assert.AreEqual(+0400 * Scale, polygon[1].Y);
            Assert.AreEqual(+0400 * Scale, polygon[2].X); Assert.AreEqual(+0400 * Scale, polygon[2].Y);
            Assert.AreEqual(+0400 * Scale, polygon[3].X); Assert.AreEqual(-0400 * Scale, polygon[3].Y);
        }


        [TestMethod]
        public void MiscShapeTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+01.0, +02.0),
                    new DoublePoint(+03.0, +02.0),
                    new DoublePoint(+04.0, +01.0),
                    new DoublePoint(+05.0, +02.0),
                    new DoublePoint(+07.0, +02.0),
                    new DoublePoint(+06.0, +03.0),
                    new DoublePoint(+07.0, +04.0),
                    new DoublePoint(+05.0, +04.0),
                    new DoublePoint(+04.0, +05.0),
                    new DoublePoint(+03.0, +04.0),
                    new DoublePoint(+01.0, +04.0),
                    new DoublePoint(+02.0, +03.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+01.0, +01.0),
                    new DoublePoint(+03.0, +00.0),
                    new DoublePoint(+05.0, +00.0),
                    new DoublePoint(+07.0, +01.0),
                    new DoublePoint(+08.0, +03.0),
                    new DoublePoint(+08.0, +05.0),
                    new DoublePoint(+07.0, +07.0),
                    new DoublePoint(+05.0, +08.0),
                    new DoublePoint(+03.0, +08.0),
                    new DoublePoint(+01.0, +07.0),
                    new DoublePoint(+00.0, +05.0),
                    new DoublePoint(+00.0, +03.0)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Union, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(12, solution[0].Count);
            solution[0].OrderBottomLeftFirst();
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03.0 * Scale, +00.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +00.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +01.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +03.0 * Scale) - solution[0][3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +05.0 * Scale) - solution[0][4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +07.0 * Scale) - solution[0][5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +08.0 * Scale) - solution[0][6]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03.0 * Scale, +08.0 * Scale) - solution[0][7]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01.0 * Scale, +07.0 * Scale) - solution[0][8]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +05.0 * Scale) - solution[0][9]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +03.0 * Scale) - solution[0][10]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01.0 * Scale, +01.0 * Scale) - solution[0][11]).Length));

            solution = new PolygonPath();

            // Orientation of polygons should not matter, switch to clockwise.
            subject.ReversePolygonOrientations();
            clip.ReversePolygonOrientations();

            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(12, solution[0].Count);
            solution[0].OrderBottomLeftFirst();
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04.0 * Scale, +01.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +02.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +02.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.0 * Scale, +03.0 * Scale) - solution[0][3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +04.0 * Scale) - solution[0][4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +04.0 * Scale) - solution[0][5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04.0 * Scale, +05.0 * Scale) - solution[0][6]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03.0 * Scale, +04.0 * Scale) - solution[0][7]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01.0 * Scale, +04.0 * Scale) - solution[0][8]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02.0 * Scale, +03.0 * Scale) - solution[0][9]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01.0 * Scale, +02.0 * Scale) - solution[0][10]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03.0 * Scale, +02.0 * Scale) - solution[0][11]).Length));
        }

        [TestMethod]
        public void BasicClipTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+08.0, +05.0),
                    new DoublePoint(+12.0, +02.0),
                    new DoublePoint(+12.0, +08.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+00.0, +00.0),
                    new DoublePoint(+10.0, +00.0),
                    new DoublePoint(+10.0, +10.0),
                    new DoublePoint(+00.0, +10.0)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +06.5 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +05.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +03.5 * Scale) - solution[0][2]).Length));

            solution = new PolygonPath();

            // Orientation of polygons should not matter, switch to clockwise.
            subject.ReversePolygonOrientations();
            clip.ReversePolygonOrientations();

            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +06.5 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +05.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +03.5 * Scale) - solution[0][2]).Length));
        }

        [TestMethod]
        public void WithDuplicateAnColinearClipTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+08.0, +05.0),
                    new DoublePoint(+08.0, +05.0),
                    new DoublePoint(+12.0, +02.0),
                    new DoublePoint(+12.0, +08.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+05.0, +00.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+10.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +06.5 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +05.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +03.5 * Scale) - solution[0][2]).Length));
        }

        [TestMethod]
        public void SharedEdgeTestClipTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+00.0, +01.0),
                    new DoublePoint(+02.0, +02.0),
                    new DoublePoint(+00.0, +03.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+00.0, +00.0),
                    new DoublePoint(+05.0, +00.0),
                    new DoublePoint(+05.0, +05.0),
                    new DoublePoint(+00.0, +05.0)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02.0 * Scale, +02.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +03.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +01.0 * Scale) - solution[0][2]).Length));
        }

        [TestMethod]
        public void SharedEdgeAndVertexTestClipTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+05.0, +05.0),
                    new DoublePoint(+05.0, +00.0),
                    new DoublePoint(+02.0, +03.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+00.0, +00.0),
                    new DoublePoint(+05.0, +00.0),
                    new DoublePoint(+05.0, +05.0),
                    new DoublePoint(+00.0, +05.0)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +05.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02.0 * Scale, +03.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +00.0 * Scale) - solution[0][2]).Length));
        }

        [TestMethod]
        public void NoClipTest()
        {
            var subject = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+10.1, +05.0),
                    new DoublePoint(+12.0, +02.0),
                    new DoublePoint(+12.0, +08.0)
                }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(
                new Polygon(new[]
                {
                    new DoublePoint(+00.0, +00.0),
                    new DoublePoint(+10.0, +00.0),
                    new DoublePoint(+10.0, +10.0),
                    new DoublePoint(+00.0, +10.0)
                }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(0, solution.Count);
        }

        [TestMethod]
        public void AllClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+03.0, +05.0),
                new DoublePoint(+08.0, +02.0),
                new DoublePoint(+08.0, +08.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+10.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +08.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03.0 * Scale, +05.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +02.0 * Scale) - solution[0][2]).Length));
        }

        [TestMethod]
        public void SamePolygonClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+10.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+10.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(4, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +10.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +10.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +00.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +00.0 * Scale) - solution[0][3]).Length));
        }

        [TestMethod]
        public void OpenSubjectClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +60.0),
                new DoublePoint(+00.0, +50.0),
                new DoublePoint(+30.0, +20.0),
                new DoublePoint(+60.0, +50.0),
                new DoublePoint(+60.0, +60.0)
            }.Select(p => new IntPoint(p * Scale)))
            {
                IsClosed = false
            });

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+10.0, +50.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+50.0, +00.0),
                new DoublePoint(+50.0, +50.0)
            }.Select(p => new IntPoint(p * Scale))));

            var tree = new PolygonTree();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, tree));

            var solution = PolygonPath.FromTree(tree);

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];

            Assert.AreEqual(3, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +40.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+30.0 * Scale, +20.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+50.0 * Scale, +40.0 * Scale) - polygon[2]).Length));
        }

        [TestMethod]
        public void OpenClipClipTest()
        {
            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +60.0),
                new DoublePoint(+00.0, +50.0),
                new DoublePoint(+30.0, +20.0),
                new DoublePoint(+60.0, +50.0),
                new DoublePoint(+60.0, +60.0)
            }.Select(p => new IntPoint(p * Scale))));

            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+10.0, +50.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+50.0, +00.0),
                new DoublePoint(+50.0, +50.0)
            }.Select(p => new IntPoint(p * Scale)))
            {
                IsClosed = false
            });

            var tree = new PolygonTree();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, tree));
            var solution = PolygonPath.FromTree(tree);

            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(2, polygon.Count);
            polygon.OrderBottomLeftFirst();
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +40.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +50.0 * Scale) - polygon[1]).Length));

            polygon = solution[1];
            Assert.AreEqual(2, polygon.Count);
            polygon.OrderBottomLeftFirst();
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+50.0 * Scale, +40.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+50.0 * Scale, +50.0 * Scale) - polygon[1]).Length));
        }

        [TestMethod]
        public void ClipWithLineTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+10.0, +50.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+50.0, +00.0),
                new DoublePoint(+50.0, +50.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +20.0),
                new DoublePoint(+00.0, +30.0),
                new DoublePoint(+60.0, +30.0),
                new DoublePoint(+60.0, +20.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();

            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +20.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+50.0 * Scale, +20.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+50.0 * Scale, +30.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +30.0 * Scale) - polygon[3]).Length));
        }

        [TestMethod]
        public void OverlappingPolygonClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+12.0, +00.0),
                new DoublePoint(+12.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+10.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(4, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +10.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +10.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +00.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +00.0 * Scale) - solution[0][3]).Length));
        }

        [TestMethod]
        public void ConcaveClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(-020, +020),
                new DoublePoint(+020, +020),
                new DoublePoint(-005, +050),
                new DoublePoint(+020, +080),
                new DoublePoint(-020, +080)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+000, +000),
                new DoublePoint(+100, +000),
                new DoublePoint(+100, +100),
                new DoublePoint(+000, +100)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(2, solution.Count);

            Assert.AreEqual(3, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+020 * Scale, +080 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+000 * Scale, +080 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+000 * Scale, +056 * Scale) - solution[0][2]).Length));

            Assert.AreEqual(3, solution[1].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+000 * Scale, +044 * Scale) - solution[1][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+000 * Scale, +020 * Scale) - solution[1][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+020 * Scale, +020 * Scale) - solution[1][2]).Length));
        }

        [TestMethod]
        public void MultiIntersection1ClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+020, +020),
                new DoublePoint(+120, +020),
                new DoublePoint(+120, +060),
                new DoublePoint(+020, +060)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+000, +000),
                new DoublePoint(+040, +000),
                new DoublePoint(+050, +040),
                new DoublePoint(+060, +000),
                new DoublePoint(+070, +000),
                new DoublePoint(+080, +040),
                new DoublePoint(+090, +000),
                new DoublePoint(+100, +000),
                new DoublePoint(+100, +100),
                new DoublePoint(+000, +100)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            Assert.AreEqual(10, solution[0].Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+100 * Scale, +060 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+020 * Scale, +060 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+020 * Scale, +020 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+045 * Scale, +020 * Scale) - solution[0][3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+050 * Scale, +040 * Scale) - solution[0][4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+055 * Scale, +020 * Scale) - solution[0][5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+075 * Scale, +020 * Scale) - solution[0][6]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+080 * Scale, +040 * Scale) - solution[0][7]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+085 * Scale, +020 * Scale) - solution[0][8]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+100 * Scale, +020 * Scale) - solution[0][9]).Length));
        }

        [TestMethod]
        public void MultiIntersection2ClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+13.0, +02.0),
                new DoublePoint(+15.0, +03.0),
                new DoublePoint(+15.0, +06.0),
                new DoublePoint(+16.0, +09.0),
                new DoublePoint(+13.0, +13.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+19.0, +08.0),
                new DoublePoint(+10.0, +01.0),
                new DoublePoint(+10.0, +03.0),
                new DoublePoint(+03.0, +03.0),
                new DoublePoint(+03.0, +15.0),
                new DoublePoint(+17.0, +10.0),
                new DoublePoint(+08.0, +10.0),
                new DoublePoint(+08.0, +08.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+15.25 * Scale, +10.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+14.6097561 * Scale, +10.8536585 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +11.42857145 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +10.0 * Scale) - polygon[3]).Length));

            polygon = solution[1];
            Assert.AreEqual(5, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+15.0 * Scale, +4.8888889 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+15.0 * Scale, +06.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+15.6666667 * Scale, +08.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +08.0 * Scale) - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +3.3333333 * Scale) - polygon[4]).Length));
        }

        [TestMethod]
        public void MultiIntersection3ClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+06.0, +11.0),
                new DoublePoint(+22.0, +11.0),
                new DoublePoint(+22.0, +01.0),
                new DoublePoint(+12.0, +01.0),
                new DoublePoint(+12.0, +06.0),
                new DoublePoint(+06.0, +06.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+19.0, +08.0),
                new DoublePoint(+10.0, +01.0),
                new DoublePoint(+10.0, +03.0),
                new DoublePoint(+03.0, +03.0),
                new DoublePoint(+03.0, +15.0),
                new DoublePoint(+17.0, +10.0),
                new DoublePoint(+08.0, +10.0),
                new DoublePoint(+08.0, +08.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.AreEqual(9, solution[0].Count);

            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+19.0 * Scale, +08.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +08.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+08.0 * Scale, +10.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+17.0 * Scale, +10.0 * Scale) - solution[0][3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+14.2 * Scale, +11.0 * Scale) - solution[0][4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.0 * Scale, +11.0 * Scale) - solution[0][5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.0 * Scale, +06.0 * Scale) - solution[0][6]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+12.0 * Scale, +06.0 * Scale) - solution[0][7]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+12.0 * Scale, +2.5555556 * Scale) - solution[0][8]).Length));
        }

        [TestMethod]
        public void SingleMidVertexClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+03.0, +06.0),
                new DoublePoint(+05.0, +03.0),
                new DoublePoint(+01.0, +03.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +06.0),
                new DoublePoint(+06.0, +06.0),
                new DoublePoint(+06.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(0, solution.Count);
        }

        [TestMethod]
        public void SingleCornerVertexClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+06.0, +10.0),
                new DoublePoint(+06.0, +13.0),
                new DoublePoint(+08.0, +13.0),
                new DoublePoint(+08.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +06.0),
                new DoublePoint(+06.0, +06.0),
                new DoublePoint(+06.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(0, solution.Count);
        }

        [TestMethod]
        public void SharedEdgeVertexClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+06.0, +09.0),
                new DoublePoint(+09.0, +09.0),
                new DoublePoint(+09.0, +07.0),
                new DoublePoint(+06.0, +07.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +06.0),
                new DoublePoint(+06.0, +06.0),
                new DoublePoint(+06.0, +10.0),
                new DoublePoint(+00.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(0, solution.Count);
        }

        [TestMethod]
        public void NoSubjectInteriorVertexClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +04.0),
                new DoublePoint(+04.0, +04.0),
                new DoublePoint(+04.0, +00.0),
                new DoublePoint(+00.0, +00.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(-02.0, +01.0),
                new DoublePoint(-02.0, +03.0),
                new DoublePoint(+06.0, +03.0),
                new DoublePoint(+06.0, +01.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04.0 * Scale, +03.0 * Scale) - solution[0][0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +03.0 * Scale) - solution[0][1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00.0 * Scale, +01.0 * Scale) - solution[0][2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04.0 * Scale, +01.0 * Scale) - solution[0][3]).Length));
        }

        [TestMethod]
        public void SelfIntersectingSubjectClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+06.0, +02.0),
                new DoublePoint(+08.0, +04.0),
                new DoublePoint(+10.0, +04.0),
                new DoublePoint(+11.0, +03.0),
                new DoublePoint(+11.0, +01.0),
                new DoublePoint(+13.0, +01.0),
                new DoublePoint(+13.0, +06.0),
                new DoublePoint(+10.0, +08.0),
                new DoublePoint(+09.0, +08.0),
                new DoublePoint(+09.0, +03.0),
                new DoublePoint(+08.0, +05.0),
                new DoublePoint(+02.0, +03.0),
                new DoublePoint(+02.0, +08.0),
                new DoublePoint(+07.0, +08.0),
                new DoublePoint(+05.0, +06.0),
                new DoublePoint(+06.0, +04.0),
                new DoublePoint(+03.0, +04.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+14.0, +08.0),
                new DoublePoint(+14.0, +11.0),
                new DoublePoint(+03.0, +11.0),
                new DoublePoint(+03.0, +09.0),
                new DoublePoint(+08.0, +07.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(2, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +07.1666667 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.8 * Scale, +07.4666667 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +08.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +08.0 * Scale) - polygon[3]).Length));

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.5714286 * Scale, +07.5714286 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +08.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.5 * Scale, +08.0 * Scale) - polygon[2]).Length));
        }

        [TestMethod]
        public void BothSelfIntersectingSubjectClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +00.0),
                new DoublePoint(+04.0, +04.0),
                new DoublePoint(+04.0, +14.0),
                new DoublePoint(+07.0, +14.0),
                new DoublePoint(+07.0, +16.0),
                new DoublePoint(+00.0, +16.0),
                new DoublePoint(+00.0, +13.0),
                new DoublePoint(+07.0, +07.0),
                new DoublePoint(+14.0, +14.0),
                new DoublePoint(+11.0, +14.0),
                new DoublePoint(+11.0, +00.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+07.0, +09.0),
                new DoublePoint(+12.0, +09.0),
                new DoublePoint(+07.0, +14.0),
                new DoublePoint(+07.0, +13.0),
                new DoublePoint(+10.0, +13.0),
                new DoublePoint(+10.0, +15.0),
                new DoublePoint(+15.0, +15.0),
                new DoublePoint(+15.0, +13.0),
                new DoublePoint(+11.0, +13.0),
                new DoublePoint(+08.0, +16.0),
                new DoublePoint(+17.0, +16.0),
                new DoublePoint(+17.0, +12.0),
                new DoublePoint(+11.0, +12.0),
                new DoublePoint(+11.0, +11.0),
                new DoublePoint(+12.0, +12.0),
                new DoublePoint(+13.0, +11.0),
                new DoublePoint(+19.0, +11.0),
                new DoublePoint(+19.0, +17.0),
                new DoublePoint(+06.0, +17.0),
                new DoublePoint(+06.0, +12.0),
                new DoublePoint(+07.0, +12.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(4, solution.Count);

            // Order by Y 
            solution = new PolygonPath(solution.OrderBy(poly => poly.Min(point => point.Y)));

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +09.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +09.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +10.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.5 * Scale, +10.5 * Scale) - polygon[3]).Length));

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(3, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +11.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+12.0 * Scale, +12.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +12.0 * Scale) - polygon[2]).Length));

            polygon = solution[2];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +13.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +13.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+14.0 * Scale, +14.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +14.0 * Scale) - polygon[3]).Length));

            polygon = solution[3];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.0 * Scale, +14.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +14.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +16.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+06.0 * Scale, +16.0 * Scale) - polygon[3]).Length));
        }

        [TestMethod]
        public void IntermediateHorizontal1Test()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00000000, +00000000),
                new DoublePoint(+01000000, +01000000),
                new DoublePoint(+03000000, +01000000),
                new DoublePoint(+04000000, +00000000),
                new DoublePoint(+04000000, +02000000),
                new DoublePoint(+00000000, +02000000)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00500000, +00000000),
                new DoublePoint(+03500000, +00000000),
                new DoublePoint(+03500000, +03000000),
                new DoublePoint(+00500000, +03000000)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();

            Assert.AreEqual(6, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00500000, +00500000) * Scale - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01000000, +01000000) * Scale - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03000000, +01000000) * Scale - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03500000, +00500000) * Scale - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03500000, +02000000) * Scale - polygon[4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00500000, +02000000) * Scale - polygon[5]).Length));
        }

        [TestMethod]
        public void IntermediateHorizontal2Test()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00000000, +00000000),
                new DoublePoint(+01000000, +01000000),
                new DoublePoint(+03000000, +01000000),
                new DoublePoint(+04000000, +02000000),
                new DoublePoint(+00000000, +02000000)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00500000, +00000000),
                new DoublePoint(+03500000, +00000000),
                new DoublePoint(+03500000, +03000000),
                new DoublePoint(+00500000, +03000000)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();

            Assert.AreEqual(6, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00500000, +00500000) * Scale - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+01000000, +01000000) * Scale - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03000000, +01000000) * Scale - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03500000, +01500000) * Scale - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03500000, +02000000) * Scale - polygon[4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+00500000, +02000000) * Scale - polygon[5]).Length));
        }

        [TestMethod]
        public void MultipleWindTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00000000, +00000000),
                new DoublePoint(+06000000, +00000000),
                new DoublePoint(+06000000, +04000000),
                new DoublePoint(+05000000, +03000000),
                new DoublePoint(+04000000, +04000000),
                new DoublePoint(+03000000, +03000000),
                new DoublePoint(+02000000, +04000000),
                new DoublePoint(+01000000, +03000000),
                new DoublePoint(+00000000, +04000000)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+02000000, +00000000),
                new DoublePoint(+04000000, +00000000),
                new DoublePoint(+04000000, +04000000),
                new DoublePoint(+02000000, +04000000)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();

            Assert.AreEqual(5, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02000000, +00000000) * Scale - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04000000, +00000000) * Scale - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+04000000, +04000000) * Scale - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+03000000, +03000000) * Scale - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02000000, +04000000) * Scale - polygon[4]).Length));
        }

        [TestMethod]
        public void DiamondSquareClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+02.0, +02.0),
                new DoublePoint(+12.0, +02.0),
                new DoublePoint(+12.0, +12.0),
                new DoublePoint(+02.0, +12.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +07.0),
                new DoublePoint(+07.0, +00.0),
                new DoublePoint(+14.0, +07.0),
                new DoublePoint(+07.0, +14.0)
            }.Select(p => new IntPoint(p * Scale))));

            var solution = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, solution));

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(8, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +02.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +02.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+12.0 * Scale, +05.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+12.0 * Scale, +09.0 * Scale) - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +12.0 * Scale) - polygon[4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +12.0 * Scale) - polygon[5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02.0 * Scale, +09.0 * Scale) - polygon[6]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+02.0 * Scale, +05.0 * Scale) - polygon[7]).Length));
        }

        [TestMethod]
        public void ThreeDiamondsClipTest()
        {
            var subject1 = new Polygon(new[]
            {
                new DoublePoint(+05.0, +05.0),
                new DoublePoint(+10.0, +00.0),
                new DoublePoint(+15.0, +05.0),
                new DoublePoint(+10.0, +10.0)
            }.Select(p => new IntPoint(p * Scale)));

            var subject2 = new Polygon(new[]
            {
                new DoublePoint(+07.0, +05.0),
                new DoublePoint(+10.0, +02.0),
                new DoublePoint(+13.0, +05.0),
                new DoublePoint(+10.0, +08.0)
            }.Select(p => new IntPoint(p * Scale)));

            var subject3 = new Polygon(new[]
            {
                new DoublePoint(+09.0, +05.0),
                new DoublePoint(+10.0, +04.0),
                new DoublePoint(+11.0, +05.0),
                new DoublePoint(+10.0, +06.0)
            }.Select(p => new IntPoint(p * Scale)));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+05.0, +00.0),
                new DoublePoint(+15.0, +00.0),
                new DoublePoint(+15.0, +10.0),
                new DoublePoint(+05.0, +10.0)
            }.Select(p => new IntPoint(p * Scale))));

            var subject = new PolygonPath(
                new[]
                {
                    subject1, subject2, subject3
                });

            var tree = new PolygonTree();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, tree));

            var solution = PolygonPath.FromTree(tree);

            Assert.AreEqual(3, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(polygon.Orientation, PolygonOrientation.CounterClockwise);
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +00.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+15.0 * Scale, +05.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +10.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+05.0 * Scale, +05.0 * Scale) - polygon[3]).Length));

            polygon = solution[1];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(polygon.Orientation, PolygonOrientation.Clockwise);
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +02.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +05.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +08.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +05.0 * Scale) - polygon[3]).Length));

            polygon = solution[2];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(polygon.Orientation, PolygonOrientation.CounterClockwise);
            Assert.AreEqual(4, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +04.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +05.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.0 * Scale, +06.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+09.0 * Scale, +05.0 * Scale) - polygon[3]).Length));
        }

        [TestMethod]
        public void BothOpenClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+10.0, +06.0),
                new DoublePoint(+05.0, +06.0),
                new DoublePoint(+00.0, +00.0)
            }.Select(p => new IntPoint(p * Scale)))
            {
                IsClosed = false
            });

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+00.0, +03.0),
                new DoublePoint(+09.0, +03.0),
                new DoublePoint(+09.0, +05.0)
            }.Select(p => new IntPoint(p * Scale))));

            var tree = new PolygonTree();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, tree));

            var solution = PolygonPath.FromTree(tree);

            // The solution should be a polygon with a single line segment.
            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            Assert.AreEqual(2, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+3.0681818 * Scale, +3.6818182 * Scale) - new IntPoint(polygon[0])).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+2.5 * Scale, +3.0 * Scale) - new IntPoint(polygon[1])).Length));
        }

        [TestMethod]
        public void ShareHorizontalRightBoundClipTest()
        {
            var subject = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+07.0, +07.0),
                new DoublePoint(+11.0, +00.0),
                new DoublePoint(+11.0, +14.0),
                new DoublePoint(+14.0, +14.0)
            }.Select(p => new IntPoint(p * Scale))));

            var clip = new PolygonPath(new Polygon(new[]
            {
                new DoublePoint(+06.0, +01.0),
                new DoublePoint(+15.0, +01.0),
                new DoublePoint(+15.0, +13.0),
                new DoublePoint(+06.0, +13.0)
            }.Select(p => new IntPoint(p * Scale))));

            var tree = new PolygonPath();
            Assert.IsTrue(new Clipper.Clipper().Execute(ClipOperation.Intersection, subject, clip, tree));

            var solution = new PolygonPath(tree);

            Assert.AreEqual(1, solution.Count);

            var polygon = solution[0];
            polygon.OrderBottomLeftFirst();
            Assert.AreEqual(polygon.Orientation, PolygonOrientation.CounterClockwise);
            Assert.AreEqual(7, polygon.Count);
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+10.42857145 * Scale, +01.0 * Scale) - polygon[0]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +01.0 * Scale) - polygon[1]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +11.0 * Scale) - polygon[2]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+13.0 * Scale, +13.0 * Scale) - polygon[3]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +13.0 * Scale) - polygon[4]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+11.0 * Scale, +11.0 * Scale) - polygon[5]).Length));
            Assert.IsTrue(GeometryHelper.NearZero((new IntPoint(+07.0 * Scale, +07.0 * Scale) - polygon[6]).Length));
        }

        private static IList<DoublePoint> FromScaledPolygon(Polygon polygon)
        {
            return polygon
                .Select(p => new DoublePoint(
                    p.X * ScaleInverse,
                    p.Y * ScaleInverse))
                .ToList();
        }
    }
}
