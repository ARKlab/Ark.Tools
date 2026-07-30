# Testing

Test the application through public HTTP and gRPC interfaces rather than
generated implementation details. The sample uses Reqnroll, an in-process
`TestServer`, an in-memory bus, and a gRPC client generated from exported
protos; framework capability tests live in the framework test project.

```bash
docker compose up -d
dotnet test samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests
```

Source: [`samples/Ark.MediatorFramework.Sample/README.md`](../../../samples/Ark.MediatorFramework.Sample/README.md).

Use the test authentication context to issue bearer tokens and test both
authorized and denied calls. Keep transport-agnostic business assertions at
the boundary; use unit tests for pure logic only. For unusual infrastructure,
write a handwritten test host or adapter. Rationale: [`design.md`](../design.md).
