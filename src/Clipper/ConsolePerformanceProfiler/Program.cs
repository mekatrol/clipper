using PerformanceTests;

namespace ConsolePerformanceProfiler
{
    internal class Program
    {
        private static void Main()
        {
            var performanceTest = new PerformanceRunnerTests();

            performanceTest.SimplePolygonTest();
            performanceTest.ComplexPolygonTest();
            performanceTest.LargePolygonTest();
        }
    }
}
