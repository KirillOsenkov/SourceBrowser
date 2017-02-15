﻿using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.SourceBrowser.Common
{
    public static class Paths
    {
        public static string BaseAppFolder
        {
            get
            {
                return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            }
        }

        public static string QuoteIfNeeded(this string path)
        {
            if (path != null && path.Contains(" "))
            {
                path = "\"" + path + "\"";
            }

            return path;
        }

        public static string TrimSlash(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.TrimEnd('\\');
        }

        public static string MustBeAbsolute(this string path)
        {
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException($"Path '{path}' is not absolute.", nameof(path));
            }

            return path;
        }
    }
}
