using System;
using Microsoft.Extensions.Options;

namespace Server.UnitTests.Fodder
{
	public class Options<T>: IOptions<T> where T: class, new()
	{
		public Options()
		{
			Value = new T();
		}

		public Options(T value)
		{
			Value = value;
		}

		public T Value { get; }
	}
}
