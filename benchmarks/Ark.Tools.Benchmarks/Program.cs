// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Benchmarks;

using BenchmarkDotNet.Running;

BenchmarkSwitcher
    .FromAssembly(typeof(ProcessorDispatchBenchmarks).Assembly)
    .Run(args);
