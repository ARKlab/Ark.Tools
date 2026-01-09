// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ark.Tools.FtpClient.SftpClient;

internal static class FtpPathExtensions
{

    public static string GetFtpPath(this string path)
    {
        if (String.IsNullOrEmpty(path))
            return "./";

        path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/", RegexOptions.None, TimeSpan.FromMilliseconds(1000)).TrimEnd('/');
        if (path.Length == 0)
            path = "./";

        return path;
    }

    public static string GetFtpPath(this string path, params string[] segments)
    {
        if (String.IsNullOrEmpty(path))
            path = "./";

        foreach (string part in segments)
        {
            if (part != null)
            {
                if (path.Length > 0 && !path.EndsWith('/'))
                    path += "/";
                path += Regex.Replace(part.Replace('\\', '/'), "[/]+", "/", RegexOptions.None, TimeSpan.FromMilliseconds(1000)).TrimEnd('/');
            }
        }

        path = Regex.Replace(path.Replace('\\', '/'), "[/]+", "/", RegexOptions.None, TimeSpan.FromMilliseconds(1000)).TrimEnd('/');
        if (path.Length == 0)
            path = "./";

        return path;
    }

    public static string? GetFtpFileName(this string? path)
    {
        var tpath = (path == null ? null : path);
        int lastslash = -1;

        if (tpath == null)
            return null;

        lastslash = tpath.LastIndexOf('/');
        if (lastslash < 0)
            return tpath;

        lastslash += 1;
        if (lastslash >= tpath.Length)
            return tpath;

        return tpath[lastslash..];
    }

    public static DateTime GetFtpDate(this string date, DateTimeStyles style)
    {
        string[] formats =
        [
            "yyyyMMddHHmmss",
            "yyyyMMddHHmmss.fff",
            "MMM dd  yyyy",
            "MMM  d  yyyy",
            "MMM dd HH:mm",
            "MMM  d HH:mm",
            "MM-dd-yy  hh:mmtt",
            "MM-dd-yyyy  hh:mmtt"
        ];
        DateTime parsed;

        if (DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, style, out parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }
}