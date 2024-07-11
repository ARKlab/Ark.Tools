using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Ark.Reference.Common.Dto.Enum
{
    //**********************************************
    //** Common ************************************
    //**********************************************

    public enum ContainerType
    {
        Files
    }

    public enum Commodity
    {
        NotSet = 0
        , Power = 1
        , Gas = 2
    }

    //**********************************************
    //** SourceCurve *******************************
    //**********************************************

    public enum SourceCurveType
    {
        NotSet
        , Meter
    }

    public enum SourceCurveContent
    {
        NotSet
        , APlus
        , AMinus
        , RcPlus
        , RcMinus
        , RiPlus
        , RiMinus
        , Bt
        , BtLettura
    }

    public enum SourceCurveContentUOM
    {
        NotSet
        , KWh
        , MWh
        , Smc
        , MW
    }

    public enum SourceCurveGranularity
    {
        NotSet
        , Hour
        , Day
        , Month
        , FifteenMinute,
        Year
    }
}