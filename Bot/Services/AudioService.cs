using Bot.Interfaces;
using Bot.Models;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{
    public partial class AudioService
    {
        /// <summary>
        /// Maintains a mapping of <see cref="IClientConnection"/>s with the IDs of the <see cref="IGuild"/>s that they are active in.
        /// </summary>
        private readonly ConcurrentDictionary<UInt64, IClientConnection> Clients;

        /// <summary>
        /// 
        /// </summary>
        private readonly IAudioDequeuable _queue;


        public AudioService(
            IAudioDequeuable queue
        )
        {
            // Initialize the audio client mapping dictionary
            Clients = new();
            _queue = queue;
        }

        /// <summary>
        /// Joins the provided <see cref="IVoiceChannel"/> if not already connected.
        /// </summary>
        /// 
        /// <param name="voiceChannel">
        /// The <see cref="IVoiceChannel"/> to use when creating or selecting an <see cref="IAudioClient"/>.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// An <see cref="IAudioClient"/> for the provided <see cref="IVoiceChannel"/>.
        /// </returns>
        /// 
        /// <exception cref="AudioServiceException"></exception>
        public async Task<IClientConnection> JoinAsync(IVoiceChannel voiceChannel, CancellationToken? cancellationToken = default)
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;
            // If the Clients dictionary contains a connection for the provided guild and voice channel
            if (Clients.TryGetValue(voiceChannel.Guild.Id, out IClientConnection? connection) && connection.VoiceChannel.Id.Equals(voiceChannel.Id))
            {
                // Return the connection
                return connection;
            }
            // If the Clients dictionary does not contain a connection for the provided guild and voice channel
            else
            {
                // Connect to the voice channel and create a new connection
                connection = new ClientConnection(voiceChannel, await voiceChannel.ConnectAsync());
                // Update the Clients dictionary with the newly created connection
                connection = Clients.AddOrUpdate(voiceChannel.Guild.Id, connection, (UInt64 guildId, IClientConnection existingConnection) =>
                {
                    // Stop the existing connection's audio client
                    Task.Run(async () => await existingConnection.AudioClient.StopAsync());
                    // Select the newly created connection
                    return connection;
                });
                // Return the connection
                return connection;
            }
        }

        /// <summary>
        /// Leaves the provided <see cref="IVoiceChannel"/> if connected.
        /// </summary>
        /// 
        /// <param name="voiceChannel">
        /// The <see cref="IVoiceChannel"/> to use when leaving and stopping the associated <see cref="IAudioClient"/>.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// A <see cref="Task"/> indicating completion.
        /// </returns>
        /// 
        /// <exception cref="AudioServiceException"></exception>
        public async Task LeaveAsync(IVoiceChannel voiceChannel, CancellationToken? cancellationToken = default)
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;
            // If the Clients dictionary contains a connection for the provided guild
            if (Clients.TryRemove(voiceChannel.Guild.Id, out IClientConnection? connection))
            {
                // Stop the existing connection's audio client
                await connection.AudioClient.StopAsync();
            }
            // If the Clients dictionary does not contain a connection for the provided guild
            else
            {
                // Throw exception
                throw new AudioServiceException($"No connection found for {voiceChannel.Guild.Name}");
            }
        }

        public async Task PlayAsync(IVoiceChannel voiceChannel, CancellationToken? cancellationToken = default)
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;

            if (Clients.TryGetValue(voiceChannel.Guild.Id, out IClientConnection? connection) is false)
                throw new AudioServiceException($"");

            if (connection.VoiceChannel.Id.Equals(voiceChannel.Id) is false)
                throw new AudioServiceException($"");

            // Link the internal and external tokens
            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(connection.Source.Token, token);

            while (linkedTokenSource.Token.IsCancellationRequested is false)
            {
                if (await _queue.DequeueAsync(voiceChannel.Guild.Id) is not Stream stream) break;

                stream.Seek(0, SeekOrigin.Begin);

                await StreamAsync(voiceChannel, stream, AudioApplication.Music, linkedTokenSource.Token);
            }
        }

        public Task StopAsync(IVoiceChannel voiceChannel)
        {
            if (Clients.TryGetValue(voiceChannel.Guild.Id, out IClientConnection? connection) is false)
                throw new AudioServiceException($"No {nameof(IClientConnection)} exists for {nameof(voiceChannel.Guild)} {voiceChannel.Guild.Id}");

            if (connection.VoiceChannel.Id.Equals(voiceChannel.Id) is false)
                throw new AudioServiceException($"The {nameof(IClientConnection)}'s {nameof(connection.VoiceChannel)} {nameof(connection.VoiceChannel.Id)} does not match the provided {nameof(connection.VoiceChannel)}'s {nameof(connection.VoiceChannel.Id)}");
            
            // Trigger cancellation
            connection.Source.Cancel();

            connection.Source = new CancellationTokenSource();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Streams a <see cref="Stream"/> object to the provided <see cref="IVoiceChannel"/>.
        /// </summary>
        /// 
        /// <param name="voiceChannel">
        /// The <see cref="IVoiceChannel"/> to stream to.
        /// </param>
        /// 
        /// <param name="stream">
        /// A <see cref="Stream"/> object containing the audio to be streamed.
        /// See <see cref="IAudioClient.CreatePCMStream"/> for specs.
        /// </param>
        /// 
        /// <param name="application">
        /// The type of audio to be streamed.
        /// </param>
        /// 
        /// <param name="cancellationToken">
        /// </param>
        /// 
        /// <returns>
        /// A <see cref="Task"/> indicating completion.
        /// </returns>
        /// 
        /// <remarks>
        /// The provided <see cref="Stream"/>'s position is not altered before streaming.
        /// Ensure that <see cref="Stream.Position"/> has been properly adjusted.
        /// </remarks>
        private async Task StreamAsync(IVoiceChannel voiceChannel, Stream stream, AudioApplication application = AudioApplication.Mixed, CancellationToken? cancellationToken = default)
        {
            // Use None if no cancellation token was provided
            CancellationToken token = cancellationToken ?? CancellationToken.None;
            // Join the provided voice channel
            IClientConnection connection = await JoinAsync(voiceChannel, token);
            // Create a PCM stream
            using Stream output = connection.AudioClient.CreatePCMStream(application);
            // Copy the provided stream to the output stream
            await stream.CopyToAsync(output, token);
            // Flush the output stream
            await stream.FlushAsync(token);
        }
    }

    public partial class AudioService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);
    }

    public class AudioServiceException : Exception
    {
        public AudioServiceException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}
