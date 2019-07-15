using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.WebAPI
{
    public class AuthenticationMiddleWare
    {
        private readonly RequestDelegate _next;
        private IConfiguration _config;

        public AuthenticationMiddleWare(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _config = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!TerminateRequest(context))
                await _next.Invoke(context);
        }

        private bool TerminateRequest(HttpContext context)
        {
            if (string.Compare(context.Request.Method, "OPTIONS", true) == 0)
            {
                context.Response.StatusCode = 200;
                context.Response.Headers.Add("Access-Control-Allow-Origin", _config["Origin"]);
                context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Access-Control-Allow-Headers, Origin,Accept, X-Requested-With, Content-Type, Access-Control-Request-Method, Access-Control-Request-Headers");
                return true;
            }

            if (context.Request.Path.ToString().Contains("getLoggedInUser") &&
                (string.Compare(context.Connection.RemoteIpAddress.ToString(), _config["SecurityIp"]) == 0))
            {
                context.Response.StatusCode = 200;
                context.Response.Headers.Add("Access-Control-Allow-Origin", _config["Origin"]);
                context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Access-Control-Allow-Headers, Origin,Accept, X-Requested-With, Content-Type, Access-Control-Request-Method, Access-Control-Request-Headers");
                context.Response.WriteAsync("Security|vtsSecurity");
                return true;
            }

            return false;
        }
    }

    public static class AuthenticationMiddleWareExtensions
    {
        public static IApplicationBuilder UseAuthenticationMiddleWare(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleWare>();
        }
    }
}