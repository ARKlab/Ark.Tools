# Sharing the contract model between MediatorFramework generators

## Status

Accepted ([#1184](https://github.com/ARKlab/Ark.Tools/issues/1184)). Step 1 implemented.

## Context

Each MediatorFramework generator (`src/mediator-framework/*Generators`) reads the
same contract attributes with its own parsing code. The copies have drifted, for
example the two `XmlDocumentation.cs` copies
([#1179](https://github.com/ARKlab/Ark.Tools/issues/1179)). Merging the generators
into one assembly was rejected: it changes packaging, generated-file hint paths
and analyzer sets for every consumer, and each runtime package shipping only its
own generator is a cheap gate.

## Decision

Share the contract model as **linked source** (option 2 of #1184), not as a shared
assembly.

- Shared files live in `src/mediator-framework/Ark.Tools.MediatorFramework.Generators/`,
  next to the files already shared this way (`GeneratorSpecPrimitives.cs`,
  `ValueObjectSymbols.cs`, `HttpStringBinding.cs`,
  `MessagingContractTopologyValidator.cs`).
- Each generator project links the files it needs with
  `<Compile Include="../Ark.Tools.MediatorFramework.Generators/<File>.cs" Link="<File>.cs" />`.
- Shared types stay `internal`, because each generator assembly compiles its own copy.

A shared netstandard2.0 assembly was rejected: it must be packed next to each
generator under `analyzers/dotnet/cs`, and a consumer that references several
runtime packages could load different versions of it from several paths.

## Consequences

- No packaging, analyzer-loading or consumer-visible change.
- The code is duplicated in each generator binary, and a change recompiles every
  generator that links it.
- Generators still walk contracts in their own pipelines; nothing is shared at run time.

## Steps

1. `ContractSymbols.cs`: contract attribute metadata names and named-argument
   readers. Adopted generator by generator; generated output stays byte-identical.
2. Shared property walk. Characterization test first, then the most correct
   behaviour; every generated-output difference is listed in the pull request.
3. Shared contract specification, once the in-flight changes to the API surface
   and gRPC generators have merged.
