namespace Ark.Tools.Solid.Abstractions
{
    public interface IValidator<T>
    {
        void ValidateOrThrow(T dto);
    }
}
