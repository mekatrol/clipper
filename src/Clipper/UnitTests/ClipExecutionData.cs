using Clipper;

namespace UnitTests
{
    public struct ClipExecutionData
    {
        public string Caption;
        public ClipOperation ClipOperation;
        public PolygonFillType FillType;
        public PolygonPath Subjects;
        public PolygonPath Clips;
        public PolygonPath Solution;
        public int TestNumber;

        public override string ToString()
        {
            return $"{TestNumber}: {Caption}";
        }
    }
}
