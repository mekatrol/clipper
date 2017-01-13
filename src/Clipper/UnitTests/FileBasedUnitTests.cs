using System;
using System.Collections.Generic;
using System.Linq;
using Clipper;
using ClipperLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class FileBasedUnitTests
    {
        [TestMethod]
        [Ignore]
        public void NewClipperFileBasedTest()
        {
            var testData = LoadTestHelper.LoadFromFile("TestData/tests.txt");

            foreach (var test in testData.Values)
            {
                var clipper = new Clipper.Clipper();

                clipper.AddPath(test.Subjects, PolygonKind.Subject);
                clipper.AddPath(test.Clips, PolygonKind.Clip);

                var solution = new PolygonTree();
                Assert.IsTrue(clipper.Execute(test.ClipOperation, solution, false, test.FillType));

                var path = new PolygonPath(solution.AllPolygons.Select(n => n.Polygon).ToList());

                // TODO: reinclude these tests once test data is verified.
                var ignoreTestNumbers = new[] { 36, 38, 39, 44, 46, 48, 51, 52, 59, 64, 67, 69 };
                if (ignoreTestNumbers.Contains(test.TestNumber)) continue;

                Assert.AreEqual(test.Solution.Count, path.Count, $"{test.TestNumber}: {test.Caption}");

                // Match points, THIS IS DESTRUCTIVE TO BOTH THE TEST DATA AND RESULT DATA.
                Assert.IsTrue(AreSame(test, path));

                // If we had an exact match then both solutions should now be empty.
                Assert.AreEqual(0, test.Solution.Count, $"{test.TestNumber}: {test.Caption}");
                Assert.AreEqual(0, path.Count, $"{test.TestNumber}: {test.Caption}");
            }
        }

        [TestMethod]
        [Ignore]
        public void OriginalClipperFileBasedTest()
        {
            var testData = LoadTestHelper.LoadFromFile("TestData/tests.txt");

            foreach (var test in testData.Values)
            {
                var subjects = test.Subjects.ToOriginal();
                var clips = test.Clips.ToOriginal();

                var clipper = new ClipperLib.Clipper();
                clipper.AddPaths(subjects, PolyType.ptSubject, true);
                clipper.AddPaths(clips, PolyType.ptClip, true);

                var originalSolution = new List<List<ClipperLib.IntPoint>>();
                var clipType = (ClipType)Enum.Parse(typeof(ClipType), $"ct{test.ClipOperation}", true);
                var fillType = (PolyFillType)Enum.Parse(typeof(PolyFillType), $"pft{test.FillType}", true);
                Assert.IsTrue(clipper.Execute(clipType, originalSolution, fillType));
                Assert.AreEqual(test.Solution.Count, originalSolution.Count, test.Caption);

                var solution = originalSolution.ToNew();

                // TODO: reinclude these tests once test data is verified.
                var ignoreTestNumbers = new[] { 36, 38, 39, 44, 46, 48, 51, 52, 59, 64, 67, 69 };
                if (ignoreTestNumbers.Contains(test.TestNumber)) continue;

                Assert.AreEqual(test.Solution.Count, solution.Count, $"{test.TestNumber}: {test.Caption}");

                // Match points, THIS IS DESTRUCTIVE TO BOTH THE TEST DATA AND RESULT DATA.
                Assert.IsTrue(AreSame(test, solution));

                // If we had an exact match then both solutions should now be empty.
                Assert.AreEqual(0, test.Solution.Count, $"{test.TestNumber}: {test.Caption}");
                Assert.AreEqual(0, solution.Count, $"{test.TestNumber}: {test.Caption}");
            }
        }

        /// <summary>
        /// Test to see if the two paths are the same in that:
        /// 1. They have the same polygon count.
        /// 2. There are matching polygons in that:
        ///    a. Same point count.
        ///    b. Same orientation.
        /// </summary>
        public static bool AreSame(ClipExecutionData test, PolygonPath path2)
        {
            if (test.Solution.Count != path2.Count) return false;

            for (var i = 0; i < path2.Count; i++)
            {
                var polygon1 = path2[i];

                // Order to make comparison easier.
                polygon1.OrderBottomLeftFirst();

                for (var j = 0; j < test.Solution.Count; j++)
                {
                    var polygon2 = test.Solution[j];

                    // Vertex counts need to match
                    if (polygon1.Count != polygon2.Count) continue;

                    // Orientations need to match
                    if (polygon1.Orientation != polygon2.Orientation) continue;

                    // Count the number of points that match in order across the polygons.
                    // Given both polygons are ordered by bottom left, then 
                    // points at each index should match if they are the same polygon.
                    var pointMatchCount = polygon1
                        .Where((point, k) => point == polygon2[k])
                        .Count();

                    // Did the point match count equal the vertex count?
                    if (pointMatchCount != polygon1.Count) continue;

                    // This is a matching polygon so remove from both solutions
                    // and decrement outer loop index.
                    path2.RemoveAt(i--);
                    test.Solution.RemoveAt(j);

                    // break from inner loop to outer loop
                    break;
                }
            }

            return true;
        }
    }
}
