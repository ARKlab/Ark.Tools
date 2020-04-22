using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ark.Tools.Authorization.Requirement
{
    /// <summary>
    /// Implements an <see cref="IAuthorizationRequirement"/> which requires one application permission.
    /// Use of this kind of requirement, require a paired implementation of <see cref="IUserPermissionsProvider{TPermissionEnum}"/>
    /// </summary>
    /// <typeparam name="TPermissionEnum">The enum of possible permissions.</typeparam>
    public class PermissionAuthorizationRequirement<TPermissionEnum> 
        : IAuthorizationRequirement
        where TPermissionEnum : System.Enum
    {
        static PermissionAuthorizationRequirement()
        {
            if (!typeof(TPermissionEnum).IsEnum)
            {
                throw new ArgumentException("TPermissionEnum must be an enumerated type");
            }
        }
        /// <summary>
        /// Creates a new instance of <see cref="PermissionAuthorizationRequirement{TPermissionEnum}"/>.
        /// </summary>
        /// <param name="permission">The required permission</param>
        public PermissionAuthorizationRequirement(TPermissionEnum permission)
        {
            this.Permission = permission;
        }

        /// <summary>
        /// Gets the required permission
        /// </summary>
        public TPermissionEnum Permission { get; }
    }

    /// <summary>
    /// Implements an <see cref="IAuthorizationRequirement"/> which requires one application permission over <typeparamref name="TResource"/>.
    /// Use of this kind of requirement, require a paired implementation of <see cref="IUserPermissionsProvider{TPermissionEnum}"/>
    /// </summary>
    /// <typeparam name="TPermissionEnum">The enum of possible permissions.</typeparam>
    /// <typeparam name="TResource">The resource type the permission refers to.</typeparam>
    public class PermissionAuthorizationRequirement<TPermissionEnum, TResource>
        : PermissionAuthorizationRequirement<TPermissionEnum>
        where TPermissionEnum : System.Enum
    {

        /// <summary>
        /// Creates a new instance of <see cref="PermissionAuthorizationRequirement{TPermissionEnum,TResource}"/>.
        /// </summary>
        /// <param name="permission">The required permission</param>
        public PermissionAuthorizationRequirement(TPermissionEnum permission)
            : base(permission)
        {
        }
    }

    /// <summary>
    /// A provider used to obtain valid permissions on a given <see cref="AuthorizationContext"/>.
    /// </summary>
    /// <typeparam name="TPermissionEnum">The enum of possible permissions.</typeparam>
    public interface IUserPermissionsProvider<TPermissionEnum>
        where TPermissionEnum : System.Enum
    {
        /// <summary>
        /// Returns all valid permissions for the give <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The authentication context.</param>
        /// <returns>The valid permissions for the context.</returns>
        Task<IEnumerable<TPermissionEnum>> GetPermissions(AuthorizationContext context);
    }

    /// <summary>
    /// Implements an <see cref="IAuthorizationHandler"/> which evaluates <see cref="PermissionAuthorizationRequirement{TPermissionEnum}"/>s.
    /// Requires an implementation of <see cref="IUserPermissionsProvider{TPermissionEnum}"/>.
    /// </summary>
    /// <typeparam name="TPermissionEnum">The enum of possible permissions.</typeparam>
    public class PermissionAuthorizationHandler<TPermissionEnum> : IAuthorizationHandler
        where TPermissionEnum : System.Enum
    {
        private IUserPermissionsProvider<TPermissionEnum> _provider;

        static PermissionAuthorizationHandler()
        {
            if (!typeof(TPermissionEnum).IsEnum)
            {
                throw new ArgumentException("TPermissionEnum must be an enumerated type");
            }
        }

        public PermissionAuthorizationHandler(IUserPermissionsProvider<TPermissionEnum> provider)
        {
            _provider = provider;
        }

        public async Task HandleAsync(AuthorizationContext context)
        {
            var permissionType = typeof(PermissionAuthorizationRequirement<TPermissionEnum>);
            if (context.Resource != null)
                permissionType = typeof(PermissionAuthorizationRequirement<,>).MakeGenericType(typeof(TPermissionEnum), context.Resource.GetType());

            var requirements = context.Policy.Requirements.Where(t => permissionType.IsAssignableFrom(t.GetType())).Cast<PermissionAuthorizationRequirement<TPermissionEnum>>().ToArray();
            if (requirements.Length == 0) return;

            var permissions = await _provider.GetPermissions(context);
            if (permissions == null || !permissions.Any()) return;

            foreach (var req in requirements)
            {
                if (permissions.Contains(req.Permission))
                    context.Succeed(req);
            }
        }
    }

    public static partial class Ex
    {
        /// <summary>
        /// Adds a <see cref="PermissionAuthorizationRequirement{TPermissionEnum}"/>
        /// to the current instance. The corresponding handler requires a <see cref="IUserPermissionsProvider{TPermissionEnum}"/> to work.
        /// </summary>
        /// <typeparam name="TPermissionEnum">The  enum of possible permissions.</typeparam>
        /// <param name="builder">The policy builder</param>
        /// <param name="Permission">The required permission</param>
        /// <returns></returns>
        public static AuthorizationPolicyBuilder RequireUserPermission<TPermissionEnum>(this AuthorizationPolicyBuilder builder, TPermissionEnum Permission)
            where TPermissionEnum : System.Enum
        {
            builder.AddRequirements(new PermissionAuthorizationRequirement<TPermissionEnum>(Permission));
            return builder;
        }

        /// <summary>
        /// Adds a <see cref="PermissionAuthorizationRequirement{TPermissionEnum}"/>
        /// to the current instance. The corresponding handler requires a <see cref="IUserPermissionsProvider{TPermissionEnum}"/> to work.
        /// </summary>
        /// <typeparam name="TResource"></typeparam>
        /// <typeparam name="TPermissionEnum">The enum of possible permissions.</typeparam>
        /// <param name="builder">The policy builder.</param>
        /// <param name="Permission">The required permission.</param>
        /// <returns></returns>
        public static AuthorizationPolicyBuilder RequireUserPermission<TResource, TPermissionEnum>(this AuthorizationPolicyBuilder builder, TPermissionEnum Permission)
            where TPermissionEnum : System.Enum
        {
            builder.AddRequirements(new PermissionAuthorizationRequirement<TPermissionEnum, TResource>(Permission));
            return builder;
        }
    }
}