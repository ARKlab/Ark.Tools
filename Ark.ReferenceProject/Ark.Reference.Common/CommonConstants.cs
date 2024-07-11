

using NodaTime;

namespace Ark.Reference.Common
{
    public static class CommonConstants
	{
        public const string TimeZone = "CET";
        public static readonly DateTimeZone Tz = DateTimeZoneProviders.Tzdb[TimeZone];

        public const string RebusDataBusContainerName = "rebus-databus";

        public const string FileStorageContainer = "files-container";

        public const string ZipFileExtension = "application/zip";
        public const string ZipFileExtensionOld = "application/x-zip-compressed";

        public const string ExcelFileExtension = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }
}
