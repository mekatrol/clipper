# clipper
A C# code refactor of Angus Johnson's clipper library to align with C# style guide. Includes Vatti clipper.


Changes from the original Angus code base:
1. Classes/structs live within their own file (file has same name as class struct).
2. Support for 32 bit integers dropped, all integers are 64 bit (long) (the use_int32 define not supported, 'cInt' changed to 'long').
3. Support for Z value on IntPoint dropped (the use_xyz define no longer supported).
