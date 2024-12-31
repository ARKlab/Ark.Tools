using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

using System.Collections.Generic;
using System.Linq;

using WebApplicationDemo.Dto;

using static Microsoft.AspNetCore.OData.Query.AllowedQueryOptions;

namespace WebApplicationDemo.Controllers.V1
{
    [ApiVersion(1.0)]
    public class PeopleController : ODataController
    {
        private static readonly List<Person.V1> _people = new()
        {
            new()
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@somewhere.com",
            },
            new()
            {
                Id = 2,
                FirstName = "Bob",
                LastName = "Smith",
                Email = "bob.smith@somewhere.com",
            },
            new()
            {
                Id = 3,
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@somewhere.com",
            }
        };

        [HttpGet]
        [EnableQuery(AllowedQueryOptions = All)]
        public IQueryable<Person.V1> Get()
        {
            return _people.AsQueryable();
        }

        [HttpGet]
        [EnableQuery]
        public SingleResult<Person.V1> Get(int key)
        {
            return SingleResult.Create(_people.Where(p => p.Id == key).AsQueryable());
        }
    }
}