namespace Server.Models
{
	public class InputConfiguration
	{
		public InputConfiguration(): this(128, 131072)
		{
		}

		public InputConfiguration(int titleLengthLimit, int bodyLengthLimit)
		{
			TitleLengthLimit = titleLengthLimit;
			BodyLengthLimit = bodyLengthLimit;
		}

		public int TitleLengthLimit { get; set; }
		public int BodyLengthLimit { get; set; }
	}
}
