using System;
namespace Server.ViewModels
{
	public class IdentifierViewModel
	{
		public IdentifierViewModel(string id)
		{
			Id = id;
		}

		public string Id { get; }
	}
}
