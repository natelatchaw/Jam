using Bot;
using Bot.Interfaces;
using Bot.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bot
{
    public partial class CommandHandler : BackgroundService
    {
        private readonly ILogger<CommandHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;


        
        private readonly IOptions<CommandHandler.Options> _handlerOptions;
        private readonly IOptions<DiscordService.Options> _serviceOptions;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        
        private readonly IRateLimiter<UInt64> _rateLimiter;

        public static Version Version => new(0, 0, 1);

        public CommandHandler.Options HandlerOptions => _handlerOptions.Value;
        public DiscordService.Options ServiceOptions => _serviceOptions.Value;

        public CommandHandler(
            ILogger<CommandHandler> logger,
            IConfiguration configuration,
            IOptions<CommandHandler.Options> handlerOptions,
            IOptions<DiscordService.Options> serviceOptions,
            DiscordSocketClient client,
            CommandService commandService,
            IRateLimiter<UInt64> rateLimiter,
            IServiceProvider serviceProvider
        )
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;

            _handlerOptions = handlerOptions;
            _serviceOptions = serviceOptions;
            _client = client;
            _commandService = commandService;
            _rateLimiter = rateLimiter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{service} v{version}", nameof(CommandHandler), Version);

            await _commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);

            _client.MessageReceived += OnMessage;
        }

        private async Task OnMessage(SocketMessage arg)
        {
            // If the message is not a user message
            if (arg is not SocketUserMessage message)
            {
                throw new CommandValidationException($"Message is not a {nameof(SocketUserMessage)}");
            }
            
            // Log message
            _logger.LogTrace("{user}#{discriminator} -> {message}", arg.Author.Username, arg.Author.Discriminator, arg.CleanContent);

            try
            {
                // Set the position variable
                Int32 position = default;
                // Validate that the message is a command
                message.Validate(_serviceOptions, ref position);
                // Validate that the message meets rate limiting criteria
                _rateLimiter.Validate(message.Author.Id, message.CreatedAt);
                // Create a command context from the message
                SocketCommandContext context = new(_client, message);
                // Execute the command
                await _commandService.ExecuteAsync(context, position, _serviceProvider);
            }
            catch (CommandValidationException exception)
            {
                _logger.LogTrace("Command validation failed for message {id}: {reason}", message.Id, exception.Message);
            }
            catch (RateLimiterException exception)
            {
                await message.Channel.SendMessageAsync(exception.Message, messageReference: new(message.Id));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
            }
        }
    }

    public partial class CommandHandler 
    {
        public class Options
        {

        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CommandHandlerServiceExtensions
    {
        public static IServiceCollection AddCommandHandler(this IServiceCollection services, IConfigurationSection section) => services
            .Configure<CommandHandler.Options>(section)
            .AddHostedService<CommandHandler>();
    }
}
