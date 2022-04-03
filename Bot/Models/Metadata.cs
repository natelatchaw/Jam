using Bot.Models;
using System;
using System.Text.Json.Serialization;

namespace Bot.Models
{
    public class Metadata
    {
        [JsonPropertyName("title")]
        public String? Title { get; set; }

        [JsonPropertyName("thumbnail")]
        public Uri? Thumbnail { get; set; }

        [JsonPropertyName("description")]
        public String? Description { get; set; }

        [JsonPropertyName("webpage_url")]
        public Uri? Source { get; set; }
    }
}

namespace Bot.Extensions
{
    public static class MetadataExtensions
    {
        public static Discord.EmbedBuilder AsEmbedBuilder(this Metadata metadata, Int32 descriptionLength = Int32.MaxValue)
        {
            Discord.EmbedBuilder builder = new();

            if (metadata.Title is String title)
                builder.Title = title;

            if (metadata.Description is String description)
                builder.Description = (description.Length > descriptionLength) switch
                {
                    true => description[..(descriptionLength - 3)] + "...",
                    false => description
                };

            if (metadata.Thumbnail is Uri thumbnail)
                builder.ImageUrl = thumbnail.AbsoluteUri;

            if (metadata.Source is Uri source)
                builder.Url = source.AbsoluteUri;

            builder.Timestamp = DateTimeOffset.Now;

            return builder;
        }

        public static Discord.IActivity AsActivity(this Metadata metadata, Int32 descriptionLength = Int32.MaxValue)
        {
            String? title = default;
            String? details = default;
            Discord.ActivityType type = Discord.ActivityType.Playing;
            Discord.ActivityProperties properties = Discord.ActivityProperties.None;

            if (metadata.Title is String metadataTitle)
                title = metadataTitle;
            if (metadata.Description is String metadataDescription)
                details = (metadataDescription.Length > descriptionLength) switch
                {
                    true => metadataDescription[..(descriptionLength - 3)] + "...",
                    false => metadataDescription
                };

            return new Discord.Game(title, type, properties, details);
        }
    }
}
