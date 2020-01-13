using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IHost BuildWebHost(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(
                    builder => { builder
                                 .UseContentRoot(@"C:\Dev\GitHub\Reegeek\SourceBrowser\src\HtmlGenerator\bin\Debug\net472\Web")
                        .UseStartup<Startup>(); })
                .Build();
    }
}
