using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Server.ViewModels
{
    public class DocumentViewModel
    {        
        public DocumentViewModel(
            string body,
			string title)
        {
            Body = body;
			Title = title;
        }

        [DataType(DataType.MultilineText)]
        public string Body { get; }

		public string Title { get; }
    }
}
