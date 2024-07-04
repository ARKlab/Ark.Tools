﻿// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http;
using Flurl.Http.Configuration;
using Flurl.Util;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace Ark.Tools.Http
{
    /// <summary>
    /// Default implementation of IFlurlClientBuilder.
    /// </summary>
    public class ArkFlurlClientBuilder : FlurlClientBuilder
    {
        static readonly FieldInfo? _factoryField =
                typeof(FlurlClientBuilder)
                    .GetField("_factory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Creates a new FlurlClientBuilder.
        /// </summary>
        public ArkFlurlClientBuilder(string? baseUrl = null, IFlurlClientFactory? flurlClientFactory = null)
            : base(baseUrl)
        {
            if (flurlClientFactory != null)
            {
                if (_factoryField is null) throw new ArgumentException("private _factory not found. Check Flurl for changes");
                _factoryField.SetValue(this, flurlClientFactory);
            }
        }
    }
}
