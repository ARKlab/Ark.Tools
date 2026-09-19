# Getting started

## With Ark.Tools.Sdk (recommended)

Projects using the `Ark.Tools.Sdk` MSBuild SDK get the whole kit with a single property.
It layers on the default .NET SDK, so list both SDKs in the project declaration:
Compliance is **opt-in while the analyzer is in beta**:

```xml
<Project Sdk="Microsoft.NET.Sdk;Ark.Tools.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <EnableArkToolsCompliance>true</EnableArkToolsCompliance>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ark.Tools.Compliance" />
  </ItemGroup>

</Project>
```

A library that only classifies its own data can reference
`Ark.Tools.Compliance.Abstractions` instead: it carries the attributes, the
`CompliancePurpose`/`ISensitiveValue<T>` contracts and the `ARKPII*` analyzers,
and its only dependency is `Microsoft.Extensions.Compliance.Abstractions`.
Reference the full `Ark.Tools.Compliance` package when you need the redactors,
`AddArkRedaction()`, the built-in sensitive value objects or the
`[SensitiveValueObject<T>]` source generator.

`EnableArkToolsCompliance=true` makes the SDK:

- add `Ark.Tools.Compliance.Analyzers` implicitly as a development dependency
  (`PrivateAssets="all"` — nothing is shipped at runtime);
- wire the default lexicon (`ComplianceLexicon.Ark.txt`) and sink list
  (`ComplianceSinks.Ark.txt`) as analyzer `AdditionalFiles`;
- apply the packaged `Ark.Tools.Compliance.globalconfig` severities
  (errors for sinks, warnings for declaration hygiene; `ARKPII001` is
  excluded from `TreatWarningsAsErrors` so a name-heuristic hit never breaks a build);
- enable the compliance surface baseline check (see
  [Analyzers — compliance surface](analyzers.md#the-compliance-surface-baseline)).

You still add `Ark.Tools.Compliance` as a `PackageReference` where you *declare*
classified members — the attributes and value objects are a normal runtime library.

`ArkComplianceMode` is an internal build state used to validate the opt-in invariant.
Do not set it yourself: it is derived from
`EnableArkToolsCompliance` (`Enforce` when `true`, `Off` otherwise) and the build
rejects any other value.

You can put the property in `Directory.Build.props` to opt in a whole solution:

```xml
<PropertyGroup>
  <EnableArkToolsCompliance>true</EnableArkToolsCompliance>
</PropertyGroup>
```

## Stand-alone (plain Microsoft.NET.Sdk)

Without `Ark.Tools.Sdk`, reference the packages explicitly:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Enables the SQL policy targets and the compliance surface generation. -->
    <EnableArkToolsCompliance>true</EnableArkToolsCompliance>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ark.Tools.Compliance" />
    <PackageReference Include="Ark.Tools.Compliance.Analyzers" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

The analyzer package's `buildTransitive` props automatically register the default
lexicon and sinks files; the core package ships the source generator that expands
`[SensitiveValueObject<T>]` structs, so no further wiring is needed.

## First classified type

```csharp
using Ark.Tools.Compliance;

public sealed record Customer
{
    public Guid Id { get; init; }

    [PersonalData]
    public EmailAddress Email { get; init; }        // built-in sensitive value object

    [PersonalData]
    public string? PhoneNumberNote { get; init; }   // plain classified string
}
```

Build. Two things happen:

1. Any use of `Email` or `PhoneNumberNote` at a guarded sink (logging, exceptions,
   telemetry, `Console`, …) is now a compile error. `ToString()` on `Email` renders
   the mask (`***`), never the address.
2. The build reports `ARKPII020` because the project's compliance surface changed
   and there is no baseline yet. Accept it by copying the generated file:

   ```bash
   cp obj/Debug/net8.0/ArkComplianceSurface.current.txt ArkComplianceSurface.txt
   ```

   Commit `ArkComplianceSurface.txt`. From now on, every change to what personal
   data the assembly holds shows up as a reviewable diff of that file.

## Reading a value back

Cleartext access is a deliberate act with a stated purpose:

```csharp
var smtpTo = customer.Email.Reveal(CompliancePurpose.SendTransactionalEmail);

// Or a custom purpose:
var body = customer.Email.Reveal(
    CompliancePurpose.Custom("Compose order confirmation", CompliancePurposeCategory.CustomerSupport));
```

There is no implicit conversion to `string`; interpolating or concatenating the
struct produces the redacted rendering, and the analyzers refuse classified plain
members at sinks. See [Classifying data](classifying-data.md) for choosing between
attributes and value objects.

## Where the rest lives

- Logging redaction is on by default when using `Ark.Tools.NLog` — see
  [Runtime redaction](runtime-redaction.md).
- Persisting classified columns with SQL Server masking/classification — see
  [SQL storage policies](sql-policies.md).
- Dapper/Newtonsoft/MessagePack/protobuf-net/Reqnroll support — see
  [Sensitive value objects — serializer adapters](sensitive-value-objects.md#serializer-adapters).
