namespace Ark.Reference.Common.Dto
{
    public interface IChanges<out T>
    {
        T? Pre { get; }
        T? Cur { get; }
    }

    public class Changes<TObject>
    {
        public class V1 : IChanges<TObject>
        {
            public TObject? Pre { get; set; }
            public TObject? Cur { get; set; }

            public static V1 From((TObject? pre, TObject? cur) input)
            {
                return new V1()
                {
                    Pre = input.pre,
                    Cur = input.cur
                };
            }
        }
    }
}
