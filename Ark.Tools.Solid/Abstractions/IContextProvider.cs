namespace Ark.Tools.Solid.Abstractions
{
    public interface IContextProvider<TItem>
        where TItem : class
    {
        TItem Current { get; }
    }
}
