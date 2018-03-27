using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Server.Utilities
{
	public static class RemoteAddress
	{
		public static IPAddress GetRemoteAddress(this HttpContext context)
		{
			IPAddress remoteAddress;
			const string ForwardedForHeaderKey = "X-Forwarded-For";
			if(context.Request.Headers.ContainsKey(ForwardedForHeaderKey) && IPAddress.TryParse(context.Request.Headers[ForwardedForHeaderKey].Last(), out remoteAddress))
			{
				//This block is empty. This could be organized better but organized like so, the compiler can correctly evaluate
				//that remoteAddress is assigned to.
			}
			else
			{
				remoteAddress = context.Connection.RemoteIpAddress;
			}
			return remoteAddress;
		}
	}
}
