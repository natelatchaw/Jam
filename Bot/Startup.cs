using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bot
{
    public class Startup
    {
        public IConfiguration _configuration { get; }

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDiscordService(_configuration.GetSection("Options"));
            services.AddCommandHandler(_configuration.GetSection("CommandHandler"));
            services.AddRateLimiter(_configuration.GetSection("RateLimiter"));

            services.AddFFmpegService(_configuration.GetSection("ffmpeg"));
            services.AddYouTubeDLService(_configuration.GetSection("youtube-dl"));

            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<CommandService>();

            services.AddLogging((ILoggingBuilder builder) =>
            {
                builder
                    .ClearProviders()
                    .AddConsole();
            });
        }
    }
}
