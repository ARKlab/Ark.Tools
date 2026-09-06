# How it works

This section explains the **mechanics** behind framework features: algorithms,
formulas, invariants, and the reason a default is what it is.

You do not need it to use the framework. Read it when you are debugging an
observed behavior, replacing a built-in seam with your own implementation, or
changing the framework itself. The [guide](../guide/README.md) is the
task-oriented documentation: what to write, what to configure, and what to tune.

| Document | Explains |
| --- | --- |
| [`messaging-processor-host.md`](messaging-processor-host.md) | Receive seam, credit-bounded prefetch, backoff math, shared lock renewal, and the adaptive concurrency algorithm. |
