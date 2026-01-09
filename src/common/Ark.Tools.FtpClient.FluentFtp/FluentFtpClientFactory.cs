// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.FluentFtp(net10.0)', Before:
namespace Ark.Tools.FtpClient.FluentFtp
{
    public sealed class FluentFtpClientFactory : DefaultFtpClientFactory
    {
        public FluentFtpClientFactory()
            : base(new FluentFtpClientConnectionFactory())
        {
        }
=======
namespace Ark.Tools.FtpClient.FluentFtp;

public sealed class FluentFtpClientFactory : DefaultFtpClientFactory
{
    public FluentFtpClientFactory()
        : base(new FluentFtpClientConnectionFactory())
    {
>>>>>>> After


namespace Ark.Tools.FtpClient.FluentFtp;

    public sealed class FluentFtpClientFactory : DefaultFtpClientFactory
    {
        public FluentFtpClientFactory()
            : base(new FluentFtpClientConnectionFactory())
        {
        }
    }