using Microsoft.Extensions.Hosting;
using System;

namespace Bot
{
    public class Program
    {
        public static void Main(String[] args) =>
            CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(String[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}