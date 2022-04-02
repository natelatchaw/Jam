using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Bot.Interfaces
{
    public interface IProcessService
    {
        public FileInfo GetFileInfo();

        public ProcessStartInfo GetStartInfo(IEnumerable<StringValues> args);

        public Process Execute(ProcessStartInfo info);
    }
}
