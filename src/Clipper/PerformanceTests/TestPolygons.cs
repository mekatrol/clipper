using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PerformanceTests
{
    public static class TestPolygons
    {
        public static List<ClipExecutionData> LoadPaths(string name)
        {
            var filename = $"TestData\\{name}";

            using (var r = new StreamReader(filename))
            {
                using (JsonReader reader = new JsonTextReader(r))
                {
                    var serializer = new JsonSerializer();
                    return serializer.Deserialize<List<ClipExecutionData>>(reader);
                }
            }

            //return JsonConvert.DeserializeObject<List<ClipExecutionData>>(File.ReadAllText(filename));
        }

        public static List<ClipExecutionData> BuildRandomSimplePaths()
        {
            var square = new[]
            {
                new Point(1, 0),
                new Point(5, 0),
                new Point(5, 4),
                new Point(1, 4)
            };

            var star = new[]
            {
                new Point(0, 1),
                new Point(2, 1),
                new Point(3, 0),
                new Point(4, 1),
                new Point(6, 1),
                new Point(5, 2),
                new Point(6, 3),
                new Point(4, 3),
                new Point(3, 4),
                new Point(2, 3),
                new Point(0, 3),
                new Point(1, 2)
            };

            var r = new Random((int)DateTime.Now.Ticks);

            const int pathCount = 10000;

            var scales = Enumerable
                .Range(0, pathCount)
                .Select(i => r.Next(0, 100000) / 100.0)
                .ToArray();

            var probabilities = Enumerable
                .Range(0, pathCount)
                .Select(i => r.NextDouble())
                .ToArray();

            var stars = new List<List<Point>>();
            var squares = new List<List<Point>>();

            foreach (var scale in scales)
            {
                stars.Add(star.Select(s => s * scale).ToList());
                squares.Add(square.Select(s => s * scale).ToList());
            }

            var paths = Enumerable
                .Range(0, pathCount)
                .Select(i =>
                    new ClipExecutionData
                    {
                        Subject = new List<List<Point>>(new[] {
                            probabilities[i] >= 0.5
                            ? stars[i]
                            : squares [i]
                        }),
                        Clip = new List<List<Point>>(new[] {
                            probabilities[i] >= 0.5
                            ? squares[i]
                            : stars [i]
                        }),
                        Operation =
                            probabilities[i] >= 0.75 ? ClipOperation.Xor :
                            probabilities[i] >= 0.50 ? ClipOperation.Union :
                            probabilities[i] >= 0.25 ? ClipOperation.Intersection : ClipOperation.Difference
                    })
                .ToList();

            var json = JsonConvert.SerializeObject(paths, Formatting.Indented);
            File.WriteAllText("SimplePolygons.json", json);

            return paths;
        }

        public static List<ClipExecutionData> BuildRandomComplexPaths()
        {
            var rect = new[]
            {
                new Point(1, 2),
                new Point(9, 2),
                new Point(9, 8),
                new Point(1, 8)
            };

            var rectHole = new[]
            {
                new Point(2, 3),
                new Point(2, 7),
                new Point(8, 7),
                new Point(8, 3)
            };

            var selfIntersect = new[]
            {
                new Point(1, 1),
                new Point(9, 9),
                new Point(1, 9),
                new Point(9, 1)
            };

            var selfIntersectHole1 = new[]
            {
                new Point(3, 2),
                new Point(5, 4),
                new Point(7, 2)
            };

            var selfIntersectHole2 = new[]
            {
                new Point(5, 6),
                new Point(3, 8),
                new Point(7, 8)
            };

            var r = new Random((int)DateTime.Now.Ticks);

            const int pathCount = 10000;

            var scales = Enumerable
                .Range(0, pathCount)
                .Select(i => r.Next(0, 100000) / 100.0)
                .ToArray();

            var probabilities = Enumerable
                .Range(0, pathCount)
                .Select(i => r.NextDouble())
                .ToArray();

            var path1 = new List<List<Point>>();
            var path2 = new List<List<Point>>();

            foreach (var scale in scales)
            {
                path1.Add(selfIntersect.Select(s => s * scale).ToList());
                path1.Add(selfIntersectHole1.Select(s => s * scale).ToList());
                path1.Add(selfIntersectHole2.Select(s => s * scale).ToList());

                path2.Add(rect.Select(s => s * scale).ToList());
                path2.Add(rectHole.Select(s => s * scale).ToList());
            }

            var paths = Enumerable
                .Range(0, pathCount)
                .Select(i =>
                    new ClipExecutionData
                    {
                        Subject = new List<List<Point>>(new[] {
                            probabilities[i] >= 0.5
                            ? path1[i]
                            : path2 [i]
                        }),
                        Clip = new List<List<Point>>(new[] {
                            probabilities[i] >= 0.5
                            ? path2[i]
                            : path1 [i]
                        }),
                        Operation =
                            probabilities[i] >= 0.75 ? ClipOperation.Xor :
                            probabilities[i] >= 0.50 ? ClipOperation.Union :
                            probabilities[i] >= 0.25 ? ClipOperation.Intersection : ClipOperation.Difference
                    })
                .ToList();

            var json = JsonConvert.SerializeObject(paths, Formatting.Indented);
            File.WriteAllText("ComplexPolygons.json", json);

            return paths;
        }

        public static List<ClipExecutionData> BuildRandomLargePaths()
        {
            var r = new Random((int)DateTime.Now.Ticks);

            // 100 polygon paths
            const int pathCount = 100;

            var scales = Enumerable
                .Range(0, pathCount)
                .Select(i => r.Next(0, 100000) / 100.0)
                .ToArray();

            var probabilities = Enumerable
                .Range(0, pathCount)
                .Select(i => r.NextDouble())
                .ToArray();

            var binary = File.ReadAllBytes("TestData\\aust.bin");
            var polygons = new List<List<Point>>();
            using (var stream = new BinaryReader(new MemoryStream(binary)))
            {
                var polygonCount = stream.ReadInt32();

                for (var i = 0; i < polygonCount; ++i)
                {
                    var vertexCount = stream.ReadInt32();
                    var polygon = new List<Point>(vertexCount);

                    for (var j = 0; j < vertexCount; ++j)
                    {
                        var x = stream.ReadSingle();
                        var y = stream.ReadSingle();
                        polygon.Add(new Point(x, y));
                    }

                    polygons.Add(polygon);
                }
            }

            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;

            foreach (var polygon in polygons)
            {
                foreach (var point in polygon)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            var australia = polygons;
            var australiaInverted = new List<List<Point>>();

            foreach (var polygon in polygons)
            {
                var invertedPolygon = new List<Point>();

                for (var j = 0; j < polygon.Count; j++)
                {
                    invertedPolygon.Add(new Point(polygon[j].X, minY + (maxY - polygon[j].Y)));
                }

                australiaInverted.Add(invertedPolygon);
            }

            var paths = scales.Select((scale, i) => new ClipExecutionData
            {
                Subject = australia
                    .Select(polygon => 
                        polygon
                            .Select(point => point * scale)
                            .ToList())
                    .ToList(),

                Clip = australiaInverted
                    .Select(polygon => 
                        polygon
                            .Select(point => point * scale)
                            .ToList())
                    .ToList(),

                Operation = 
                    probabilities[i] >= 0.75 ? ClipOperation.Xor : 
                    probabilities[i] >= 0.50 ? ClipOperation.Union : 
                    probabilities[i] >= 0.25 ? ClipOperation.Intersection : ClipOperation.Difference
            }).ToList();

            var json = JsonConvert.SerializeObject(paths, Formatting.Indented);
            File.WriteAllText("LargePolygons.json", json);

            return paths;
        }
    }
}
