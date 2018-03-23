using System;
using System.Collections.Immutable;

namespace Server.Utilities
{
	public static class TurboValidate
	{
		static readonly IImmutableSet<char> Base64UrlCharacters = ImmutableHashSet.CreateRange<char>("qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890_-=");

		//This DFA is intended to cheaply falsify the claim that the given string is a UUID or MD5 sum encoded in Base64URL encoding.
		//If it returns true, we are certain that the given string is _not_ a Base64 URL encoded UUID or MD5 sum.
		public static bool FalsifyAsIdentifier(this string s) {
			if (s == null)
				return true;
			//Both Guids and MD5 sums are 128 bits in length. 128 / 6 = 21 1/3, which must be rounded up to 22.
			const int length = 22;
			if(s.Length != length) {
				return true;
			}
			//If it contains a character not present in the canonical set of base64 URL characters than it's definitely not in this encoding.
			for (var i = 0; i < s.Length; i++) {
				if(Base64UrlCharacters.Contains(s[i]) == false) {
					return true;
				}
			}
			//We have failed to prove it isn't an identifier.
			return false;
		}
	}
}
