using Bot.Extensions;
using Bot.Interfaces;
using Bot.Models;
using Bot.Services;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Bot.Modules
{
    public partial class AudioModule : ModuleBase
    {
        private readonly ILogger<AudioModule> _logger;
        private readonly AudioService _audioService;
        private readonly YouTubeDLService _youtubeService;
        private readonly FFmpegService _ffmpegService;
        private readonly IAudioEnqueuable _queue;

        private IAudioClient? Client { get; set; }

        public AudioModule(
            ILogger<AudioModule> logger,
            AudioService audioService,
            YouTubeDLService youtubeService,
            FFmpegService ffmpegService,
            IAudioEnqueuable queue
        ) : base()
        {
            _logger = logger;
            _audioService = audioService;
            _youtubeService = youtubeService;
            _ffmpegService = ffmpegService;
            _queue = queue;
        }

        [Command("play", RunMode = RunMode.Async)]
        public Task PlayAsync() => Task.Run(async () =>
        {
            try
            {
                IVoiceChannel voiceChannel = Context.GetVoiceChannel();

                await _audioService.JoinAsync(voiceChannel);

                await _audioService.PlayAsync(Context.GetVoiceChannel()).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, $"Tasks were cancelled. This is expected behavior.");
                return;
            }
            catch (CommandContextException exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync("Could not determine the voice channel to use.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                _logger.LogError(exception, "{message}", exception.StackTrace);
                await Context.Channel.SendMessageAsync(exception.Message);
            }
        });

        [Command("stop")]
        public Task StopAsync() => Task.Run(async () =>
        {
            try
            {
                IVoiceChannel voiceChannel = Context.GetVoiceChannel();

                await _audioService.StopAsync(voiceChannel);
            }
            catch (CommandContextException exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync("Could not determine the voice channel to use.");
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(exception, $"Tasks were cancelled. This is expected behavior.");
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                _logger.LogError(exception, "{message}", exception.StackTrace);
                await Context.Channel.SendMessageAsync(exception.Message);
            }
        });

        [Command("queue", RunMode = RunMode.Async)]
        public Task QueueAsync([Remainder] String query) => Task.Run(async () =>
        {
            try
            {
                if (await DownloadMetadataAsync(query) is not Metadata metadata)
                {
                    _logger.LogWarning("Could not obtain metadata for query '{query}'", query);
                    await Context.Channel.SendMessageAsync($"No results found for `{query}`");
                    return;
                }

                await UpdateStatus(metadata);

                await _queue.EnqueueAsync(Context.Guild.Id, metadata);
            }
            catch (CommandContextException exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync("Could not determine the voice channel to use.");
            }
            catch (YouTubeDLServiceException exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync("An issue occurred during audio download. Check the logs for details.");
            }
            catch (FFmpegServiceException exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync($"An issue occurred during FFmpeg multiplexing. Check the logs for details.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{message}", exception.Message);
                await Context.Channel.SendMessageAsync($"An unhandled error occurred. Check the log for details.");
            }
        });


        [Command("play_enable_sc", RunMode = RunMode.Async)]
        public Task Enable() => Task.Run(async () =>
        {
            IGuild guild = Context.Guild;

            SlashCommandBuilder builder = new();
            if (typeof(AudioModule).GetMethod(nameof(QueueAsync)) is not MethodBase method) return;
            if (method.GetCustomAttribute<CommandAttribute>() is not CommandAttribute attribute) return;
            String name = attribute.Text.ToLower();
            _logger.LogInformation(name);
            String description = "Play audio";
            builder.WithName(attribute.Text.ToLower());
            builder.WithDescription(description);

            SlashCommandOptionBuilder optionBuilder = new();
            optionBuilder.WithType(ApplicationCommandOptionType.String);
            optionBuilder.WithDescription("A search query");
            optionBuilder.WithName("Query");
            optionBuilder.WithRequired(true);
            builder.AddOption(optionBuilder);

            try
            {
                await guild.CreateApplicationCommandAsync(builder.Build());
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
        });
    }

    public partial class AudioModule
    {
        private async Task<Metadata?> DownloadMetadataAsync(String query)
        {
            /// Spawn youtube-dl
            _logger.LogTrace("Spawning YouTube-DL process...");
            List<StringValues> youtubeDLMetadataOptions = GetMetadataArgs(query);
            ProcessStartInfo youtubeDLMetadataInfo = _youtubeService.GetInfo(youtubeDLMetadataOptions);
            Process youtubeDLMetadata = _youtubeService.Execute(youtubeDLMetadataInfo);
            _logger.LogDebug("{filename} {arguments}", youtubeDLMetadata.StartInfo.FileName, youtubeDLMetadata.StartInfo.Arguments);

            /// Download metadata
            _logger.LogDebug("Deserializing YouTube-DL metadata...");
            Metadata? metadata = await youtubeDLMetadata.DeserializeAsync<Metadata>();
            if (youtubeDLMetadata.HasExited is false) youtubeDLMetadata.Kill();
            _logger.LogDebug("Metadata deserialization finished.");

            /// Return
            return metadata;
        }

        private async Task UpdateStatus(Metadata metadata, Int32 descriptionLength = 150)
        {
            IUser author = Context.Message.Author;

            EmbedBuilder embedBuilder = metadata
                .AsEmbedBuilder(descriptionLength)
                .WithAuthor(author)
                .WithColor(Color.Red);

            if (Context.Channel is ITextChannel channel)
                await channel.SendMessageAsync(embed: embedBuilder.Build());
        }

        private async Task UpdateActivity(Metadata metadata, Int32 descriptionLength = 150)
        {
            IActivity activity = metadata
                .AsActivity(descriptionLength);

            if (Context.Client is DiscordSocketClient client)
                await client.SetActivityAsync(activity);
        }
    }

    public partial class AudioModule
    {
        private static List<StringValues> GetMetadataArgs(String query) => new()
        {
            new(new[] { $"ytsearch:\"{query}\"" }),
            new(new[] { "--dump-json" }),
            new(new[] { "--output", "-" }),
        };
    }

    public class AudioModuleException : Exception
    {
        public AudioModuleException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}
