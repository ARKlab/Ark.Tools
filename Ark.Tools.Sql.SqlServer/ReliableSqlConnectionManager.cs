// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Data;
using Dapper;

using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

namespace Ark.Tools.Sql.SqlServer
{
    public class ReliableSqlConnectionManager : SqlConnectionManager
	{
		protected override SqlConnection Build(string connectionString)
		{
			var opt = new SqlConnectionStringBuilder(connectionString);
			if (!opt.ShouldSerialize("ConnectRetryCount"))
				opt.ConnectRetryCount = 3;
			if (!opt.ShouldSerialize("ConnectRetryInterval"))
				opt.ConnectRetryInterval = 10;
			if (!opt.ShouldSerialize("ConnectTimeout"))
				opt.ConnectTimeout = 30;

			var conn = new SqlConnection(opt.ConnectionString);
			conn.InfoMessage += new SqlInfoMessageEventHandler(OnInfoMessage);
			conn.FireInfoMessageEventOnUserErrors = false;
			return conn;
		}
	}
}