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
    public partial class FFmpegService : BackgroundService
    {
        private readonly ILogger<FFmpegService> _logger;
        private readonly IOptions<Options> _options;

        private readonly FileInfo _executable;

        public Options FFmpegOptions => _options.Value;

        public FFmpegService(
            ILogger<FFmpegService> logger,
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
                RedirectStandardInput = true,
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
                throw new FFmpegServiceException($"{nameof(Process)} {info.FileName} failed to start.");

            if (process.HasExited)
                throw new FFmpegServiceException($"{nameof(Process)} {info.FileName} exited prematurely.");

            _logger.LogTrace("{type} {name} started with ID {id}.", nameof(Process), process.ProcessName, process.Id);

            return process;
        }
    }

    public partial class FFmpegService
    {
        private FileInfo GetExecutable()
        {
            try
            {
                if (FFmpegOptions.FileName is not String filename)
                    throw new FFmpegServiceException($"{nameof(FFmpegOptions.FileName)} property in {nameof(FFmpegOptions)} was missing.");

                if (FFmpegOptions.Path is not String path)
                    throw new FFmpegServiceException($"{nameof(FFmpegOptions.Path)} property in {nameof(FFmpegOptions)} was missing.");

                if (Directory.Exists(path) is false)
                    throw new FFmpegServiceException($"Directory '{path}' does not exist.");

                DirectoryInfo directory = new(path);

                SearchOption option = FFmpegOptions.Recursive switch
                {
                    true => SearchOption.AllDirectories,
                    false => SearchOption.TopDirectoryOnly,
                    _ => SearchOption.TopDirectoryOnly,
                };

                IEnumerable<FileInfo> files = directory.GetFiles(filename, option);

                if (files.SingleOrDefault() is not FileInfo file)
                    throw new FFmpegServiceException(String.Join('\n', new List<String>
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
                throw new FFmpegServiceException($"Directory '{FFmpegOptions.Path}' contained multiple matches for '{FFmpegOptions.FileName}'.", exception);
            }
        }
    }

    public partial class FFmpegService
    {
        public class Options
        {
            public Boolean? Recursive { get; set; } = false;

            public String? FileName { get; set; } = "ffmpeg.exe";

            public String? Path { get; set; } = AppDomain.CurrentDomain.BaseDirectory;        
        }
    }

    public class FFmpegServiceException : Exception
    {
        public FFmpegServiceException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FFmpegServiceExtensions
    {
        public static IServiceCollection AddFFmpegService(this IServiceCollection services, IConfigurationSection section) => services
            .AddSingleton<FFmpegService>()
            .Configure<FFmpegService.Options>(section);
    }
}