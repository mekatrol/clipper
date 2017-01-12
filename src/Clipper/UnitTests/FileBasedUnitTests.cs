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
        public void LoadedTest()
        {
            var testData = LoadTestHelper.LoadFromFile("TestData/tests.txt");
            LoadTestHelper.SaveToFile("TestData/tests.txt", testData.Values);

            foreach (var test in testData.Values)
            {
                var clipper = new Clipper.Clipper();

                clipper.AddPaths(test.Subjects, PolygonKind.Subject);
                clipper.AddPaths(test.Clips, PolygonKind.Clip);

                var solution = new PolygonTree();
                Assert.IsTrue(clipper.Execute(test.ClipOperation, solution, test.FillType));

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
        public void Number36Test()
        {
            var testData = LoadTestHelper.LoadFromFile("TestData/tests.txt");
            var test = testData[36];

            var subjects = test
                .Subjects
                    .Select(subject => subject.Select(s => new ClipperLib.IntPoint(s.X, s.Y)).ToList())
                    .ToList();

            var clips = test
                .Clips
                    .Select(subject => subject.Select(s => new ClipperLib.IntPoint(s.X, s.Y)).ToList())
                    .ToList();

            var clipper1 = new ClipperLib.Clipper();
            clipper1.AddPaths(subjects, PolyType.ptSubject, true);
            clipper1.AddPaths(clips, PolyType.ptClip, true);

            var solution1 = new List<List<ClipperLib.IntPoint>>();
            var clipType = (ClipType)Enum.Parse(typeof(ClipType), $"ct{test.ClipOperation}", true);
            var fillType = (PolyFillType)Enum.Parse(typeof(PolyFillType), $"pft{test.FillType}", true);
            Assert.IsTrue(clipper1.Execute(clipType, solution1, fillType));
            Assert.AreEqual(test.Solution.Count, solution1.Count, test.Caption);

            var clipper2 = new Clipper.Clipper();
            clipper2.AddPaths(test.Subjects, PolygonKind.Subject);
            clipper2.AddPaths(test.Clips, PolygonKind.Clip);

            var solution2 = new PolygonTree();
            Assert.IsTrue(clipper2.Execute(test.ClipOperation, solution2, test.FillType));

            var path = solution2.AllPolygons;

            Assert.AreEqual(test.Solution.Count, path.Count, test.Caption);
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
