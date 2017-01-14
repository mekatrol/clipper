# clipper
A C# code refactor of Angus Johnson's clipper library to align with C# style guide. Includes Vatti clipper.


Changes from the original Angus code base:

1. Classes/structs live within their own file (file has same name as class struct).

2. Support for 32 bit integers dropped, all integers are 64 bit (long) (the use_int32 define not supported, 'cInt' changed to 'long').

3. Support for Z value on IntPoint dropped (the use_xyz define no longer supported).

4. Removed PreserveCollinear property from clipper.

5. The ReverseSolution flag changed to ReverseOrientation.

The solution is made up of:

1. Clipper - the refactored clipper library.

2. ClipperOriginal - Angus' original clipper code (unmodified).

3. ConsolePerformanceProfiler - an executable that can be used by the performance Profiler (menu Analyze | performance Profiler) when profiling the source performance.

3. PerformanceTests - quasi performance tests to compare the refactored code against the original for performance.

4. UnitTests - unit tests for clipper and other functions.

5. Visualizer - that enables visualization of clipper and test data.

All solutions should complie on both Microsoft.Net and Mono (including the Visualizer). The solution was created in Visual Studio 2017, but should work in 2015 as well

The community edition of Visual Studio 2015 and 2017 is free to download:  https://www.visualstudio.com/downloads/

