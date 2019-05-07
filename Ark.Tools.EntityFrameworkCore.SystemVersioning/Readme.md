![image](http://www.ark-energy.eu/wp-content/uploads/ark-dark.png)
# Ark.Tools.EntityFrameworkCore.SystemVersioning
This add support for System Versioning on EF Core and also give the possibility to enable an Audit trail.

## Getting Started
The library is provided from NuGet.

```Install-Package Ark.Tools.EntityFrameworkCore.SystemVersioning```

## Usage

To Enable Audit and System versioning use:

```csharp
services.AddDbContext<MyDbContext>((provider, options) =>
{
	//...

	//For System Versioning And Audit
	options.AddSqlServerSystemVersioningAudit();
});
```

But is not enough! We need to go into details.

### Temporal Tables

First of all, to keep of all changes, we implemented the Temporal Table.
With the following extension of the ModelBuilder we enabled the creation od system versioned table at _CREATE_ time.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	base.OnModelCreating(modelBuilder);

	//...After definition of db relation

	modelBuilder.AllTemporalTable();
}
```
The name of the hystorical table created from the original table is : __"*OriginalTabelName*"__+__"*Hystory*"__. 

### Audit 

First part done! Now we need a class for the Audit Record and for Entities Record, like this:

```csharp
public class Audit
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
	public DateTime Timestamp { get; set; }

    public List<AffectedEntity> AffectedEntities { get; set; } = new List<AffectedEntity>();
}
```

Audit class have it's owned class called AffectedEntities: in this way we have only one Audit record per transaction 
and multiple records for each entity that changed during that single transaction.

To do that, we need that the Entities that we whant to track, will implement the ```IAuditableEntityFramework``` interface:
only for that classes we will have a complete Audit trail.

Then we need to tell to model builder to add an Audit refence for our class:

```csharp
modelBuilder.AddAuditReference<MyClass>();
```

### AuditDbContext

This package can manage the Audit signed as IAuditableEntityFramework but only if the DbContext used is derived from 
the ```AuditDbContext```.

This class overrides the SaveChanges and SaveChangesAsync to manage automatically the audit when the data are saved.

## Supported features

This library add supportalso for "AsOf" and "Between" queries

### IQueryableExtensions

__SqlServerAsOf__ extension
 
```csharp
var bookList = _dbContext
				.Books
				.SqlServerAsOf(asOf)
				.ToList();
```

__SqlServerBetween__ extension

```csharp
var bookList = _dbContext
				.Books
				.SqlServerBetween(startTime, endTime)
				.ToList();
```
