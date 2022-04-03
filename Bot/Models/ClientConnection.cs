using Discord;
using Discord.Audio;

namespace Bot.Models
{
    public interface IClientConnection
    {
        public IVoiceChannel VoiceChannel { get; set; }
        public IAudioClient AudioClient { get; set; }
    }

    public class ClientConnection : IClientConnection
    {
        public IVoiceChannel VoiceChannel { get; set; }
        public IAudioClient AudioClient { get; set; }

        public ClientConnection(IVoiceChannel voiceChannel, IAudioClient audioClient)
        {
            VoiceChannel = voiceChannel;
            AudioClient = audioClient;
        }
    }
}
