﻿using Ark.Tools.Solid;

using System.Collections.Generic;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Queries
{
    public static class Get_PostsQuery
    {
        public class V1 : IQuery<List<Post>>
        {
        }
    }
}
