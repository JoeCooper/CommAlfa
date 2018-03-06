using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Server.Models;

namespace Server.ViewModels
{
    public class DocumentViewModel
    {
        public DocumentViewModel(string body, string title):
		this(body, title, Enumerable.Empty<DocumentListingViewModel>(), Enumerable.Empty<DocumentListingViewModel>())
        {
        }
        
        public DocumentViewModel(
            string body,
			string title,
			IEnumerable<DocumentListingViewModel> sources,
			IEnumerable<DocumentListingViewModel> comparables
            )
        {
            Body = body;
			Title = title;
			Sources = sources;
			Comparables = comparables;
        }

        [DataType(DataType.MultilineText)]
        public string Body { get; }

		public string Title { get; }

		public IEnumerable<DocumentListingViewModel> Sources { get; }

		public IEnumerable<DocumentListingViewModel> Comparables { get; }

		public DocumentViewModel WithComparables(IEnumerable<DocumentListingViewModel> comparables) {
			return new DocumentViewModel(Body, Title, Sources, comparables);
		}

		public DocumentViewModel WithSources(IEnumerable<DocumentListingViewModel> sources)
		{
			return new DocumentViewModel(Body, Title, sources, Comparables);
        }
    }
}
