using System;

namespace Ark.Tools.Activity
{
    public struct Resource : IEquatable<Resource>
    {
        public Resource(string provider, string id)
        {
            Provider = provider;
            Id = id;
        }

        public string Provider;
        public string Id;

        public static Resource Create(string provider, string resourceId)
        {
            return new Resource(provider, resourceId);
        }

        public readonly bool Equals(Resource other)
        {
            return string.Equals(Provider, other.Provider, StringComparison.InvariantCultureIgnoreCase)
                && string.Equals(Id, other.Id, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool operator ==(Resource x, Resource y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Resource x, Resource y)
        {
            return !x.Equals(y);
        }

        public override readonly bool Equals(object? obj)
        {
            if (!(obj is Resource))
                return false;

            return Equals((Resource)obj);
        }

        public override readonly int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + Provider.GetHashCode(StringComparison.OrdinalIgnoreCase);
                hash = hash * 92821 + Id.GetHashCode(StringComparison.OrdinalIgnoreCase);
                return hash;
            }
        }

        public override readonly string ToString()
        {
            return string.Format(null, "{0}.{1}", Provider, Id);
        }
    }
}
