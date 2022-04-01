using Bot.Services;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Modules
{
    public class InfoModule : ModuleBase
    {
        private readonly YouTubeDLService _youtubeService;
        private readonly FFmpegService _ffmpegService;

        public InfoModule(
            YouTubeDLService youtubeService,
            FFmpegService ffmpegService
        ) : base()
        {
            _youtubeService = youtubeService;
            _ffmpegService = ffmpegService;
        }

        [Command("info")]
        [Summary("Retrieves info about the bot.")]
        public async Task GetInfo()
        {
            Dictionary<String, Version> components = new()
            {
                { nameof(DiscordService), DiscordService.Version },
                { nameof(RateLimiter), RateLimiter.Version },
            };

            EmbedBuilder builder = new();
            builder.Title = "Info";
            builder.Description = String.Empty;
            String versionList = String.Join("\n", components.Select((KeyValuePair<String, Version> pair) => $"{pair.Key} v{pair.Value}"));
            builder.AddField("Components", versionList, true);

            await Context.Channel.SendMessageAsync(embed: builder.Build());
        }
    }
}
