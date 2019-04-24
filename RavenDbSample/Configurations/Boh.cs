using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;


namespace RavenDbSample.Configurations
{


	//public class ODataQueryOptionsModelBinder : IModelBinder
	//{
	//	private struct AsyncVoid
	//	{
	//	}

	//	private static MethodInfo _createODataQueryOptions = typeof(ODataQueryOptionsModelBinder).GetMethod("CreateODataQueryOptions");

	//	private static readonly Task _defaultCompleted = Task.FromResult<AsyncVoid>(default(AsyncVoid));

	//	public Task BindModelAsync(ModelBindingContext bindingContext)
	//	{
	//		if (bindingContext == null)
	//		{
	//			throw new ArgumentNullException("bindingContext");
	//		}

	//		var request = bindingContext.HttpContext.Request;
	//		if (request == null)
	//		{
	//			throw new ArgumentNullException("actionContext");
	//		}

	//		var actionDescriptor = bindingContext.ActionContext.ActionDescriptor;
	//		if (actionDescriptor == null)
	//		{
	//			throw new ArgumentNullException("actionDescriptor");
	//		}


	//		Type entityClrType = GetEntityClrTypeFromParameterType(actionDescriptor) 
	//			?? GetEntityClrTypeFromActionReturnType(actionDescriptor as ControllerActionDescriptor);

	//		IEdmModel model = actionDescriptor.GetEdmModel(entityClrType);
	//		ODataQueryContext entitySetContext = new ODataQueryContext(model, entityClrType);
	//		ODataQueryOptions parameterValue = CreateODataQueryOptions(entitySetContext, request, entityClrType);
	//		bindingContext.Result = ModelBindingResult.Success(parameterValue);

	//		return _defaultCompleted;
	//	}

	//	private static ODataQueryOptions CreateODataQueryOptions(ODataQueryContext ctx, HttpRequest req, Type entityClrType)
	//	{
	//		var method = _createODataQueryOptions.MakeGenericMethod(entityClrType);
	//		var res = method.Invoke(null, new object[] { ctx, req }) as ODataQueryOptions;
	//		return res;
	//	}

	//	public static ODataQueryOptions<T> CreateODataQueryOptions<T>(ODataQueryContext context, HttpRequest request)
	//	{
	//		var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, request.Scheme + "://" + request.Host + request.Path + request.QueryString);
	//		return new ODataQueryOptions<T>(context, req);
	//	}

	//	internal static Type GetEntityClrTypeFromActionReturnType(ControllerActionDescriptor actionDescriptor)
	//	{
	//		if (actionDescriptor.MethodInfo.ReturnType == null)
	//		{
	//			throw new Exception("Cannot use ODataQueryOptions when return type is null");
	//		}

	//		return TypeHelper.GetImplementedIEnumerableType(actionDescriptor.MethodInfo.ReturnType);
	//	}

	//	internal static Type GetEntityClrTypeFromParameterType(ActionDescriptor parameterDescriptor)
	//	{
	//		Type parameterType = parameterDescriptor.Parameters.First(x => x.ParameterType == typeof(ODataQueryOptions) || x.ParameterType.IsSubclassOf<ODataQueryOptions>()).ParameterType;

	//		if (parameterType.IsGenericType &&
	//			parameterType.GetGenericTypeDefinition() == typeof(ODataQueryOptions<>))
	//		{
	//			return parameterType.GetGenericArguments().Single();
	//		}

	//		return null;
	//	}
	//}
	//public static class ODataModelHelper
	//{
	//	private const string ModelKeyPrefix = "MS_EdmModel";

	//	private static System.Web.Http.HttpConfiguration configuration = new System.Web.Http.HttpConfiguration();

	//	internal static Microsoft.Data.Edm.IEdmModel GetEdmModel(this ActionDescriptor actionDescriptor, Type entityClrType)
	//	{
	//		if (actionDescriptor == null)
	//		{
	//			throw new ArgumentNullException("actionDescriptor");
	//		}

	//		if (entityClrType == null)
	//		{
	//			throw new ArgumentNullException("entityClrType");
	//		}

	//		if (actionDescriptor.Properties.ContainsKey(ModelKeyPrefix + entityClrType.FullName))
	//		{
	//			return actionDescriptor.Properties[ModelKeyPrefix + entityClrType.FullName] as Microsoft.Data.Edm.IEdmModel;
	//		}
	//		else
	//		{
	//			ODataConventionModelBuilder builder = new ODataConventionModelBuilder(ODataModelHelper.configuration, isQueryCompositionMode: true);
	//			EntityTypeConfiguration entityTypeConfiguration = builder.AddEntity(entityClrType);
	//			builder.AddEntitySet(entityClrType.Name, entityTypeConfiguration);
	//			Microsoft.Data.Edm.IEdmModel edmModel = builder.GetEdmModel();
	//			actionDescriptor.Properties[ModelKeyPrefix + entityClrType.FullName] = edmModel;
	//			return edmModel;

	//		}
	//	}
	//}
}
