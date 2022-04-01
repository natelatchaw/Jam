using Microsoft.Extensions.Hosting;
using System;

namespace Bot
{
    public class Program
    {
        public static void Main(String[] args) =>
            CreateWebHostBuilder(args).Build().Run();

        public static IHostBuilder CreateWebHostBuilder(String[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}