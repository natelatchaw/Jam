using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Bot.Services
{
    public abstract partial class ProcessService
    {
        /// <summary>
        /// The target executable's filename.
        /// </summary>
        protected abstract String FileName { get; }

        /// <summary>
        /// The target executable's path.
        /// </summary>
        protected abstract String Path { get; }

        /// <summary>
        /// A <see cref="System.IO.SearchOption"/> value specifying how to search for the file.
        /// </summary>
        protected abstract SearchOption SearchOption { get; }


        /// <summary>
        /// Gets a <see cref="FileInfo"/> instance representing the target executable.
        /// </summary>
        /// 
        /// <remarks>
        /// The search parameters used are:
        /// <list type="bullet">
        /// <item>The directory referenced by <see cref="Directory"/></item>
        /// <item>A file with a name matching <see cref="FileName"/></item>
        /// <item>Child directories depending on the <see cref="System.IO.SearchOption"/> specified by <see cref="SearchOption"/></item>
        /// </list>
        /// </remarks>
        /// 
        /// <exception cref="ProcessServiceException">
        /// </exception>
        public virtual FileInfo File
        {
            get
            {
                IEnumerable<FileInfo> files = Directory.GetFiles(FileName, SearchOption);
                try
                {
                    if (files.SingleOrDefault() is FileInfo file) return file;
                    List<String> errorDetails = new()
                    {
                        $"Multiple candidates found for {FileName}",
                        $"Search Area: {Directory.FullName}",
                        $"Search Mode: {Enum.GetName(typeof(SearchOption), SearchOption)}",
                    };
                    throw new ProcessServiceException(String.Join('\n', errorDetails));
                }
                catch (InvalidOperationException exception)
                {
                    IEnumerable<String> candidates = files.Select((FileInfo match) => match.FullName);
                    List<String> errorDetails = new()
                    {
                        $"Multiple candidates found for {FileName}",
                        $"Search Area: {Directory.FullName}",
                        $"Search Mode: {Enum.GetName(typeof(SearchOption), SearchOption)}",
                        $"Candidates:\n{String.Join('\n', candidates)}",
                    };
                    throw new ProcessServiceException(String.Join('\n', errorDetails), exception);
                }
            }
        }

        /// <summary>
        /// Gets a <see cref="DirectoryInfo"/> instance representing the target executable's parent directory.
        /// </summary>
        /// 
        /// <remarks>
        /// Checks whether the directory referenced by <see cref="Path"/> exists.
        /// Throws <see cref="ProcessServiceException"/> if the directory does not exist.
        /// </remarks>
        /// 
        /// <exception cref="ProcessServiceException">
        /// </exception>
        public virtual DirectoryInfo Directory
        {
            get
            {
                if (System.IO.Directory.Exists(Path) is false)
                    throw new ProcessServiceException($"Directory '{Path}' does not exist.");
                else
                    return new(Path);
            }
        }


        public abstract ProcessStartInfo GetInfo(IEnumerable<StringValues> args);

        public abstract Process Execute(ProcessStartInfo info);
    }

    public abstract partial class ProcessService : BackgroundService { }

    public class ProcessServiceException : Exception
    {
        public ProcessServiceException(String? message, Exception? innerException = null) : base(message, innerException) { }
    }
}
