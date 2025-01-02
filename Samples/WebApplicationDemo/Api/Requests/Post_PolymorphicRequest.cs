﻿using Ark.Tools.Solid;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests
{
    public class Post_PolymorphicRequest
    {
        public class V1 : IRequest<Polymorphic?>
        {
            public Polymorphic? Entity { get; set; }
        }
    }
}
