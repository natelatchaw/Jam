using Bot.Services;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{
    public partial class DiscordService : BackgroundService
    {
        public static Version Version => new(0, 0, 1);

        private readonly ILogger<DiscordService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IOptions<Options> _options;

        public Options ServiceOptions => _options.Value;

        public DiscordService(
            ILogger<DiscordService> logger,
            IOptions<Options> options,
            DiscordSocketClient client
        )
        {
            _logger = logger;
            _client = client;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{service} prefix set to [{prefix}]", nameof(DiscordService), ServiceOptions.Prefix);

            _client.Log += Log;

            await _client.LoginAsync(Discord.TokenType.Bot, ServiceOptions.Token);
            await _client.StartAsync();

            await Task.Delay(-1, cancellationToken);
        }

        private Task Log(Discord.LogMessage message) => Task
            .Run(() => _logger.Log((LogLevel)message.Severity, "{message}", message.Message));
    }

    public partial class DiscordService
    {
        public class Options
        {
            public String? Token { get; set; }

            public Char? Prefix { get; set; }
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DiscordServiceExtensions
    {
        public static IServiceCollection AddDiscordService(this IServiceCollection services, IConfigurationSection section) => services
            .AddSingleton<DiscordSocketClient>()
            .Configure<DiscordService.Options>(section)
            .AddHostedService<DiscordService>();
    }
}
