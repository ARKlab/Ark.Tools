# SQL storage policies

`Ark.Tools.Compliance.Sql` turns classification into SQL Server DDL: sensitivity
classification (`ADD SENSITIVITY CLASSIFICATION`) and dynamic data masking
(`ADD MASKED WITH`). Generation is **opt-in per type** — C# names are not the SQL
schema, so nothing is emitted unless you say which table and columns you mean.

```xml
<ItemGroup>
  <PackageReference Include="Ark.Tools.Compliance.Sql" />
</ItemGroup>
```

Requires `EnableArkToolsCompliance=true` on the project that declares the entities.

## Declaring the mapping

Layer the SQL attributes *on top of* the classification:

```csharp
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.Sql;

[SqlDataPolicy(Schema = "sales", Table = "Customers", Label = "Confidential - GDPR")]
public sealed record CustomerEntity
{
    [PersonalData]
    [SqlColumnPolicy("email_address", StoragePolicy.Masked,
                     MaskFunction = SqlMask.Email, InformationType = "Contact Info")]
    public EmailAddress Email { get; init; }

    [PersonalData]
    [SqlColumnPolicy("phone_number", StoragePolicy.Masked)]
    public PhoneNumber? Phone { get; init; }
}
```

- `[SqlDataPolicy]` turns generation **on** for the type. `Schema` defaults to the
  SQLCMD token `$(ComplianceSchema)` and `Label` to `$(ComplianceLabel)` so they can
  be substituted per environment; `Table` has no default — it must be explicit.
- `[SqlColumnPolicy(columnName, storagePolicy)]` carries the **column name
  verbatim**; the generator never derives it from the property name. `Schema`,
  `Table`, and `Label` may be overridden per member for split-table mappings;
  `InformationType` feeds the classification metadata; `MaskFunction` picks the
  masking function (`SqlMask.Default` = `default()`, `SqlMask.Email` = `email()`,
  or any literal SQL masking function string).
- `StoragePolicy`: `None` (classify only), `Masked` (classification + dynamic data
  masking), `ApplicationEncrypted` (classification; value encrypted by the
  application before storage).
- Inside a `[SqlDataPolicy]` type, every classified member **must** declare a
  `[SqlColumnPolicy]` — a missing one is error `ARKPII007`. Types without
  `[SqlDataPolicy]` generate nothing and are governed by `ARKPII012` instead.

## What gets generated

During build, the package's targets expand the generator output into one
`policy-<sha256>.compliance.sql` file per type under
`$(IntermediateOutputPath)ArkCompliance.Sql` (override with
`ArkComplianceSqlOutputPath`). Content:

```sql
ADD SENSITIVITY CLASSIFICATION TO [sales].[Customers].[email_address]
    WITH (LABEL = 'Confidential - GDPR', INFORMATION_TYPE = 'Contact Info', RANK = HIGH);

ALTER TABLE [sales].[Customers]
    ALTER COLUMN [email_address] ADD MASKED WITH (FUNCTION = 'email()');
```

The scripts are *templates*: `$(Token)` placeholders left in them are ordinary
SQLCMD variables. You can substitute at build time with MSBuild items:

```xml
<ItemGroup>
  <ArkComplianceSqlToken Include="ComplianceSchema" Value="sales" />
  <ArkComplianceSqlToken Include="ComplianceLabel" Value="Confidential - GDPR" />
</ItemGroup>
```

Unsubstituted tokens survive to deployment time and are resolved by
SqlPackage/`sqlcmd` variables instead.

## Wiring a database project (.sqlproj)

A Microsoft.Build.Sql database project consumes the generated policies as a
post-deployment script. Three pieces:

**1. A custom target** that asks each entity project for its policies (via the
package's `ArkGenerateComplianceSqlStandalone` entry point — it compiles the
project first, so it works on clean builds) and concatenates them:

```xml
<Project DefaultTargets="Build">
  <Sdk Name="Microsoft.Build.Sql" Version="2.2.0" />

  <PropertyGroup>
    <ArkComplianceSqlOutputPath>$(MSBuildProjectDirectory)\obj\ArkCompliance.Sql</ArkComplianceSqlOutputPath>
  </PropertyGroup>

  <Target Name="ArkMaterializeComplianceSql" BeforeTargets="_SetupSqlBuildInputs;SqlBuild"
          Condition="'$(DesignTimeBuild)' != 'true'">
    <!-- Start clean so removed policies do not linger. -->
    <ItemGroup>
      <_ArkComplianceSqlPolicyToDelete Include="$(ArkComplianceSqlOutputPath)\**\policy-*.compliance.sql" />
    </ItemGroup>
    <Delete Files="@(_ArkComplianceSqlPolicyToDelete)" />

    <!-- One <MSBuild> invocation per project that declares [SqlDataPolicy] entities. -->
    <MSBuild Projects="..\MyApp.Common\MyApp.Common.csproj"
             Targets="ArkGenerateComplianceSqlStandalone"
             Properties="ArkComplianceSqlOutputPath=$(ArkComplianceSqlOutputPath)\MyApp.Common;EnableArkToolsCompliance=true"
             BuildInParallel="false" />

    <!-- Concatenate everything into a single deployable script. -->
    <ItemGroup>
      <_ArkComplianceSqlPolicy Include="$(ArkComplianceSqlOutputPath)\**\policy-*.compliance.sql" />
    </ItemGroup>
    <MakeDir Directories="$(ArkComplianceSqlOutputPath)"
             Condition="@(_ArkComplianceSqlPolicy->Count()) &gt; 0" />
    <Delete Files="$(ArkComplianceSqlOutputPath)\CompliancePolicies.sql"
            Condition="@(_ArkComplianceSqlPolicy->Count()) == 0" />
    <ReadLinesFromFile File="%(_ArkComplianceSqlPolicy.Identity)"
                       Condition="@(_ArkComplianceSqlPolicy->Count()) &gt; 0">
      <Output TaskParameter="Lines" ItemName="_ArkComplianceSqlLine" />
    </ReadLinesFromFile>
    <WriteLinesToFile File="$(ArkComplianceSqlOutputPath)\CompliancePolicies.sql"
                      Lines="@(_ArkComplianceSqlLine)"
                      Overwrite="true"
                      Condition="@(_ArkComplianceSqlPolicy->Count()) &gt; 0" />
  </Target>

</Project>
```

**2. The post-deployment script** includes the concatenated file:

```sql
-- Script.PostDeployment.sql
:r .\obj\ArkCompliance.Sql\CompliancePolicies.sql
```

```xml
<ItemGroup>
  <PostDeploy Include="Script.PostDeployment.sql">
    <CopyToOutputDirectory>DoNotCopy</CopyToOutputDirectory>
  </PostDeploy>
</ItemGroup>
```

**3. Optional: masked/privileged readers.** Dynamic data masking is enforced per
principal — grant `UNMASK` only to roles that need cleartext:

```sql
IF DATABASE_PRINCIPAL_ID(N'CompliancePrivilegedReader') IS NULL
    CREATE USER [CompliancePrivilegedReader] WITHOUT LOGIN;
GRANT UNMASK TO [CompliancePrivilegedReader];
```

With this in place, `dotnet build` on the database project regenerates the policy
scripts from the current entity annotations and packs them into the DACPAC —
classification and masking deploy with every schema deployment, and a removed
`[SqlColumnPolicy]` removes its script on the next build.
