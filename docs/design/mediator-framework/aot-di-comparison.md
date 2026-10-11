# Native AOT composition: open-generic decorators

Comparison, October 2026, of how to compose Ark.Tools.Solid handlers and their open-generic decorators (validation,
authorization, logging, profiling) in a Native AOT application, where SimpleInjector cannot run. It follows the
[Native AOT investigation](native-aot-investigation.md).

## Requirement

- **Composition-agnostic code.** Neither a handler nor a decorator knows the composition: no container types,
  attributes, tags or keys in their constructors. Today's SimpleInjector decorators meet this.
- **Open generics.** A decorator such as `ValidationDecorator<TQuery, TResult>` is written once and applies to every
  handler, including value-type results (`IQueryHandler<CountQuery, int>`).
- **Ordered chain** of several decorators over the same contract.
- **Scoped lifetimes aligned with Microsoft DI**, because ASP.NET Core and the generic host own the request and
  message scopes.
- **Native AOT clean:** no publish warnings, and it runs.

## Verified results

Each candidate composed the same code: two handlers (`CountHandler : IQueryHandler<CountQuery, int>`,
`EchoHandler : IQueryHandler<EchoQuery, string>`), two plain open-generic decorators (`LoggingDecorator<,>`, and
`ValidationDecorator<,>`, which also takes `IEnumerable<IValidator<TQuery>>`) and a scoped `Trace` that every member of a
chain records into. Each spike was published with `PublishAot` on `linux-x64` (.NET SDK 10.0.401) and run. It
checked the chain order `Validation > Logging > Handler`, value and reference results, an empty and a non-empty
validator collection, one `Trace` per scope, and a new one per scope.

| Approach | Decorators composition-agnostic | Native AOT publish | Runs under AOT | Scopes |
| --- | --- | --- | --- | --- |
| Microsoft DI + Scrutor `Decorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator<,>))` | Yes | IL2104/IL3053 from Scrutor | **No**: `A suitable constructor for type LoggingDecorator<CountQuery, Int32> could not be located` | Native |
| Microsoft DI + one generic registration helper (below), one line per handler | Yes | Clean | **Yes**, all checks | Native |
| Microsoft DI + a registration source generator that emits those lines | Yes | Clean | **Yes**, all checks | Native |
| Pure.DI 2.5.4, `TT` markers, inner chosen with `Tag.On("…Decorator…:inner")` in the setup | Yes | Does not compile: `DIE000 Unable to resolve IQueryHandler<CountQuery, int>`, or `DIE039` cyclic dependency, for the second decorator | — | — |
| Pure.DI 2.5.4, `TT` markers, inner chosen with `[Tag("…")]` on the decorator's constructor | **No**: decorators are Pure.DI-aware | Clean | **Yes**, all checks | Pure.DI scopes |
| The previous, bridged into Microsoft DI (one composition per Microsoft DI scope, one line per root) | No (as above) | Clean | **Yes**, all checks | Native, through the bridge |

Native executable sizes: 2.3 MB (Microsoft DI) and 1.5 MB (Pure.DI); both are small next to an ASP.NET Core app.

### Generators do not see each other's regular output

The Microsoft DI generator spike also measured which handlers a composition generator can see:

| Handler | Registered by the generator |
| --- | --- |
| Hand-written in the same project | Yes |
| Compiled into a referenced assembly | Not visible as source; registered by that assembly's own generator or by hand |
| Emitted by another generator with `RegisterPostInitializationOutput` | Yes: post-initialization sources are added before any generator runs |
| Emitted by another generator with `RegisterSourceOutput` (how Ark emits endpoints and functions) | **No** |

This is the same limit that hid the Azure Functions triggers from the Worker SDK generators. A composition generator is
safe as long as the handlers and decorators it composes are hand-written or come from referenced assemblies, which is the
Ark model: contracts and handlers are user code, decorators are library code. It breaks if a future generator emits
handlers or decorators.

## Findings

1. **Microsoft DI alone has no open-generic decoration that is AOT-safe.** Open-generic registrations close types with
   `MakeGenericType`, which fails under AOT for value-type arguments, and decoration libraries (Scrutor, Injectio) do the
   same at runtime.
2. **A generic C# method is the AOT-friendly form of an open-generic decorator.** ILC compiles every instantiation it
   sees, so a helper written once covers every handler while staying composition-agnostic:

   ```csharp
   public static IServiceCollection AddQuery<TQuery, TResult,
       [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
       where THandler : class, IQueryHandler<TQuery, TResult>
   {
       services.AddScoped<THandler>();
       services.AddScoped<IQueryHandler<TQuery, TResult>>(static sp =>
       {
           IQueryHandler<TQuery, TResult> handler = sp.GetRequiredService<THandler>();
           handler = ActivatorUtilities.CreateInstance<LoggingDecorator<TQuery, TResult>>(sp, handler);
           return ActivatorUtilities.CreateInstance<ValidationDecorator<TQuery, TResult>>(sp, handler);
       });
       return services;
   }

   services.AddQuery<CountQuery, int, CountHandler>();     // one line per handler
   ```

   `ActivatorUtilities.CreateInstance<T>` resolves each decorator's other dependencies from the provider, so decorators
   keep plain constructors. Order is the nesting order; a conditional decorator is an `if` in the helper.
3. **Pure.DI does not meet the requirement today.** Its open-generic decorators work and are AOT-clean, but only with
   `[Tag]` attributes on the decorators' constructors. `Tag.On`, which would move the tags into the setup, resolved one
   level of a chain over the same contract but not the second, with every path form tried
   (`*ValidationDecorator*:inner`, `Spike.ValidationDecorator`2.ValidationDecorator:inner`, `*ValidationDecorator`2:inner`).
   Pure.DI also needs one binding per handler and one `Root` plus one bridge line per handler, and its own Microsoft DI
   bridge (`Pure.DI.MS`) uses `Expression.Compile`; the hand-written bridge above is AOT-clean. Pure.DI is itself a
   source generator, so a generator cannot write its setup.
4. **The per-handler line is the only boilerplate**, in every option. Today SimpleInjector removes it with assembly
   scanning, which is reflection.

## Recommendation

Use **Microsoft DI with a generic registration helper**:

- Ark.Tools ships the helpers (`AddArkQuery<TQuery, TResult, THandler>`, and the request and command forms) with the
  standard decorator chain, plus Microsoft DI processors that dispatch through the typed `ExecuteAsync<TQuery, TResult>`
  overload. No new dependency.
- The per-handler line is hand-written at first. A registration generator can emit it later: the spike shows it works
  for hand-written handlers, the Ark case. A generator that emitted handlers would need its registrations emitted by the
  same generator.
- Revisit Pure.DI if `Tag.On` supports decorator chains over one contract; then it would meet the requirement with
  composition-time tags only.
