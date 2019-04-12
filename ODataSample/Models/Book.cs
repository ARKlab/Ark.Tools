using Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit;
using Microsoft.AspNet.OData.Builder;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ODataSample.Models
{
	// Book
	public class Book : IAuditable
    {
        [Key]
        public int Id { get; set; }

        public string ISBN { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public decimal Price { get; set; }
        public Address Location { get; set; }

        public Guid AuditId { get; set; }
        public virtual Audit Audit { get; set; }

        [Contained]
        public List<Address> Addresses { get; set; }

        public virtual Press Press { get; set; }

        [ConcurrencyCheck]
        public string _ETag { get; set; }
    }

    // Press
    public class Press
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public Category Category { get; set; }
    }

    // Category
    public enum Category
    {
        Book,
        Magazine,
        EBook
    }

    // Address
    [Owned]
    public class Address
    {
        public string City { get; set; }
        public string Street { get; set; }
    }
}