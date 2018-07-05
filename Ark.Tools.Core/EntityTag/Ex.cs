namespace Ark.Tools.Core.EntityTag
{
    public static class Ex
    {
        public static void VerifyETag(this IEntityWithETag newEntity, IEntityWithETag existingEntity)
        {
            if (existingEntity == null)
                if (newEntity._ETag == "*")
                    throw new EntityTagMismatchException("Entity already exists.");
                else return;

            if (!string.IsNullOrEmpty(newEntity?._ETag))
                if (existingEntity?._ETag != newEntity._ETag)
                    throw new EntityTagMismatchException("Entity tags mismatch.");
        }
    }
}
