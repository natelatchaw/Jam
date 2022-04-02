using Bot.Extensions;
using Bot.Services;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Bot.Modules
{
    public partial class AudioModule : ModuleBase
    {
        private readonly ILogger<AudioModule> _logger;
        private readonly YouTubeDLService _youtubeService;
        private readonly FFmpegService _ffmpegService;

        private IAudioClient? Client { get; set; }

        public AudioModule(
            ILogger<AudioModule> logger,
            YouTubeDLService youtubeService,
            FFmpegService ffmpegService
        ) : base()
        {
            _logger = logger;
            _youtubeService = youtubeService;
            _ffmpegService = ffmpegService;
        }

        [Command("play", RunMode = RunMode.Async)]
        [Summary("Searches for and plays a song.")]
        public Task Play([Remainder] String query) => Task.Run(async () =>
        {
            Boolean verbose = true;

            try
            {
                // Get the current user
                if (Context.User is not IGuildUser user)
                {
                    IUserMessage message = await Context.Channel.SendMessageAsync("I have no idea who you are.", messageReference: Context.Message.Reference);
                    await Task.Delay(DateTimeOffset.Now.AddSeconds(5) - DateTimeOffset.Now);
                    await message.DeleteAsync();
                    return;
                }

                // Get the user's current voice channel
                if (user.VoiceChannel is not IVoiceChannel voiceChannel)
                {
                    IUserMessage message = await Context.Channel.SendMessageAsync("Please join a voice channel first.", messageReference: Context.Message.Reference);
                    await Task.Delay(DateTimeOffset.Now.AddSeconds(5) - DateTimeOffset.Now);
                    await message.DeleteAsync();
                    return;
                }

                // If the client is not initialized
                if (Client is not IAudioClient client)
                {
                    // Connect and intialize the client
                    Client = await voiceChannel.ConnectAsync();
                }
                // If the client is currently playing
                else if (client.GetStreams().ContainsKey(voiceChannel.Id))
                {
                    IUserMessage message = await Context.Channel.SendMessageAsync("Queueing is not supported yet.", messageReference: Context.Message.Reference);
                    await Task.Delay(DateTimeOffset.Now.AddSeconds(5) - DateTimeOffset.Now);
                    await message.DeleteAsync();
                    return;
                }

                IUserMessage? updateMessage = default;
                if (verbose) updateMessage = await Context.Channel.SendMessageAsync("Starting download...");
                await Execute(query, updateMessage);
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
                await Context.Channel.SendMessageAsync(exception.Message, messageReference: Context.Message.Reference);
            }
            finally
            {
                await Context.Message.DeleteAsync(new() { Timeout = 3 });
            }
        });

        private async Task Execute(String query, IUserMessage? message)
        {
            await Task.Run(async () =>
            {
                try
                {
                    if (Client is not IAudioClient client) return;

                    //
                    if (message is not null) await message.ModifyAsync((MessageProperties properties) => properties.Content = "Initializing streaming service...");

                    _logger.LogTrace("Spawning YouTube-DL process...");
                    List<StringValues> youtubeDLOptions = GetYoutubeDLOptions(query);
                    ProcessStartInfo youtubeDLInfo = _youtubeService.GetStartInfo(youtubeDLOptions);
                    Process youtubeDL = _youtubeService.Execute(youtubeDLInfo);
                    _logger.LogDebug("{filename} {arguments}", youtubeDL.StartInfo.FileName, youtubeDL.StartInfo.Arguments);

                    _logger.LogTrace("Spawning FFmpeg process...");
                    List<StringValues> ffmpegOptions = GetFFmpegOptions();
                    ProcessStartInfo ffmpegInfo = _ffmpegService.GetStartInfo(ffmpegOptions);
                    Process ffmpeg = _ffmpegService.Execute(ffmpegInfo);
                    _logger.LogDebug("{directory}> {filename} {arguments}", ffmpegInfo.WorkingDirectory, ffmpeg.StartInfo.FileName, ffmpeg.StartInfo.Arguments);

                    //
                    if (message is not null) await message.ModifyAsync((MessageProperties properties) => properties.Content = "Beginning download...");

                    _logger.LogDebug("Piping youtube-dl output to ffmpeg...");
                    using Stream audio = await youtubeDL.PipeAsync(ffmpeg);
                    _logger.LogDebug("Received {length} bytes from ffmpeg.", audio.Length);

                    //
                    if (message is not null) await message.ModifyAsync((MessageProperties properties) => properties.Content = "Preparing stream...");

                    _logger.LogDebug("Creating PCM Stream...");
                    using AudioOutStream output = client.CreatePCMStream(AudioApplication.Mixed);
                    _logger.LogDebug("Created PCM Stream.");

                    _logger.LogDebug("Rewinding audio stream from position {position}...", audio.Position);
                    Int64 position = audio.Seek(0, SeekOrigin.Begin);
                    _logger.LogDebug("Audio stream rewound to position {position}", position);

                    //
                    if (message is not null) await message.ModifyAsync((MessageProperties properties) => properties.Content = "Streaming to Discord...");

                    _logger.LogDebug("Copying {length} bytes from audio stream to PCM stream...", audio.Length);
                    await audio.CopyToAsync(output);
                    _logger.LogDebug("Copied {length} bytes.", audio.Length);

                    //
                    if (message is not null) await message.DeleteAsync();

                    _logger.LogDebug("Flushing PCM stream to client...");
                    await output.FlushAsync();
                    _logger.LogDebug("Flushed audio stream.");
                }
                catch (DllNotFoundException)
                {
                    throw;
                }
            });
        }
    }

    public partial class AudioModule
    {
        private static List<StringValues> GetYoutubeDLOptions(String query) => new()
        {
            new(new string[] { $"ytsearch:\"{query}\"" }),
            new(new string[] { "--output", "-" }),
        };
        private static List<StringValues> GetFFmpegOptions() => new()
        {
            // INPUT PARAMETERS
            //new(new string[] { "-loglevel", "trace" }),
            //new(new string[] { "-hide_banner" }),
            new(new string[] { "-i", "pipe:0" }),

            // OUTPUT PARAMETERS
            new(new string[] { "-ac", "2" }),
            new(new string[] { "-f", "s16le" }),
            new(new string[] { "-ar", "48000" }),
            new(new string[] { "pipe:1" }),
        };
    }
}
