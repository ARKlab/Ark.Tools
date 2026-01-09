using Dapper;


namespace Ark.Tools.Nodatime.Dapper;

public static class NodaTimeDapper
{
    public static void Setup()
        => Setup(InstantHandlerType.DateTime);

    public static void Setup(InstantHandlerType instantHandlerType)
    {
        NodeTimeConverter.Register();

        switch (instantHandlerType)
        {
            case InstantHandlerType.Int64Ticks:
                SqlMapper.AddTypeHandler(InstantTickHandler.Instance);
                break;

            case InstantHandlerType.Int64Milliseconds:
                SqlMapper.AddTypeHandler(InstantMillisecondHandler.Instance);
                break;

            case InstantHandlerType.Int64Seconds:
                SqlMapper.AddTypeHandler(InstantSecondHandler.Instance);
                break;

            case InstantHandlerType.DateTime:
                SqlMapper.AddTypeHandler(InstantHandler.Instance);
                break;

            default:
                throw new NotSupportedException();
        }

        SqlMapper.AddTypeHandler(LocalDateHandler.Instance);
        SqlMapper.AddTypeHandler(LocalDateTimeHandler.Instance);
        SqlMapper.AddTypeHandler(LocalTimeHandler.Instance);
        SqlMapper.AddTypeHandler(OffsetDateTimeHandler.Instance);
    }
}