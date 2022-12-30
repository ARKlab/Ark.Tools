// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Flurl.Http.Configuration;
using MessagePack;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ark.Tools.Http
{

    public class SystemTextJsonSerializer : ISerializer
    {
        private readonly JsonSerializerOptions? _options;

        public SystemTextJsonSerializer(JsonSerializerOptions? options = null)
        {
            _options = options;
        }

        public T Deserialize<T>(string s)
        {
            return JsonSerializer.Deserialize<T>(s, _options)!;
        }

        public T Deserialize<T>(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return Deserialize<T>(reader.ReadToEnd());
        }

        public string Serialize(object obj)
        {
            return JsonSerializer.Serialize(obj, _options);
        }
    }
}
