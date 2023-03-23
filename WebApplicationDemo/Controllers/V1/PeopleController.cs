using Asp.Versioning;
using Asp.Versioning.OData;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using WebApplicationDemo.Controllers.V2;
using WebApplicationDemo.Dto;

using static Microsoft.AspNetCore.Http.StatusCodes;
using static Microsoft.AspNetCore.OData.Query.AllowedQueryOptions;

namespace WebApplicationDemo.Controllers.V1
{
    [ApiVersion(1.0)]
    public class PeopleController : ODataController
    {
        private static List<Person> _people = new List<Person>()
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
        public IEnumerable<Person> Get(ODataQueryOptions<Person> query)
        {
            return ((IQueryable<Person>)query.ApplyTo(_people.AsQueryable())).ToArray();
        }

        [HttpGet]
        [EnableQuery]
        public IActionResult Get([FromRoute] int key)
        {
            return Ok(_people.FirstOrDefault(p => p.Id == key));
        }
    }
}