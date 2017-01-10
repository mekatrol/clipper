using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace PerformanceTests
{
    [TestClass]
    public class PerformanceRunnerTests
    {
        /* ***************************************************************************************
         *  NOTE:
         *  
         *  This is not a true performance test. Completion time varies each 
         *  time the test is executed, sometimes the legacy code is faster and 
         *  other times the refactored code is faster. 
         *  
         *  The intent of this test was to ensure that during refactoring the code 
         *  did not trend toward poorer performance. 
         *  
         *  A threshold tolerance of 105% is used to validate that on average the 
         *  refactored code is not performing worse. 
         *
         *****************************************************************************************/

        // Scale to convert floating points values to integer point values.
        private const double Scale = 1E7;

        // The number of times each set is executed.
        private const int TestIterationCount = 100;

        // The execution time tolerance.
        private const double ExecutionTimeThresholdTolerancePercentage = 1.05;

        /// <summary>
        /// 
        /// This test uses two simple polygons to generate random data, square and star, where:
        /// 
        ///     1. The square and star are scaled randomly to provide range in the polygon sizes.
        ///     2. The square and star are selected randomly as the subject and clip polygons.
        ///     3. The clip operation for these polygons is randomly chosen.
        ///     4. The random data is pre-generated so that both the legacy and 
        ///        refactored clippers operate on the same data values and operations.
        /// 
        /// </summary>
        [TestMethod]
        public void SimplePolygonTest()
        {
            var paths = TestPolygons.LoadPaths("SimplePolygons.json");

            var legacyClipperExecutionTime = ExecuteLegacyClipper(TestIterationCount, paths);
            var refactoredClipperExecutionTime = ExecuteRefactoredClipper(TestIterationCount, paths);

            var pct = refactoredClipperExecutionTime / (double)legacyClipperExecutionTime;

            Assert.IsTrue(pct <= ExecutionTimeThresholdTolerancePercentage);
        }

        /// <summary>
        /// 
        /// This test uses two complex polygons complex1 and complex2 (self intersecting with holes)
        /// to generate random data, where:
        /// 
        ///     1. The complex polygons are scaled randomly to provide range in the polygon sizes.
        ///     2. The complex polygons are selected randomly as the subject and clip polygons.
        ///     3. The clip operation for these polygons is randomly chosen.
        ///     4. The random data is pre-generated so that both the legacy and 
        ///        refactored clippers operate on the same data values and operations.
        /// 
        /// </summary>
        [TestMethod]
        public void ComplexPolygonTest()
        {
            var paths = TestPolygons.LoadPaths("ComplexPolygons.json");

            var legacyClipperExecutionTime = ExecuteLegacyClipper(TestIterationCount, paths);
            var refactoredClipperExecutionTime = ExecuteRefactoredClipper(TestIterationCount, paths);

            var pct = refactoredClipperExecutionTime / (double)legacyClipperExecutionTime;
             
            Assert.IsTrue(pct <= ExecutionTimeThresholdTolerancePercentage);
        }

        /// <summary>
        /// 
        /// This test uses two large polygons from random vertex data, where:
        /// 
        ///     1. The polygons are scaled randomly to provide range in the polygon sizes.
        ///     2. The polygons are selected randomly as the subject and clip polygons.
        ///     3. The clip operation for these polygons is randomly chosen.
        ///     4. The random data is pre-generated so that both the legacy and 
        ///        refactored clippers operate on the same data values and operations.
        /// 
        /// </summary>
        [TestMethod]
        public void LargePolygonTest()
        {
            var paths = TestPolygons.LoadPaths("LargePolygons.json");

            var legacyClipperExecutionTime = ExecuteLegacyClipper(TestIterationCount, paths);
            var refactoredClipperExecutionTime = ExecuteRefactoredClipper(TestIterationCount, paths);

            var pct = refactoredClipperExecutionTime / (double)legacyClipperExecutionTime;

             Assert.IsTrue(pct <= ExecutionTimeThresholdTolerancePercentage);
        }

        public static long ExecuteRefactoredClipper(int testIterationCount, List<ClipExecutionData> executionData)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < testIterationCount; i++)
            {
                foreach (var clipPath in executionData)
                {
                    var subject = new Clipper.PolygonPath(
                        clipPath
                            .Subject
                                .Select(poly => new Clipper.Polygon(poly.Select(pt =>
                                    new Clipper.IntPoint(
                                        pt.X * Scale,
                                        pt.Y * Scale)))));

                    var clip = new Clipper.PolygonPath(
                        clipPath
                            .Clip
                                .Select(poly => new Clipper.Polygon(poly.Select(pt =>
                                    new Clipper.IntPoint(
                                        pt.X * Scale,
                                        pt.Y * Scale)))));

                    var solution = new Clipper.PolyTree();
                    var clipper = new Clipper.Clipper();

                    clipper.AddPaths(subject, Clipper.PolyType.Subject);
                    clipper.AddPaths(clip, Clipper.PolyType.Clip);

                    // Convert performance test library operation enum to Clipper operation enum.
                    var operation = (Clipper.ClipOperation)Enum.Parse(typeof(Clipper.ClipOperation), clipPath.Operation.ToString(), true);
                    Assert.IsTrue(clipper.Execute(operation, solution));
                }
            }

            stopwatch.Stop();

            return stopwatch.Elapsed.Ticks;
        }

        public static long ExecuteLegacyClipper(int testIterationCount, List<ClipExecutionData> executionData)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (var i = 0; i < testIterationCount; i++)
            {
                foreach (var clipPath in executionData)
                {
                    var subject = new List<List<ClipperLib.IntPoint>>(
                        clipPath
                            .Subject
                                .Select(poly => new List<ClipperLib.IntPoint>(poly.Select(pt =>
                                    new ClipperLib.IntPoint(
                                        pt.X * Scale,
                                        pt.Y * Scale)))));

                    var clip = new List<List<ClipperLib.IntPoint>>(
                        clipPath
                            .Clip
                                .Select(poly => new List<ClipperLib.IntPoint>(poly.Select(pt =>
                                    new ClipperLib.IntPoint(
                                        pt.X * Scale,
                                        pt.Y * Scale)))));

                    var solution = new ClipperLib.PolyTree();
                    var clipper = new ClipperLib.Clipper();

                    clipper.AddPaths(subject, ClipperLib.PolyType.ptSubject, true);
                    clipper.AddPaths(clip, ClipperLib.PolyType.ptClip, true);

                    // Convert performance test library operation enum to ClipperLib operation enum.
                    var operation = (ClipperLib.ClipType)Enum.Parse(typeof(ClipperLib.ClipType), $"ct{clipPath.Operation}", true);
                    Assert.IsTrue(clipper.Execute(operation, solution));
                }
            }

            stopwatch.Stop();

            return stopwatch.Elapsed.Ticks;
        }
    }
}
