using Bot.Extensions;
using Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{
    public partial class YouTubeDLService : BackgroundService
    {
        private readonly ILogger<YouTubeDLService> _logger;
        private readonly IOptions<Options> _options;

        private readonly FileInfo _executable;

        public Options YouTubeDLOptions => _options.Value;

        public YouTubeDLService(
            ILogger<YouTubeDLService> logger,
            IOptions<Options> options
        )
        {
            _logger = logger;
            _options = options;
            _executable = GetExecutable();
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);

        public Task<ProcessStartInfo> GetProcessInfo(IEnumerable<StringValues> arguments)
        {
            ProcessStartInfo info = new()
            {
                FileName = _executable.Name,
                WorkingDirectory = _executable.DirectoryName,
                Arguments = arguments.AsArgumentString(),
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            return Task.FromResult(info);
        }

        public async Task<Process> StartProcessAsync(IEnumerable<StringValues> arguments)
        {
            ProcessStartInfo info = await GetProcessInfo(arguments);

            if (Process.Start(info) is not Process process)
                throw new YouTubeDLServiceException($"{nameof(Process)} {info.FileName} failed to start.");

            if (process.HasExited)
                throw new YouTubeDLServiceException($"{nameof(Process)} {info.FileName} exited prematurely.");

            _logger.LogTrace("{type} {name} started with ID {id}.", nameof(Process), process.ProcessName, process.Id);

            return process;
        }
    }

    public partial class YouTubeDLService
    {
        private FileInfo GetExecutable()
        {
            try
            {
                if (YouTubeDLOptions.FileName is not String filename)
                    throw new YouTubeDLServiceException($"{nameof(YouTubeDLOptions.FileName)} property in {nameof(YouTubeDLOptions)} was missing.");

                if (YouTubeDLOptions.Path is not String path)
                    throw new YouTubeDLServiceException($"{nameof(YouTubeDLOptions.Path)} property in {nameof(YouTubeDLOptions)} was missing.");

                if (Directory.Exists(path) is false)
                    throw new YouTubeDLServiceException($"Directory '{path}' does not exist.");

                DirectoryInfo directory = new(path);

                SearchOption option = YouTubeDLOptions.Recursive switch
                {
                    true => SearchOption.AllDirectories,
                    false => SearchOption.TopDirectoryOnly,
                    _ => SearchOption.TopDirectoryOnly,
                };

                IEnumerable<FileInfo> files = directory.GetFiles(filename, option);

                if (files.SingleOrDefault() is not FileInfo file)
                    throw new YouTubeDLServiceException(String.Join('\n', new List<String>
                    {
                        $"Directory '{directory.Name}' contained no files matching '{filename}'.",
                        $"Full Path: {directory.FullName}",
                        $"Search Mode: {Enum.GetName(typeof(SearchOption), option)}",
                    }));

                _logger.LogInformation("{fileName} was located in {directoryName}", file.Name, file.DirectoryName);

                return file;
            }
            catch (InvalidOperationException exception)
            {
                throw new YouTubeDLServiceException($"Directory '{YouTubeDLOptions.Path}' contained multiple matches for '{YouTubeDLOptions.FileName}'.", exception);
            }
        }
    }

    public partial class YouTubeDLService
    {
        public class Options
        {
            public Boolean? Recursive { get; set; } = false;

            public String? FileName { get; set; } = "youtube-dl.exe";

            public String? Path { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public class YouTubeDLServiceException : Exception
    {
        public YouTubeDLServiceException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class YouTubeDLServiceExtensions
    {
        public static IServiceCollection AddYouTubeDLService(this IServiceCollection services, IConfigurationSection section) => services
            .AddSingleton<YouTubeDLService>()
            .Configure<YouTubeDLService.Options>(section);
    }
}