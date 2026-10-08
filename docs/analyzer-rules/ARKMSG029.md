# ARKMSG029: Default messaging contract name is version-only

- **Severity:** Warning
- **Component:** Mediator Framework
- **Diagnostic message:** `Contract '{0}' has no explicit name and its default logical name '{1}' derives from a bare version type name; set an explicit name, for example [{2}(Name = "...")]`

## What it checks

A contract marked with `[Message]` or `[Event]` that sets no `Name` gets a default logical name built from its API group and its type name, for example `ark.create-book-review-request`. When the contract is a nested versioned type such as `CreateBookReviewRequest.V1`, the type name is only `V1`, so the default is `ark.v1`. That name says nothing about the contract on the wire, and every other unnamed `V1` contract resolves to the same name and is reported by [ARKMSG020](ARKMSG020.md).

The rule reports a `[Message]` or `[Event]` contract with no explicit `Name` whose type name is `V` followed by digits only.

The default naming rule is unchanged, so adding an explicit name changes the wire name of a contract that is already deployed. That is a topology migration, not a rename: `FormerNames` is a receive-only alias, so it lets an updated consumer read messages that still carry `ark.v1`, but it does not let deployed consumers read the new name and it does not keep or remove the old queue or topic. Migrate explicitly: deploy consumers that know both names, drain the old destination, then switch producers. If that migration is not worth it, keep the deployed name and suppress the rule (see below).

## How to fix it

Set an explicit, normalized logical name on the attribute.

### Incorrect

```csharp
public static class CreateBookReviewRequest
{
    [Message]
    public sealed record V1(Guid BookId, int Rating) : ICommand<V1>;
}
```

### Correct

```csharp
public static class CreateBookReviewRequest
{
    [Message(Name = "books.create-book-review")]
    public sealed record V1(Guid BookId, int Rating) : ICommand<V1>;
}
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKMSG029.severity` EditorConfig setting only for an intentional exception, such as a contract whose `ark.v1` name is already deployed and cannot change. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMSG029.md`.
