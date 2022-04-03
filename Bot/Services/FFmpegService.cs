using Bot.Extensions;
using Bot.Models;
using Bot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{
    public partial class FFmpegService : ProcessService
    {
        private readonly ILogger<FFmpegService> _logger;
        private readonly IOptions<Options> _options;
        private readonly IOptions<Credential> _credential;


        public FFmpegService(
            ILogger<FFmpegService> logger,
            IOptions<Options> options,
            IOptions<Credential> credential
        )
        {
            _logger = logger;
            _options = options;
            _credential = credential;
        }


        protected override String FileName
        {
            get
            {
                if (_options.Value.FileName is not String fileName)
                    throw new FFmpegServiceException($"{nameof(_options.Value.FileName)} property in {nameof(FFmpegService)}.{nameof(Options)} was missing.");
                else
                    return fileName;
            }
        }

        protected override String Path
        {
            get
            {
                if (_options.Value.Path is not String path)
                    throw new FFmpegServiceException($"{nameof(_options.Value.FileName)} property in {nameof(FFmpegService)}.{nameof(Options)} was missing.");
                else
                    return path;
            }
        }

        protected override SearchOption SearchOption
        {
            get
            {
                if (_options.Value.Recursive is not Boolean recursive)
                    throw new FFmpegServiceException($"{nameof(_options.Value.Recursive)} property in {nameof(FFmpegService)}.{nameof(Options)} was missing.");
                return recursive switch
                {
                    true => SearchOption.AllDirectories,
                    false => SearchOption.TopDirectoryOnly,
                };
            }
        }


        public override DirectoryInfo Directory
        {
            get
            {
                try
                {
                    DirectoryInfo directory = base.Directory;
                    _logger.LogTrace("Located directory {directoryName}", directory.FullName);
                    return directory;
                }
                catch (ProcessServiceException exception)
                {
                    throw new FFmpegServiceException(exception.Message, exception);
                }
            }
        }

        public override FileInfo File
        {
            get
            {
                try
                {
                    FileInfo file = base.File;
                    _logger.LogTrace("{fileName} was located in {directoryName}", file.Name, file.DirectoryName);
                    return file;
                }
                catch (ProcessServiceException exception)
                {
                    throw new FFmpegServiceException(exception.Message, exception);
                }
            }
        }


        public override ProcessStartInfo GetInfo(IEnumerable<StringValues> args)
        {
            FileInfo fileInfo = File;
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

        public override Process Execute(ProcessStartInfo info)
        {
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
        public class Options
        {
            public Boolean? Recursive { get; set; } = false;

            public String? FileName { get; set; } = "ffmpeg.exe";

            public String? Path { get; set; } = AppDomain.CurrentDomain.BaseDirectory;        
        }
    }

    public partial class FFmpegService : ProcessService
    {
        protected override Task ExecuteAsync(CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);
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