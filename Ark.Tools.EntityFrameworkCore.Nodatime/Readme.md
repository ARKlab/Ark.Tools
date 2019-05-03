![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools.EntityFrameworkCore.Nodatime
This add support for NodaTime types in EF Core Design and SqlServer provider, including filter predicate pushdown to SQL.

## Getting Started
The library is provided from NuGet.

```Install-Package Ark.Tools.EntityFrameworkCore.Nodatime```

## Usage

### Design Time

To enable the support at design time, for example to be used to Add-Migration or Update-Database, create a class implementing `IDesignTimeServices` and set NodaTime support for SqlServer.
Consider that this do replace the original SqlServer services and therefore is not composable with other extensions.
 
```csharp
    public class NodatimeDesignTime : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
        {
            serviceCollection.SetNodaTimeSqlServerMappingSource();
        }
    }
```

### Runtime

To add support to Nodatime types over SqlServer at runtime, configure the `DbContext` replacing some of the original SqlServer services with the one provided by this library.
 
```csharp
public class ExampleContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddNodaTimeSqlServer();
    }
}
```

## Supported features

This library add support for the following Nodatime types with their mapping to SqlServer types

| NodaTime | SqlServer |
|----------|-----------|
| LocalDate| date      |
| LocalDateTime| datetime2 |
| Instant | datetime2 |
| OffsetDateTime | datetimeoffset |

Support for LocalTime is up-for-grab ;)

### Server-side query

The usual binary operators supported by Nodatime directly are mapped to SqlServer and executed server-side. 


The only exception is for OffsetDateTime where Nodatime doesn't support greater or less comparisons. 
The following use of the Comparer is not mapped to SqlServer and executed in memory client-side.
```csharp
ctx.Entity.Where(x => OffsetDateTime.Comparer.Instant.Compare(x.OffsetDateTime, x.OffsetDateTime) > 0)
```
As alternative you "convert" to use the System types and would be executed server-side.
```csharp
ctx.Entity.Where(x => x.OffsetDateTime.ToDateTimeOffset() > x.OffsetDateTime.ToDateTimeOffset())
```

#### Truncations

Truncations are supported when mapped to supported types, so for example
```csharp
ctx.Entity.Where(x => x.OffsetDateTime.Date > x.OffsetDateTime.Date)
```
get's compiled to
```sql
CONVERT(date, [x].[OffsetDateTime]) > CONVERT(date, [x].[OffsetDateTime])
```

#### DatePart support
The library supports the translation to T-SQL DATEPART for the following accesses, inclusing in nested comparison like

| Nodatime            | 
|---------------------|
| LocalDate.Year      |
| LocalDate.Month     |
| LocalDate.DayOfYear |
| LocalDate.Day       |
| LocalDateTime.Year      |
| LocalDateTime.Month     |
| LocalDateTime.DayOfYear |
| LocalDateTime.Day       |
| LocalDateTime.Hour    |
| LocalDateTime.Minute   |
| LocalDateTime.Second  |
| LocalDateTime.Millisecond  |
| LocalDateTime.NanosecondOfSecond |
| OffsetDateTime.Year |
| OffsetDateTime.Month |
| OffsetDateTime.DayOfYear |
| OffsetDateTime.Day |
| OffsetDateTime.Hour |
| OffsetDateTime.Minute |
| OffsetDateTime.Second |
| OffsetDateTime.Millisecond |
| OffsetDateTime.NanosecondOfSecond |

#### Plus operations

This library support execution server side of simple PlusX operations.
Plus(Duration) or Plus(Period) are not supported (read: I accept PRs).

For example the following statement

```csharp
ctx.Entity.Where(x => x.LocalDate.PlusDays(1) > x.LocalDateTime.Date)
```

is executed server side as
```sql
WHERE DATEADD(day, 1, [x].[LocalDate]) > CONVERT(date, [x].[LocalDateTime])
```


