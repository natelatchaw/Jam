using Bot.Interfaces;
using Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Interfaces
{
    public interface IRateLimiter<TKey>
    {
        public void Validate(TKey userId, DateTimeOffset messageTimestamp);
        public void Reset();
    }
}

namespace Bot.Services
{
    public partial class RateLimiter : BackgroundService, IRateLimiter<UInt64>
    {
        public static Version Version => new(0, 0, 3);

        private readonly ILogger<RateLimiter> _logger;
        private readonly IOptions<Options> _options;
        
        private ConcurrentDictionary<UInt64, DateTimeOffset> history { get; set; }

        public Options LimiterOptions => _options.Value;

        public RateLimiter(
            ILogger<RateLimiter> logger,
            IOptions<Options> options,
            IOptions<DiscordService.Options> serviceOptions
        )
        {
            _logger = logger;
            _options = options;

            history = new();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{service} v{version}", nameof(RateLimiter), Version);
            _logger.LogInformation("{service} cooldown set to {cooldown} seconds.", nameof(RateLimiter), LimiterOptions.Cooldown);
            await Task.Delay(-1, cancellationToken);
        }

        public void Validate(UInt64 userId, DateTimeOffset messageTimestamp)
        {
            // Get the timestamp of the user's last message
            DateTimeOffset lastTimestamp = history.GetValueOrDefault(userId, DateTimeOffset.MinValue);

            DateTimeOffset thisTimestamp = messageTimestamp;
            // Calculate the next available timestamp to send a message
            DateTimeOffset nextTimestamp = lastTimestamp.AddSeconds(LimiterOptions.Cooldown);

            // If the current message was posted before the next possible message timestamp
            if (thisTimestamp < nextTimestamp)
            {
                // Calculate the difference between timestamps
                TimeSpan differential = nextTimestamp - thisTimestamp;
                // Throw exception
                throw new RateLimiterException($"Command rejected: Rate limit triggered. Try again in {differential.TotalSeconds} seconds.");
            }
            // Otherwise
            else
            {
                _logger.LogTrace("Updating timestamp history of user {id} with timestamp {timestamp}", userId, messageTimestamp);
                // Update the history with the current message's timestamp
                history.AddOrUpdate(userId, messageTimestamp, (UInt64 id, DateTimeOffset timestamp) => messageTimestamp);

                _logger.LogTrace("Entries:\n{entries}", String.Join("\n", history.Keys.Select((UInt64 key) => $"\t{key}: {history[key]}")));
            }
        }

        public void Reset() => history.Clear();
    }

    public partial class RateLimiter
    {
        public class Options
        {
            public Int32 Cooldown { get; set; } = default;
        }
    }

    public class RateLimiterException : Exception
    {
        public RateLimiterException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RateLimiterServiceExtensions
    {
        public static IServiceCollection AddRateLimiter(this IServiceCollection services, IConfigurationSection section) => services
            .Configure<RateLimiter.Options>(section)
            .AddSingleton<IRateLimiter<UInt64>, RateLimiter>();
    }
}
