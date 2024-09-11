﻿using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto
{
    public static class Person {
        
        /// <summary>
        /// Represents a person.
        /// </summary>
        public class V1
        {
            /// <summary>
            /// Gets or sets the unique identifier for a person.
            /// </summary>
            /// <value>The person's unique identifier.</value>
            public int Id { get; set; }

            /// <summary>
            /// Gets or sets the first name of a person.
            /// </summary>
            /// <value>The person's first name.</value>
            [Required]
            [StringLength(25)]
            public string? FirstName { get; set; }

            /// <summary>
            /// Gets or sets the last name of a person.
            /// </summary>
            /// <value>The person's last name.</value>
            [Required]
            [StringLength(25)]
            public string? LastName { get; set; }

            /// <summary>
            /// Gets or sets the email address for a person.
            /// </summary>
            /// <value>The person's email address.</value>
            public string? Email { get; set; }

        }

        
        /// <summary>
        /// Represents a person.
        /// </summary>
        public class V2 : V1
        {

            /// <summary>
            /// Gets or sets the telephone number for a person.
            /// </summary>
            /// <value>The person's telephone number.</value>
            public string? Phone { get; set; }
        }

    }

}
