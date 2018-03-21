using Server.Controllers;
using Xunit;

namespace Server.UnitTests
{
	public class EmailValidatorTest
	{
		[Theory(DisplayName = "Validator rejects bad addresses")]
		[InlineData("me@")]
		[InlineData("@example.com")]
		[InlineData("me.@example.com")]
		[InlineData(".me@example.com")]
		[InlineData("me@example..com")]
		[InlineData("me.example @com")]
		[InlineData("me\\@example.com")]
		public void TestBadEmails(string emailAddress) {
			Assert.False(AccountController.VetEmail(emailAddress));
		}

		[Theory(DisplayName = "Validator permits good addresses")]
		[InlineData("alfa@bravo.com")]
		[InlineData("alfa@com")]
		[InlineData("me @example.com")]
		[InlineData("a.nonymous @example.com")]
		[InlineData("name+tag @example.com")]
		[InlineData("name\\@tag @example.com")]
		[InlineData("spaces\\ are\\ allowed @example.com")]
		[InlineData("\"spaces may be quoted\"@example.com")]
		[InlineData("!#$%&'*+-/=.?^_`{|}~@[1.0.0.127]")]
		[InlineData("!#$%&'*+-/=.?^_`{|}~@[IPv6:0123:4567:89AB:CDEF:0123:4567:89AB:CDEF]")]
		[InlineData("\"very.(),:;<>[]\".VERY.\"very@\\ \"very\".unusual\"@strange.example.com")]
		public void TestGoodEmails(string emailAddress)
		{
			Assert.True(AccountController.VetEmail(emailAddress));
		}
	}
}
