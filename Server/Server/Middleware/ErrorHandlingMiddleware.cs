using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Server.Models;

namespace Server.Middleware
{
	public class ErrorHandlingMiddleware
	{
		readonly RequestDelegate next;

		public ErrorHandlingMiddleware(RequestDelegate next)
		{
			this.next = next;
		}

		public async Task Invoke(HttpContext context /* other dependencies */)
		{
			try
			{
				await next(context);
			}
			catch (Exception ex)
			{
				await HandleExceptionAsync(context, ex);
			}
		}

		static Task HandleExceptionAsync(HttpContext context, Exception exception)
		{
			var code = HttpStatusCode.InternalServerError; // 500 if unexpected
			if (exception is FileNotFoundException) code = HttpStatusCode.NotFound;
			else if (exception is DocumentBlockedException)
			{
				var documentBlockedException = (DocumentBlockedException)exception;
				if (documentBlockedException.IsBlockVoluntary)
					code = HttpStatusCode.Gone;
				else
					code = (HttpStatusCode) 451;
			}
			var result = JsonConvert.SerializeObject(new { error = exception.Message });
			context.Response.ContentType = "application/json";
			context.Response.StatusCode = (int)code;
			return context.Response.WriteAsync(result);
		}
	}
}
