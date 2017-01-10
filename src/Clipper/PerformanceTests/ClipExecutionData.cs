using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PerformanceTests
{
    public class ClipExecutionData
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public ClipOperation Operation { get; set; }
        public List<List<Point>> Subject { get; set; }
        public List<List<Point>> Clip { get; set; }
    }
}
