// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.FtpClient.Core;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.FtpClient.SftpClient(net10.0)', Before:
namespace Ark.Tools.FtpClient.SftpClient
{
    public sealed class SFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public SFtpClientPoolFactory()
            : base(new SFtpClientConnectionFactory())
        {
        }
    }


=======
namespace Ark.Tools.FtpClient.SftpClient;

public sealed class SFtpClientPoolFactory : DefaultFtpClientPoolFactory
{
    public SFtpClientPoolFactory()
        : base(new SFtpClientConnectionFactory())
    {
    }
>>>>>>> After
    namespace Ark.Tools.FtpClient.SftpClient;

    public sealed class SFtpClientPoolFactory : DefaultFtpClientPoolFactory
    {
        public SFtpClientPoolFactory()
            : base(new SFtpClientConnectionFactory())
        {
        }
    }