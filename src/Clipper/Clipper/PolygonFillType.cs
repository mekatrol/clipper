namespace Clipper
{
    /// <summary>
    /// By far the most widely used winding rules for polygon filling are
    /// EvenOdd & NonZero (GDI, GDI+, XLib, OpenGL, Cairo, AGG, Quartz, SVG, Gr32)
    /// Others rules include Positive, Negative and ABS_GTR_EQ_TWO (only in OpenGL)
    /// see http://glprogramming.com/red/chapter11.html
    /// </summary>
    public enum PolygonFillType { EvenOdd, NonZero, Positive, Negative };
}