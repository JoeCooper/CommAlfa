using System;
namespace Server.Models
{
	public class DocumentBlockedException: Exception
	{
		public DocumentBlockedException(bool isBlockVoluntary)
		{
			IsBlockVoluntary = isBlockVoluntary;
		}

		public bool IsBlockVoluntary { get; }
	}
}
