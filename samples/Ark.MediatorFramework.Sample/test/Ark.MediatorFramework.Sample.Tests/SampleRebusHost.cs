// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Rebus;

namespace Ark.MediatorFramework.Sample.RebusProcessor;

[ArkRebusHost(typeof(SampleMessagingParticipant))]
internal static partial class SampleRebusHost;

[ArkRebusHost(typeof(SampleMessagingPublisherParticipant))]
internal static partial class SampleRebusPublisherHost;
