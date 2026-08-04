# Azure Functions boundary tests

This project starts the built mediator sample with Azure Functions Core Tools,
waits for the generated anonymous `/healthCheck` function, and stops the complete
process tree after each test run. It is intentionally separate from the solution's
in-process tests because it requires the pinned `func` executable.

```bash
dotnet build Ark.Tools.slnx --configuration Debug
dotnet test tests/Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests \
  --configuration Debug --minimum-expected-tests 1
```

Set `ARK_AZF_FUNCTION_APP_DIR` when the sample output is not at its default
`bin/Debug/net10.0` location. Host logs are written to the system temporary
directory and redact authorization and connection-string values.
