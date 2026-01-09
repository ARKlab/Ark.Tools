using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Activity(net10.0)', Before:
namespace Ark.Tools.Activity.Messages
{
    public class SliceReady : IEquatable<SliceReady>
    {
        public Resource Resource { get; set; }

        public Slice ResourceSlice { get; set; }

        public Slice ActivitySlice { get; set; }

        public bool Equals(SliceReady? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (Equals(other, null))
                return false;

            return Resource == other.Resource
                && ResourceSlice == other.ResourceSlice
                && ActivitySlice == other.ActivitySlice;
        }

        public static bool operator ==(SliceReady x, SliceReady y)
        {
            if (!Equals(x, null))
                return x.Equals(y);
            else if (Equals(y, null))
                return true;
            else
                return false;

        }

        public static bool operator !=(SliceReady x, SliceReady y)
        {
            if (!Equals(x, null))
                return !x.Equals(y);
            else if (Equals(y, null))
                return false;
            else
                return true;
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is SliceReady))
                return false;

            return Equals((SliceReady)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 7243;
                hash = hash * 92821 + Resource.GetHashCode();
                hash = hash * 92821 + ResourceSlice.GetHashCode();
                hash = hash * 92821 + ActivitySlice.GetHashCode();
                return hash;
            }
=======
namespace Ark.Tools.Activity.Messages;

public class SliceReady : IEquatable<SliceReady>
{
    public Resource Resource { get; set; }

    public Slice ResourceSlice { get; set; }

    public Slice ActivitySlice { get; set; }

    public bool Equals(SliceReady? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (Equals(other, null))
            return false;

        return Resource == other.Resource
            && ResourceSlice == other.ResourceSlice
            && ActivitySlice == other.ActivitySlice;
    }

    public static bool operator ==(SliceReady x, SliceReady y)
    {
        if (!Equals(x, null))
            return x.Equals(y);
        else if (Equals(y, null))
            return true;
        else
            return false;

    }

    public static bool operator !=(SliceReady x, SliceReady y)
    {
        if (!Equals(x, null))
            return !x.Equals(y);
        else if (Equals(y, null))
            return false;
        else
            return true;
    }

    public override bool Equals(object? obj)
    {
        if (!(obj is SliceReady))
            return false;

        return Equals((SliceReady)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 7243;
            hash = hash * 92821 + Resource.GetHashCode();
            hash = hash * 92821 + ResourceSlice.GetHashCode();
            hash = hash * 92821 + ActivitySlice.GetHashCode();
            return hash;
>>>>>>> After


namespace Ark.Tools.Activity.Messages;

public class SliceReady : IEquatable<SliceReady>
{
    public Resource Resource { get; set; }

    public Slice ResourceSlice { get; set; }

    public Slice ActivitySlice { get; set; }

    public bool Equals(SliceReady? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (Equals(other, null))
            return false;

        return Resource == other.Resource
            && ResourceSlice == other.ResourceSlice
            && ActivitySlice == other.ActivitySlice;
    }

    public static bool operator ==(SliceReady x, SliceReady y)
    {
        if (!Equals(x, null))
            return x.Equals(y);
        else if (Equals(y, null))
            return true;
        else
            return false;

    }

    public static bool operator !=(SliceReady x, SliceReady y)
    {
        if (!Equals(x, null))
            return !x.Equals(y);
        else if (Equals(y, null))
            return false;
        else
            return true;
    }

    public override bool Equals(object? obj)
    {
        if (!(obj is SliceReady))
            return false;

        return Equals((SliceReady)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 7243;
            hash = hash * 92821 + Resource.GetHashCode();
            hash = hash * 92821 + ResourceSlice.GetHashCode();
            hash = hash * 92821 + ActivitySlice.GetHashCode();
            return hash;
        }
    }
}