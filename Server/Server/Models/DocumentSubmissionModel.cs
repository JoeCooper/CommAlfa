using System.Collections.Generic;

namespace Server.Models
{
    public class DocumentSubmissionModel
    {
        public string Body { get; set; }

        public string Title { get; set; }

        public IEnumerable<string> AntecedentIdBase64 { get; set; }
    }
}
