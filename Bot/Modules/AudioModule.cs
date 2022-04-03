using Bot.Extensions;
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

        private IAudioClient? Client { get; set; }

        public AudioModule(
            ILogger<AudioModule> logger,
            AudioService audioService,
            YouTubeDLService youtubeService,
            FFmpegService ffmpegService
        ) : base()
        {
            _logger = logger;
            _audioService = audioService;
            _youtubeService = youtubeService;
            _ffmpegService = ffmpegService;
        }

        [Command("play", RunMode = RunMode.Async)]
        public Task Play([Remainder] String query) => Task.Run(async () =>
        {
            try
            {
                IVoiceChannel voiceChannel = Context.GetVoiceChannel();

                if (await DownloadMetadataAsync(query) is Metadata metadata)
                {
                    Int32 descriptionLength = 150;
                    IUser author = Context.Message.Author;
                    EmbedAuthorBuilder embedAuthorBuilder = author
                        .AsEmbedAuthorBuilder();
                    EmbedBuilder embedBuilder = metadata
                        .AsEmbedBuilder(descriptionLength)
                        .WithAuthor(author)
                        .WithColor(Color.Red);
                    await Context.Channel.SendMessageAsync(embed: embedBuilder.Build());

                    if (Context.Client is DiscordSocketClient client)
                        await client.SetActivityAsync(metadata.AsActivity(descriptionLength));
                }

                _ = await _audioService.JoinAsync(voiceChannel);

                try
                {
                    await Context.Message.DeleteAsync(new() { Timeout = 3 });
                }
                catch (HttpException exception)
                {
                    _logger.LogWarning("Failed to remove queue message. Are permissions enabled?");
                    _logger.LogError(exception, "{message}", exception.Message);
                }

                Stream stream = await DownloadAudioAsync(query);
                await Task.Run(async () => await _audioService.StreamAsync(voiceChannel, stream));
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
            if (typeof(AudioModule).GetMethod(nameof(Play)) is not MethodBase method) return;
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

        private async Task<Stream> DownloadAudioAsync(String query)
        {
            /// Spawn youtube-dl
            _logger.LogTrace("Spawning YouTube-DL process...");
            List<StringValues> youtubeDLOptions = GetDownloadArgs(query);
            ProcessStartInfo youtubeDLInfo = _youtubeService.GetInfo(youtubeDLOptions);
            Process youtubeDL = _youtubeService.Execute(youtubeDLInfo);
            _logger.LogDebug("{filename} {arguments}", youtubeDL.StartInfo.FileName, youtubeDL.StartInfo.Arguments);

            /// Spawn ffmpeg
            _logger.LogTrace("Spawning FFmpeg process...");
            List<StringValues> ffmpegOptions = GetMultiplexArgs();
            ProcessStartInfo ffmpegInfo = _ffmpegService.GetInfo(ffmpegOptions);
            Process ffmpeg = _ffmpegService.Execute(ffmpegInfo);
            _logger.LogDebug("{directory}> {filename} {arguments}", ffmpegInfo.WorkingDirectory, ffmpeg.StartInfo.FileName, ffmpeg.StartInfo.Arguments);

            /// Pipe audio
            _logger.LogDebug("Piping youtube-dl output to ffmpeg...");
            Stream audio = await youtubeDL.PipeAsync(ffmpeg);
            if (youtubeDL.HasExited is false) youtubeDL.Kill();
            if (ffmpeg.HasExited is false) ffmpeg.Kill();
            _logger.LogDebug("Received {length} bytes from ffmpeg.", audio.Length);

            /// Rewind audio
            audio.Seek(0, SeekOrigin.Begin);

            /// Return
            return audio;
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

        private static List<StringValues> GetDownloadArgs(String query) => new()
        {
            new(new[] { $"ytsearch:\"{query}\"" }),
            new(new[] { "--output", "-" }),
        };

        private static List<StringValues> GetMultiplexArgs() => new()
        {
            // INPUT PARAMETERS
            new(new[] { "-hide_banner" }),
            new(new[] { "-loglevel verbose" }),
            new(new[] { "-i", "pipe:0" }),

            // OUTPUT PARAMETERS
            new(new[] { "-ac", "2" }),
            new(new[] { "-f", "s16le" }),
            new(new[] { "-ar", "48000" }),
            new(new[] { "pipe:1" }),
        };
    }

    public class AudioModuleException : Exception
    {
        public AudioModuleException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}
