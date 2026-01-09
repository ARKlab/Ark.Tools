// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.FtpClient.Core;

public struct FtpEntry : IEquatable<FtpEntry>
{
    //public ListStyle Style;
    public string Name;
    public string FullPath;
    public DateTime Modified;
    public bool IsDirectory;
    public long Size;

    public override readonly bool Equals(object? obj)
    {
        return obj is FtpEntry entry && Equals(entry);
    }

    public readonly bool Equals(FtpEntry other)
    {
        return Name == other.Name &&
               FullPath == other.FullPath &&
               Modified == other.Modified &&
               IsDirectory == other.IsDirectory &&
               Size == other.Size;
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Name, FullPath, Modified, IsDirectory, Size);
    }

    public static bool operator ==(FtpEntry left, FtpEntry right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FtpEntry left, FtpEntry right)
    {
        return !(left == right);
    }
}