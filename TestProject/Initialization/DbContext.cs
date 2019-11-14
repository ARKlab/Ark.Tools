using NLog;
using TechTalk.SpecFlow;
using System.Linq;
using System;
using System.Data.SqlClient;
//using Microsoft.SqlServer.Dac;
using Dapper;

namespace TestProject
{
	//[Binding]
	//public class TestDbContextSQL
	//{
	//	private static Logger _logger = LogManager.GetCurrentClassLogger();

	//	const string DatabaseConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;";
	//	public static readonly string SqlConnectionString = $@"{DatabaseConnectionString}Initial Catalog=ER.Metering.Database;";

	//	[BeforeTestRun(Order = 0)]
	//	public static void TestEnvironmentInitializationSQL()
	//	{
	//		var instance = new DacServices(DatabaseConnectionString);
	//		using (var dacpac = DacPackage.Load("ER.Company.MeteringService.Repository.dacpac"))
	//		{
	//			instance.Deploy(dacpac, "ER.Metering.Database", true, new DacDeployOptions()
	//			{
	//				CreateNewDatabase = true,
	//			});
	//		}
	//	}

	//	[BeforeScenario]
	//	public void ResetConfigsOnEachScenario(FeatureContext fctx)
	//	{
	//		if (fctx.FeatureInfo.Tags.Contains("ResetToDefaultConfigurationsOnEachScenario"))
	//			ResetConfigs();
	//	}

	//	[BeforeScenario]
	//	public void ResetConfigs()
	//	{
	//		_logger.Info(@"ResetConfigs all Database");

	//		using (var conn = new SqlConnection(SqlConnectionString))
	//		{
	//			conn.Execute(
	//				@"[core].[sp_ResetFull_onlyForTesting]",
	//				new
	//				{
	//					areYouReallySure = true,
	//					resetConfig = true,
	//					cleanHistory = true
	//				},
	//				commandType: System.Data.CommandType.StoredProcedure,
	//				commandTimeout: 60);
	//		}
	//	}
	//}
}