# Cache enum string metadata and audit generic reflection

## `EnumExtensions.AsString`

`EnumExtensions.AsString<T>` previously called `GetField` and
`GetCustomAttributes` for every value conversion. It now uses
`EnumStringCache<T>`, a static generic holder initialized once for each closed
enum type.

The holder builds a `FrozenDictionary<string, string>` containing every
declared enum name and its selected output. Both dictionary keys and values are
stored with `string.Intern`. The lookup preserves the existing precedence:
`EnumMemberAttribute.Value`, then `DescriptionAttribute.Description`, then the
enum member name. Composite flags and undefined numeric values still fall back
to `Enum.ToString()`.

## Generic reflection audit

The repository was searched across `src`, `tests`, `samples`, and
`benchmarks` for generic methods or classes that use reflection with a generic
type parameter. Existing static generic caches were reviewed and are listed as
already optimized. The following are candidates for future work; none were
changed as part of this task.

| Location | Reflection performed | Assessment |
| --- | --- | --- |
| `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkTypeConverterValue.cs` (`TryParse<T>`) | Calls `TypeDescriptor.GetConverter(typeof(T))` for every parse. | **High candidate.** Cache the converter and its string-conversion capability per closed `T`. Confirm converter lifetime and provider behavior before changing it. |
| `src/aspnetcore/Ark.Tools.AspNetCore.NestedStartup/FakeServer.cs` (`StartAsync<TContext>`) | Calls `typeof(TContext).GetProperty("HttpContext")` for each server start. | **Low candidate.** A per-`TContext` property/accessor cache would remove startup reflection, but startup is normally once per server and trimming annotations must remain correct. |
| `src/common/Ark.Tools.Reqnroll/TableExtensions.cs` (`CreateComplexType<T>`) | Looks up properties, the recursive generic method, and invokes a constructed method while creating each complex row. | **Medium candidate.** Cache target-type property metadata and the recursive method factory per closed type. This is a test helper and is already explicitly reflection/trimming-oriented. |
| `src/common/Ark.Tools.Reqnroll/TableExtensions.cs` (`VerifyAllPropertiesExistOnTargetType<T>`) | Calls `typeof(T).GetProperties()` for every verification. | **Low candidate.** Cache the property-name set per closed `T`; benefit is limited to test setup. |
| `src/common/Ark.Tools.Core/Reflection/ShredObjectToDataTable.cs` (`ShredObjectToDataTable<T>`) | The constructor calls `GetFields` and `GetProperties` for every instance; the polymorphic path repeats the calls for each runtime type. | **Medium candidate.** Move base-type metadata into a static generic cache and use a runtime-type cache for polymorphic types. Preserve table extension and derived-type behavior. |
| `src/common/Ark.Tools.Authorization/Requirement/PermissionAuthorizationRequirement.cs` (`PermissionAuthorizationHandler<TPermissionEnum>.HandleAsync`) | Constructs a resource-specific closed generic requirement type with `MakeGenericType` for every authorization check. | **Medium candidate.** Cache the closed requirement type by runtime resource type, likely with a concurrent dictionary. A cache keyed only by `TPermissionEnum` is insufficient because the resource type varies. |

## Reviewed and already cached

These generic reflection paths already perform their work in static generic
initialization or an equivalent cache and were not changed:

- `src/common/Ark.Tools.Core/DataKey/DataKeyComparer.cs`
- `src/common/Ark.Tools.Core/DataKey/DataKeyPrinter.cs`
- `src/common/Ark.Tools.Core/DataTableExtensions.cs`
- `src/common/Ark.Tools.Core/EvolvableEnum.cs`
- `src/common/Ark.Tools.Core/Reflection/EnumerableExtensions.cs`
- `src/common/Ark.Tools.Core/ArkTypeConverter.cs`
- `src/common/Ark.Tools.EventSourcing/Aggregates/AggregateHelper.cs`
- `src/common/Ark.Tools.MessagePack/EvolvableEnumFormatterResolver.cs`
- `src/mediator-framework/Ark.Tools.MediatorFramework.AzureFunctions/ArkAzureFunctionsInvocation.cs`
- `src/common/Ark.Tools.Solid.Authorization/PolicyAuthorize*Decorator.cs`
- `benchmarks/Ark.Tools.Core.Benchmarks/HistoricalBaselineConverter.cs`

`UniversalInvariantTypeConverterJsonConverter` and
`ValueCollectionJsonConverterFactory` were also reviewed as adjacent factory
reflection, but they receive a runtime `Type` and do not perform reflection on
`typeof(T)` in a generic method or class. They remain outside this audit's
requested scope.

## Verification

- `EnumExtensionsTests` covers attribute precedence, fallback behavior,
  undefined values, and interned cached output.
- Run the repository build and test commands before merging.
