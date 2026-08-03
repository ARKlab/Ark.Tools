// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;
using Rebus.Transport.InMem;

using Ark.MediatorFramework.Sample.RebusProcessor;

var network = new InMemNetwork();
await using var container = RebusProcessorComposition.BuildContainer(network, useSqlStore: false);
container.Verify();
container.StartBus();
await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
