using Dapper;

using System.Reflection;

namespace Ark.Tools.Nodatime.Dapper
{
    public static class NodaTimeDapper
    {
        public static void Setup(InstantHandlerType instantHandlerType = InstantHandlerType.DateTime)
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

                default:
                    SqlMapper.AddTypeHandler(InstantHandler.Instance);
                    break;
            }

            SqlMapper.AddTypeHandler(LocalDateHandler.Instance);
            SqlMapper.AddTypeHandler(LocalDateTimeHandler.Instance);
            SqlMapper.AddTypeHandler(LocalTimeHandler.Instance);
            SqlMapper.AddTypeHandler(OffsetDateTimeHandler.Instance);
        }
    }
}
