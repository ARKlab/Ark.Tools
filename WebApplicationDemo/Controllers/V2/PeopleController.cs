using Asp.Versioning;
using Asp.Versioning.OData;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.OData;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using WebApplicationDemo.Dto;

using static Microsoft.AspNetCore.Http.StatusCodes;
using static Microsoft.AspNetCore.OData.Query.AllowedQueryOptions;

namespace WebApplicationDemo.Controllers.V2
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns the first element from the specified sequence.
        /// </summary>
        /// <param name="enumerable">The <see cref="IEnumerable">sequence</see> to take an element from.</param>
        /// <returns>The first element in the sequence or <c>null</c>.</returns>
        public static object? FirstOrDefault(this IEnumerable enumerable)
        {
            var iterator = enumerable.GetEnumerator();

            try
            {
                if (iterator.MoveNext())
                {
                    return iterator.Current;
                }
            }
            finally
            {
                if (iterator is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return default;
        }

        /// <summary>
        /// Returns a single element from the specified sequence.
        /// </summary>
        /// <param name="enumerable">The <see cref="IEnumerable">sequence</see> to take an element from.</param>
        /// <returns>The single element in the sequence or <c>null</c>.</returns>
        public static object? SingleOrDefault(this IEnumerable enumerable)
        {
            var iterator = enumerable.GetEnumerator();
            var result = default(object);

            try
            {
                if (iterator.MoveNext())
                {
                    result = iterator.Current;

                    if (iterator.MoveNext())
                    {
                        throw new InvalidOperationException("The sequence contains more than one element.");
                    }
                }
            }
            finally
            {
                if (iterator is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Represents a RESTful people service.
    /// </summary>
    [ApiVersion(2.0)]
    public class PeopleController : ODataController
    {
        /// <summary>
        /// Gets all people.
        /// </summary>
        /// <param name="options">The current OData query options.</param>
        /// <returns>All available people.</returns>
        /// <response code="200">The successfully retrieved people.</response>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ODataValue<IEnumerable<Person>>), Status200OK)]
        public IActionResult Get(ODataQueryOptions<Person> options)
        {
            var validationSettings = new ODataValidationSettings()
            {
                AllowedQueryOptions = Select | OrderBy | Top | Skip | Count,
                AllowedOrderByProperties = { "firstName", "lastName" },
                AllowedArithmeticOperators = AllowedArithmeticOperators.None,
                AllowedFunctions = AllowedFunctions.None,
                AllowedLogicalOperators = AllowedLogicalOperators.None,
                MaxOrderByNodeCount = 2,
                MaxTop = 100,
            };

            try
            {
                options.Validate(validationSettings);
            }
            catch (ODataException)
            {
                return BadRequest();
            }

            var people = new Person[]
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
            },
            };

            return Ok(options.ApplyTo(people.AsQueryable()));
        }

        /// <summary>
        /// Gets a single person.
        /// </summary>
        /// <param name="key">The requested person identifier.</param>
        /// <param name="options">The current OData query options.</param>
        /// <returns>The requested person.</returns>
        /// <response code="200">The person was successfully retrieved.</response>
        /// <response code="404">The person does not exist.</response>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Person), Status200OK)]
        [ProducesResponseType(Status404NotFound)]
        public IActionResult Get(int key, ODataQueryOptions<Person> options)
        {
            var people = new Person[]
            {
            new()
            {
                Id = key,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@somewhere.com",
            }
            };

            var person = options.ApplyTo(people.AsQueryable()).SingleOrDefault();

            if (person == null)
            {
                return NotFound();
            }

            return Ok(person);
        }
    }
}
