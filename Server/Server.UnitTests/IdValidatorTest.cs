using Server.Utilities;
using Xunit;

namespace Server.UnitTests
{
	public class IdValidatorTest
	{
		[Theory(DisplayName = "Validator permits good IDs")]
		[InlineData("49s7dJFiQ0y7B7g46HE6mg")]
		[InlineData("I--lhwnHhkmzKLw7ooyjQw")]
		[InlineData("RYBbMHJV2k27j_AK2vt3MA")]
		[InlineData("37S6F0Ak50e1-RukN72mQg")]
		[InlineData("9LzmeL2qnk2Ajl68xLDNnw")]
		[InlineData("zIhX_XEl4kOxFdJf_RNHJQ")]
		[InlineData("9kJYMWJwqEeOH6l79x3bHA")]
		[InlineData("BsaggLh-9UqJG5UBS0R8jw")]
		[InlineData("-UlRZnFStECzd3yBSvZ6zg")]
		[InlineData("GystoyrqZkqgbBYqwMMlKw")]
		[InlineData("7M-xJZbAO0y2gZGUddakpg")]
		[InlineData("i5ATa1IVPkmmSngQlZZaiA")]
		[InlineData("6YpQY3R0aEq2gcI5fhD1SQ")]
		[InlineData("QpK1XigTFUuwjZ-edZo5ww")]
		[InlineData("r10_jM6zwU-UEBoySOXiUQ")]
		[InlineData("amfp1gIjo0yxzWGbAgLrPg")]
		[InlineData("AEr7RPByYkW_NZapqWeOzQ")]
		[InlineData("LaU2tYtaCkeJAcLZdWR2Dg")]
		[InlineData("RfiO9tfD_0i7V8LnWRVB1w")]
		[InlineData("2OXuMNNJl0q4LSHMAXJ1Nw")]
		[InlineData("-M3bg1i-uUmW8EgdH9Dx_Q")]
		[InlineData("5RDklV-5m066tYPYJJgGTQ")]
		[InlineData("2XiC7tFhZkKjKVGHZmg6lQ")]
		[InlineData("95ltdf14UkujTfPXYPHyJA")]
		[InlineData("hXhrkgm7skKp6UphlqsvHQ")]
		[InlineData("vJd5YX-wIkuO8vRBDT8uEw")]
		[InlineData("HkL5IeOP8kClp5JUtejpXA")]
		[InlineData("PaD7VRkpFUmOh47DoqoxtA")]
		[InlineData("DlJWXYzMx0myx1yPG5r7lA")]
		[InlineData("E4Rd4Q12bkOyAVF6JkOv4A")]
		[InlineData("5XnpKTzhc0mzI-jRDZP7vg")]
		[InlineData("2wdNW0coKU-SIYBCETB9Qg")]
		public void TestGood(string givenString) {
			Assert.False(givenString.FalsifyAsIdentifier());
		}

		[Theory(DisplayName = "Validator rejects bad IDs")]
		[InlineData("95ltdf14UkujTfP%YPHyJA")]
		[InlineData("hXhrkgm7skKp#UphlqsvHQ")]
		[InlineData("v∂d5Yą-wIkuO8vRBDT8uEw")]
		[InlineData("9LzmeL$nk2Ajl68xNnw")]
		[InlineData("zIhX_XEl4kOxqnk2AFdJf_RNHJQ")]
		[InlineData("9kJYqnk2AqEeOH6l^AqEe79x3bHA")]
		[InlineData("BsaggLhEl4kOxqEl4kOxq5UBS0R8jw")]
		[InlineData("-UlRZnF$tECzd3yBSvZ6zg")]
		public void TestBad(string givenString)
		{
			Assert.True(givenString.FalsifyAsIdentifier());
		}
	}
}
