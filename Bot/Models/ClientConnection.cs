using Discord;
using Discord.Audio;
using System.Threading;

namespace Bot.Models
{
    public interface IClientConnection
    {
        public IVoiceChannel VoiceChannel { get; set; }
        public IAudioClient AudioClient { get; set; }
        public CancellationTokenSource Source { get; set; }
    }

    public class ClientConnection : IClientConnection
    {
        public IVoiceChannel VoiceChannel { get; set; }
        public IAudioClient AudioClient { get; set; }
        public CancellationTokenSource Source { get; set; }

        public ClientConnection(IVoiceChannel voiceChannel, IAudioClient audioClient)
        {
            VoiceChannel = voiceChannel;
            AudioClient = audioClient;
            Source = new CancellationTokenSource();
        }
    }
}
