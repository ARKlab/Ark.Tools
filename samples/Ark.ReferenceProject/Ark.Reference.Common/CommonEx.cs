using Ark.Reference.Common.Dto;

namespace Ark.Reference.Common
{
    public static class CommonEx
    {
        public static IChanges<TObject> ToChanges<TObject>(this (TObject? pre, TObject? cur) input)
        {
            return new Changes<TObject>.V1()
            {
                Pre = input.pre,
                Cur = input.cur
            };
        }
    }
}
