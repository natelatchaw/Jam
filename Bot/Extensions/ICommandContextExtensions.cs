using Discord;
using Discord.Commands;
using System;

namespace Bot.Extensions
{
    public static class ICommandContextExtensions
    {
        public static IVoiceChannel GetVoiceChannel(this ICommandContext context)
        {
            // Get the current user
            if (context.User is not IGuildUser user)
            {
                throw new CommandContextException($"{nameof(ICommandContext.User)} is not an {nameof(IGuildUser)}.");
            }
            // Get the user's current voice channel
            else if (user.VoiceChannel is not IVoiceChannel voiceChannel)
            {
                throw new CommandContextException($"{nameof(IGuildUser.VoiceChannel)} is not an {nameof(IVoiceChannel)}.");
            }
            // Return the voice channel
            else
            {
                return voiceChannel;
            }
        }
    }

    public class CommandContextException : Exception
    {
        public CommandContextException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}
