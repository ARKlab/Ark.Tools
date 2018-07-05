namespace Ark.Tools.Solid.Decorators
{
    public interface IAuthorizer<T>
    {
        void AuthorizeOrThrow(T dto);
    }
}
