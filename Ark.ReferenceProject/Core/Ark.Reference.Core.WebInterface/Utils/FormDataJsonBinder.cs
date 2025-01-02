
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ark.Reference.Core.WebInterface.Utils
{
    public class FormDataJsonBinder : IModelBinder
    {
        private readonly JsonSerializerOptions _options;

        public FormDataJsonBinder(FormatterCollection<IInputFormatter> inputFormatters)
        {
            _options = inputFormatters.OfType<SystemTextJsonInputFormatter>().FirstOrDefault()?.SerializerOptions ?? ArkSerializerOptions.JsonOptions;
        }


        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            string modelBindingKey;
            if (bindingContext.IsTopLevelObject)
            {
                modelBindingKey = bindingContext.BinderModelName ?? string.Empty;
            }
            else
            {
                modelBindingKey = bindingContext.ModelName;
            }

            // Fetch the value of the argument by name and set it to the model state
            string fieldName = bindingContext.FieldName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(fieldName);
            if (valueProviderResult == ValueProviderResult.None) return Task.CompletedTask;
            else bindingContext.ModelState.SetModelValue(fieldName, valueProviderResult);

            // Do nothing if the value is null or empty
            string? value = valueProviderResult.FirstValue;
            if (string.IsNullOrEmpty(value)) return Task.CompletedTask;

            try
            {
                // Deserialize the provided value and set the binding result
                object? result = System.Text.Json.JsonSerializer.Deserialize(value, bindingContext.ModelType, _options);
                bindingContext.Result = ModelBindingResult.Success(result);

            }
            catch (System.Text.Json.JsonException ex1)
            {
                bindingContext.ModelState.TryAddModelException(modelBindingKey, ex1);
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }

    public class FormDataJsonBinderProvider : IModelBinderProvider
    {
        private readonly FormatterCollection<IInputFormatter> _inputFormatters;

        public FormDataJsonBinderProvider(FormatterCollection<IInputFormatter> inputFormatters)
        {
            _inputFormatters = inputFormatters;
        }

        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Do not use this provider for binding simple values
            if (!context.Metadata.IsComplexType)
                return null;

            // Do not use this provider if the binding target is not a property
            var propName = context.Metadata.PropertyName;
            if (propName == null)
                return null;

            var propInfo = context.Metadata.ContainerType?.GetProperty(propName);
            if (propInfo == null)
                return null;

            // Do not use this provider if the target property type implements IFormFile
            if (propInfo.PropertyType.IsAssignableFrom(typeof(IFormFile)))
                return null;

            // Do not use this provider if this property does not have the FromForm attribute
            if (!propInfo.GetCustomAttributes(typeof(FromFormAttribute), false).Any())
                return null;

            // All criteria met; use the FormDataJsonBinder
            return new FormDataJsonBinder(_inputFormatters);
        }
    }
}
