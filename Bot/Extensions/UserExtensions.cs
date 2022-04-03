using System;

namespace Bot.Extensions
{
    public static class UserExtensions
    {
        public static Discord.EmbedAuthorBuilder AsEmbedAuthorBuilder(this Discord.IUser author)
        {
            Discord.EmbedAuthorBuilder authorBuilder = new();
            authorBuilder.Name = author.Username;
            if (author.GetAvatarUrl() is String avatarUrl)
                authorBuilder.IconUrl = avatarUrl;
            else if (author.GetDefaultAvatarUrl() is String defaultAvatarUrl)
                authorBuilder.IconUrl = defaultAvatarUrl;
            return authorBuilder;
        }
    }
}
