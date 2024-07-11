using Ark.Reference.Common;
using Ark.Reference.Common.Services.FileStorageService;

using Microsoft.Extensions.Configuration;

using System;

namespace Ark.Reference.Core.Application.Config
{
    public class ApiHostConfig : IApiHostConfig
    {
        public string ApiHostConfig_SwaggerClientId { get; set; }
        public string RebusBusConfig_AsbConnectionString { get; set; }
        public string RebusBusConfig_RequestQueue { get; set; }
        public string RebusBusConfig_StorageConnectionString { get; set; }
        public string CoreDataContextConfig_SQLConnectionString { get; set; }

        public string CoreConfig_Environment { get; set; }

        public string ArtesianConnectionConfig_Audience { get; set; }
        public string ArtesianConnectionConfig_Domain { get; set; }
        public string ArtesianConnectionConfig_ClientSecret { get; set; }
        public string ArtesianConnectionConfig_ClientId { get; set; }
        public Uri ArtesianConnectionConfig_BaseAddress { get; set; }
        public string ArtesianConnectionConfig_ApiKey { get; set; }

        public string FileServiceStorageAccount { get; set; }
        public string FileServiceStoragePrefix { get; set; }

        public string TableName => "Outbox";
        public string SchemaName => "dbo";

        string IApiHostConfig.SwaggerClientId => ApiHostConfig_SwaggerClientId;
        string IRebusBusConfig.AsbConnectionString => RebusBusConfig_AsbConnectionString;
        string IRebusBusConfig.RequestQueue => RebusBusConfig_RequestQueue;
        string IRebusBusConfig.StorageConnectionString => RebusBusConfig_StorageConnectionString;
        string ICoreDataContextConfig.SQLConnectionString => CoreDataContextConfig_SQLConnectionString;

        string ICoreConfig.Environment => CoreConfig_Environment;

        string IFileStorageServiceConfig.StorageAccount => FileServiceStorageAccount;
        string IFileStorageServiceConfig.StoragePrefix => FileServiceStoragePrefix;
    }

    public static class Ex
    {
        public static ApiHostConfig AddRebusBusConfig(this ApiHostConfig @this, IConfiguration configuration)
        {
            var CoreQueue = configuration["ConnectionStrings:Core.Queue"];
            @this.RebusBusConfig_AsbConnectionString = configuration["ConnectionStrings:Core.AzureServiceBus"];
            @this.RebusBusConfig_RequestQueue = String.IsNullOrEmpty(CoreQueue) ? "Core.Queue" : CoreQueue;
            @this.RebusBusConfig_StorageConnectionString = configuration["ConnectionStrings:Core.StorageAccount"];
            return @this;
        }

        public static ApiHostConfig AddCoreDataContextConfig(this ApiHostConfig @this, IConfiguration configuration)
        {
            @this.CoreDataContextConfig_SQLConnectionString = configuration["ConnectionStrings:Core.Database"];
            return @this;
        }

        public static ApiHostConfig AddCoreConfig(this ApiHostConfig @this, IConfiguration configuration)
        {
            @this.CoreConfig_Environment = configuration["Environment"];
            return @this;
        }

        public static ApiHostConfig AddFileStorageServiceConfig(this ApiHostConfig @this, IConfiguration configuration)
        {
            @this.FileServiceStorageAccount = configuration["ConnectionStrings:Core.StorageAccount"];
            @this.FileServiceStoragePrefix = CommonConstants.FileStorageContainer;
            return @this;
        }

    }
}
