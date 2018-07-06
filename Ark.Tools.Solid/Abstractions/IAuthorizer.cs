namespace Ark.Tools.Solid.Abstractions

{
    public interface IAuthorizer<T>
    {
        void AuthorizeOrThrow(T dto);
    }
}
