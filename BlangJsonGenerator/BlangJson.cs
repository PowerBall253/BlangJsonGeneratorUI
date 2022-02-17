using System.Collections.Generic;

namespace BlangJsonGenerator
{
    public class BlangJson
    {
        public List<BlangJsonString> Strings { get; set; }
    }

    public class BlangJsonString
    {
        public string Name { get; set; }
        public string Text { get; set; }
    }
}
