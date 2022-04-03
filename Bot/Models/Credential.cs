using Bot.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace Bot.Models
{
    public class Credential
    {
        public String? Domain { get; set; }
        public String? Username { get; set; }
        public String? Password { get; set; }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CredentialExtensions
    {
        public static IServiceCollection AddCredential(this IServiceCollection services, IConfigurationSection section) => services
            .Configure<Credential>(section);
    }
}
