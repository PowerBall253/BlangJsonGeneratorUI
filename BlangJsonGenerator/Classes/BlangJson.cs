using System.Collections.Generic;

namespace BlangJsonGenerator.Classes
{
    public class BlangJson
    {
        public List<BlangJsonString> Strings { get; set; }

        public BlangJson()
        {
            Strings = new List<BlangJsonString>();
        }
    }

    public class BlangJsonString
    {
        public string Name { get; set; }
        public string Text { get; set; }

        public BlangJsonString()
        {
            Name = "";
            Text = "";
        }
    }
}
