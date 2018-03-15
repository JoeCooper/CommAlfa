using System;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Server.Models;

namespace Server.Utilities
{
	public class IdentifierConverter: JsonConverter
	{
		public IdentifierConverter()
		{
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Guid) || objectType == typeof(MD5Sum);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			string encoded;
			if(value is Guid) {
				var buffer = ((Guid)value).ToByteArray();
				encoded = WebEncoders.Base64UrlEncode(buffer);
			} else if(value is MD5Sum) {
				encoded = value.ToString();
			} else {
				throw new ArgumentException();
			}
			writer.WriteValue(encoded);
		}
	}
}
