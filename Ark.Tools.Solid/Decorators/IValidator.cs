namespace Ark.Tools.Solid.Decorators
{
    public interface IValidator<T>
    {
        void ValidateOrThrow(T dto);
    }
}
