using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace Microsoft.Extensions.Hosting
{
    public static class IHostBuilderExtensions
    {
        public static IHostBuilder UseStartup<TStartup>(this IHostBuilder hostBuilder)
            where TStartup : class
        {
            // Invoke the ConfigureServices method on IHostBuilder
            return hostBuilder.ConfigureServices((HostBuilderContext context, IServiceCollection services) =>
            {
                // Get the constructor the the StartupType
                ConstructorInfo? constructor = typeof(TStartup)
                    .GetConstructor(new Type[] { typeof(IConfiguration) });

                // Initialize a MethodInfo for a ConfigureServices method
                MethodInfo? configureServices = null;
                // Initialize an array of Objects for parameter specification
                Object[]? parameters = null;

                // Get the MethodInfo for the ConfigureServices method on StartupType
                if (configureServices is null)
                {
                    configureServices = typeof(TStartup)
                        .GetMethod("ConfigureServices", new Type[] { typeof(HostBuilderContext), typeof(IServiceCollection) });
                    parameters = new Object[] { context, services };
                }

                // Get the MethodInfo for the ConfigureServices method on StartupType
                if (configureServices is null)
                {
                    configureServices = typeof(TStartup)
                        .GetMethod("ConfigureServices", new Type[] { typeof(IServiceCollection) });
                    parameters = new Object[] { services };
                }

                if (constructor != null)
                {
                    // Create an instance of StartupType
                    Object instance = (TStartup)Activator.CreateInstance(typeof(TStartup), context.Configuration);
                    // Invoke StartupType.ConfigureServices
                    Object result = configureServices?.Invoke(instance, parameters);
                }
                else
                {
                    // Create an instance of StartupType
                    Object instance = (TStartup)Activator.CreateInstance(typeof(TStartup), null);
                    // Invoke StartupType.ConfigureServices
                    Object result = configureServices?.Invoke(instance, parameters);
                }
            });
        }
    }
}