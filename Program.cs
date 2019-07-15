using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Server;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.WindowsServices;

namespace SenecaGlobal.VTS.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            var directoryPath = Path.GetDirectoryName(exePath);

            var config = new ConfigurationBuilder()
                        .SetBasePath(directoryPath)
                        .AddJsonFile("hosting.json", optional: true)
                        .Build();
            var host = new WebHostBuilder()
                    .UseStartup<Startup>()
                    .UseContentRoot(directoryPath)
                    .UseConfiguration(config)
                    .UseWebListener(options =>
                    {
                        options.ListenerSettings.Authentication.Schemes = AuthenticationSchemes.NTLM;
                        options.ListenerSettings.Authentication.AllowAnonymous = true;
                    })
                    .Build();

            if (Debugger.IsAttached || args.Contains("--debug"))
                host.Run();
            else
                host.RunAsService();
        }
    }
}
