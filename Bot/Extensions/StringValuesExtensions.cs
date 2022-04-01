using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Extensions
{
    public static class StringValuesExtensions
    {
        public static String AsArgumentString(this IEnumerable<StringValues> arguments) => String
            .Join(' ', arguments.Select((StringValues values) => String.Join(' ', values)));
    }
}
