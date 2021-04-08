using Dapper;

namespace Ark.Tools.Nodatime.Dapper
{
    public static class NodaTimeDapper
    {
        public static void Setup()
        {
            NodeTimeConverter.Register();
            SqlMapper.AddTypeHandler(InstantHandler.Instance);
            SqlMapper.AddTypeHandler(LocalDateHandler.Instance);
            SqlMapper.AddTypeHandler(LocalDateTimeHandler.Instance);
            SqlMapper.AddTypeHandler(LocalTimeHandler.Instance);
            SqlMapper.AddTypeHandler(OffsetDateTimeHandler.Instance);
        }
    }
}
