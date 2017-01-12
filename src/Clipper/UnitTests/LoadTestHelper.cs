using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Clipper;

namespace UnitTests
{
    public static class LoadTestHelper
    {
        public static void SaveToFile(string filename, IReadOnlyCollection<ClipExecutionData> dataSet)
        {
            var builder = new StringBuilder();

            foreach (var data in dataSet)
            {
                // Write caption
                builder.Append($"CAPTION: {data.TestNumber}. {data.Caption}{Environment.NewLine}");

                // Write clip type
                builder.Append($"CLIPTYPE: {data.ClipOperation.ToString().ToUpper()}{Environment.NewLine}");

                // Write fill type
                builder.Append($"FILLTYPE: {data.FillType.ToString().ToUpper()}{Environment.NewLine}");

                // Write subjects
                WritePath(builder, "SUBJECTS", data.Subjects);

                // Write clips
                WritePath(builder, "CLIPS", data.Clips);

                // Write solution
                WritePath(builder, "SOLUTION", data.Solution);

                // Add blank line.
                builder.Append(Environment.NewLine);
            }

            File.WriteAllText(filename, builder.ToString());
        }

        private static void WritePath(StringBuilder builder, string name, PolygonPath path)
        {
            // Write path type
            builder.Append($"{name.ToUpper()}{Environment.NewLine}");

            var first = true;

            // Write polygons
            foreach (var polygon in path)
            {
                var copy = new Polygon(polygon);

                if (first && name == "SOLUTION" && copy.Orientation != PolygonOrientation.CounterClockwise)
                {
                    first = false;
                    copy.Orientation = PolygonOrientation.CounterClockwise;
                }

                copy.OrderBottomLeftFirst();

                // Write points for polygon
                foreach (var point in copy)
                {
                    builder.Append($"{point.X},{point.Y} ");
                }

                // New line at end of polygon
                builder.Append(Environment.NewLine);
            }
        }

        public static Dictionary<int, ClipExecutionData> LoadFromFile(string filename)
        {
            var data = new Dictionary<int, ClipExecutionData>();
            var lines = File.ReadAllLines(filename);
            var lineNumber = 0;

            while (true)
            {
                SkipEmptyLines(lines, ref lineNumber);
                if (lineNumber == lines.Length) break;
                ReadExecutionData(data, lines, ref lineNumber);
            }

            return data;
        }

        private static void ReadExecutionData(IDictionary<int, ClipExecutionData> data, IReadOnlyList<string> lines, ref int lineNumber)
        {
            const string matchCaption = @"^CAPTION:\s*(\d+)\s*\.\s*(.*)\s*?($|^)";
            const string matchClipOperation = @"^CLIPTYPE:\s*(.*)\s*$";
            const string matchFillType = @"^FILLTYPE:\s*(.*)\s*$";

            var executionData = new ClipExecutionData();

            // Read caption and test number
            var match = Regex.Match(lines[lineNumber++], matchCaption);
            if (!match.Success) { throw new Exception($"Expecting caption at line {lineNumber}."); }

            executionData.TestNumber = int.Parse(match.Groups[1].Value);
            executionData.Caption = match.Groups[2].Value;

            // Read clip type
            match = Regex.Match(lines[lineNumber++], matchClipOperation);
            if (!match.Success) { throw new Exception($"Expecting clip operation at line {lineNumber}."); }
            executionData.ClipOperation = (ClipOperation)Enum.Parse(typeof(ClipOperation), match.Groups[1].Value, true);

            // Read fill type
            match = Regex.Match(lines[lineNumber++], matchFillType);
            if (!match.Success) { throw new Exception($"Expecting fill type at line {lineNumber}."); }
            executionData.FillType = (PolygonFillType)Enum.Parse(typeof(PolygonFillType), match.Groups[1].Value, true);

            // Read subjects
            executionData.Subjects = ReadPath("SUBJECTS", lines, ref lineNumber);

            // Read clip
            executionData.Clips = ReadPath("CLIPS", lines, ref lineNumber);

            // Read solution
            executionData.Solution = ReadPath("SOLUTION", lines, ref lineNumber);

            data.Add(executionData.TestNumber, executionData);
        }

        private static PolygonPath ReadPath(string pathName, IReadOnlyList<string> lines, ref int lineNumber)
        {
            // Get the path name
            var name = lines[lineNumber++].Trim();
            if (!name.Equals(pathName, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new Exception($"Expecting path name '{pathName}' at line {lineNumber}.");
            }

            // Create a new path
            var path = new PolygonPath();

            // Read polygons into path.
            while (ReadPolygon(path, lines, ref lineNumber)) { }

            return path;
        }

        private static bool ReadPolygon(PolygonPath path, IReadOnlyList<string> lines, ref int lineNumber)
        {
            const string matchPolygonPointLine = @"^\s*[\+-]?\s*\d+\s*,\s*[\+-]?\s*\d+\s*";
            const string matchPointValues = @"\s*([\+-]?\s*\d+\s*),\s*([\+-]?\s*\d+)\s*";

            // EOF?
            if (lineNumber == lines.Count) return false;

            var match = Regex.Match(lines[lineNumber], matchPolygonPointLine);
            if (!match.Success) { return false; }

            var line = lines[lineNumber++].Trim();

            var polygon = new Polygon();
            while (line.Length > 0)
            {
                // Read point
                match = Regex.Match(line, matchPointValues);

                // It should be a match for valid values as we only get here if line has length > 0, and we remove whitespace from begining.
                if (!match.Success) { throw new Exception($"Invalid point value at line {lineNumber} ==> {line}"); }

                var x = int.Parse(match.Groups[1].Value.Replace(" ", ""));
                var y = int.Parse(match.Groups[2].Value.Replace(" ", ""));

                polygon.Add(new IntPoint(x, y));

                // Skip past match
                line = line.Substring(match.Length);
            }

            // Order to make comparison easier.
            polygon.OrderBottomLeftFirst();

            path.Add(polygon);

            return true;
        }

        private static void SkipEmptyLines(IReadOnlyList<string> lines, ref int lineNumber)
        {
            while (lineNumber < lines.Count && string.IsNullOrWhiteSpace(lines[lineNumber])) lineNumber++;
        }
    }
}
