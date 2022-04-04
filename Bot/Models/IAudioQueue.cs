using Bot.Extensions;
using Bot.Interfaces;
using Bot.Models;
using Bot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Interfaces
{
    public interface IAudioEnqueuable
    {
        public Task EnqueueAsync(UInt64 id, Metadata item, CancellationToken? cancellationToken = default);
    }

    public interface IAudioDequeuable
    {
        public Task<Stream?> DequeueAsync(UInt64 id, CancellationToken? cancellationToken = default);
        public Task ClearAsync(UInt64 id, CancellationToken? cancellationToken = default);
    }
}

namespace Bot.Models
{
    public partial class AudioQueue : IAudioEnqueuable, IAudioDequeuable
    {
        private readonly ILogger<AudioQueue> _logger;
        private readonly ConcurrentDictionary<UInt64, ConcurrentQueue<Metadata>> _queues;
        private readonly YouTubeDLService _youtubeDLService;
        private readonly FFmpegService _ffmpegService;


        public AudioQueue(
            ILogger<AudioQueue> logger,
            YouTubeDLService youtubeDLService,
            FFmpegService ffmpegService
        )
        {
            _logger = logger;
            _youtubeDLService = youtubeDLService;
            _ffmpegService = ffmpegService;
            _queues = new();
        }

        /// <summary>
        /// Adds a <see cref="Metadata"/> instance to the queue with the provided <paramref name="id"/>
        /// </summary>
        /// 
        /// <param name="id">
        /// The ID of the <see cref="ConcurrentQueue{T}"/> to access.
        /// </param>
        /// 
        /// <param name="item">
        /// The <see cref="Metadata"/> to add the the specified <see cref="ConcurrentQueue{T}"/>.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// A <see cref="Task"/> indicating completion.
        /// </returns>
        public Task EnqueueAsync(UInt64 id, Metadata item, CancellationToken? cancellationToken = default) => Task.Run(() =>
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            ConcurrentQueue<Metadata> queue = _queues.GetOrAdd(id, _ => new());
            queue.Enqueue(item);
        });

        /// <summary>
        /// Gets a <see cref="Stream"/> instance, downloaded from the <see cref="Metadata"/> 
        /// in the queue with the provided <paramref name="id"/>.
        /// </summary>
        /// 
        /// <param name="id">
        /// The ID of the <see cref="ConcurrentQueue{T}"/> to access.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// A <see cref="Stream"/> containing the audio downloaded from the metadata.
        /// </returns>
        public Task<Stream?> DequeueAsync(UInt64 id, CancellationToken? cancellationToken = default) => Task.Run(() =>
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            ConcurrentQueue<Metadata> queue = _queues.GetOrAdd(id, _ => new());
            return queue.TryDequeue(out Metadata? metadata) switch
            {
                false => Task.FromResult(default(Stream)),
                true => DownloadAudioAsync(metadata, cancellationToken) as Task<Stream?>,
            };
        });

        /// <summary>
        /// Clears the queue with the provided <paramref name="id"/>.
        /// </summary>
        /// 
        /// <param name="id">
        /// The ID of the <see cref="ConcurrentQueue{T}"/> to access.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// A <see cref="Task"/> indicating completion.
        /// </returns>
        public Task ClearAsync(UInt64 id, CancellationToken? cancellationToken = default) => Task.Run(() =>
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            ConcurrentQueue<Metadata> queue = _queues.GetOrAdd(id, _ => new());
            queue.Clear();
        });
    }

    public partial class AudioQueue
    {
        private async Task<Stream> DownloadAudioAsync(Metadata metadata, CancellationToken? cancellationToken = default)
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            if (metadata.Source is not Uri uri)
                throw new ArgumentException($"The provided {nameof(Metadata)} did not contain a {nameof(Metadata.Source)} {nameof(Uri)}.", nameof(metadata));

            /// Spawn youtube-dl
            _logger.LogTrace("Spawning YouTube-DL process...");
            List<StringValues> youtubeDLOptions = GetDownloadArgs(uri);
            ProcessStartInfo youtubeDLInfo = _youtubeDLService.GetInfo(youtubeDLOptions);
            Process youtubeDL = _youtubeDLService.Execute(youtubeDLInfo);
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

    public partial class AudioQueue
    {
        private static List<StringValues> GetDownloadArgs(Uri uri) => new()
        {
            new(new[] { uri.AbsoluteUri }),
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
}
