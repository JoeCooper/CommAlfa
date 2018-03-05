using System;
using System.Security.Cryptography;
using System.Text;

namespace Server.Utilities
{
	public static class Gravatar
	{
		public static string ToGravatarHash(this string email) {
            string gravatarHash;
			using (var md5Encoder = MD5.Create())
			{
				md5Encoder.Initialize();
				var flattened = email.Trim().ToLower();
				var buffer = Encoding.UTF8.GetBytes(flattened);
				var hash = md5Encoder.ComputeHash(buffer);
				var builder = new StringBuilder();
				for (var i = 0; i < hash.Length; i++)
				{
					builder.AppendFormat("{0:x2}", hash[i]);
				}
				gravatarHash = builder.ToString();
			}
			return gravatarHash;
		}
	}
}
