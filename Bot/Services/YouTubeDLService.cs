using Bot.Extensions;
using Bot.Interfaces;
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
    public partial class YouTubeDLService : IProcessService
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
        }


        public String FileName
        {
            get
            {
                if (_options.Value.FileName is not String fileName)
                    throw new YouTubeDLServiceException($"{nameof(_options.Value.FileName)} property in {nameof(YouTubeDLService)}.{nameof(Options)} was missing.");
                else
                    return fileName;
            }
        }

        public DirectoryInfo Directory
        {
            get
            {
                if (_options.Value.Path is not String path)
                    throw new YouTubeDLServiceException($"{nameof(_options.Value.FileName)} property in {nameof(YouTubeDLService)}.{nameof(Options)} was missing.");
                else if (System.IO.Directory.Exists(path) is false)
                    throw new YouTubeDLServiceException($"Directory '{path}' does not exist.");
                else
                    return new(path);
            }
        }

        public SearchOption SearchOption
        {
            get
            {
                if (_options.Value.Recursive is not Boolean recursive)
                    throw new YouTubeDLServiceException($"{nameof(_options.Value.Recursive)} property in {nameof(YouTubeDLService)}.{nameof(Options)} was missing.");
                return recursive switch
                {
                    true => SearchOption.AllDirectories,
                    false => SearchOption.TopDirectoryOnly,
                };
            }
        }


        public FileInfo GetFileInfo()
        {
            IEnumerable<FileInfo> files = Directory.GetFiles(FileName, SearchOption);
            try
            {
                if (files.SingleOrDefault() is not FileInfo file)
                {
                    List<String> errorDetails = new()
                    {
                        $"Failed to locate {FileName}",
                        $"Search Area: {Directory.FullName}",
                        $"Search Mode: {Enum.GetName(typeof(SearchOption), SearchOption)}",
                    };
                    throw new YouTubeDLServiceException(String.Join('\n', errorDetails));
                }
                _logger.LogTrace("{fileName} was located in {directoryName}", file.Name, file.DirectoryName);
                return file;
            }
            catch (InvalidOperationException exception)
            {
                IEnumerable<String> candidates = files.Select((FileInfo match) => match.FullName);
                List<String> errorDetails = new()
                {
                    $"Multiple candidates found for {FileName}",
                    $"Candidates:\n{String.Join('\n', candidates)}",
                    $"Search Mode: {Enum.GetName(typeof(SearchOption), SearchOption)}",
                };
                throw new YouTubeDLServiceException(String.Join('\n', errorDetails), exception);
            }
        }

        public ProcessStartInfo GetStartInfo(IEnumerable<StringValues> args)
        {
            FileInfo fileInfo = GetFileInfo();
            return new()
            {
                FileName = fileInfo.Name,
                WorkingDirectory = fileInfo.DirectoryName,
                Arguments = args.AsArgumentString(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
        }

        public Process Execute(ProcessStartInfo info)
        {
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
        public class Options
        {
            public Boolean? Recursive { get; set; } = false;

            public String? FileName { get; set; } = "youtube-dl.exe";

            public String? Path { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public partial class YouTubeDLService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);
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