using Bot;
using Discord.Commands;
using Microsoft.Extensions.Options;
using System;

namespace Discord.WebSocket
{
    public static class SocketUserMessageExtensions
    {
        public static void Validate(this SocketUserMessage message, IOptions<DiscordService.Options> options, ref Int32 position)
        {
            // If the message was authored by a bot
            if (message.Author.IsBot)
            {
                throw new CommandValidationException($"Message author is a bot user.");
            }
            // If the prefix has not been defined
            if (options.Value.Prefix is not Char prefix)
            {
                throw new CommandValidationException($"A prefix has not been defined in the application settings.");
            }
            // If the message does not begin with the prefix
            if (message.HasCharPrefix(prefix, ref position) is false)
            {
                throw new CommandValidationException($"Message does not begin with the prefix {options.Value.Prefix}");
            }
        }
    }

    public class CommandValidationException: Exception
    {
        public CommandValidationException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}