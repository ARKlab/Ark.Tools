using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ODataEntityFrameworkSample.Models
{
	// Book
	public class Book : IAuditableEntityFramework
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
		public virtual Bibliografy Bibliografy { get; set; }

		[Contained]
        public virtual List<Address> Addresses { get; set; }

        public virtual Press Press { get; set; }

        [ConcurrencyCheck]
        public string _ETag { get; set; }

		public void With(Book book)
		{
			this.ISBN = book.ISBN;
			this.Title = book.Title;
			this.Author = book.Author;
			this.Price = book.Price;
			this.Location.City = book.Location.City;
			this.Location.Street = book.Location.Street;

			this.Addresses.Clear();
			foreach (var a in book.Addresses)
				this.Addresses.Add(a);
		}
	}

	// Book
	public class BookDto
	{
		public int Id { get; set; }

		public string ISBN { get; set; }
		public string Title { get; set; }
		public string Author { get; set; }
		public decimal Price { get; set; }
		public Address Location { get; set; }
		
		public virtual Bibliografy Bibliografy { get; set; }

		public virtual List<Address> Addresses { get; set; }

		public virtual Press Press { get; set; }

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
		public int Id { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
    }

	//*************************************************//
	[Owned]
	public class Bibliografy
	{
		public int Id { get; set; }
		public string Name { get; set; }

		[Contained]
		public virtual List<Code> Codes { get; set; }
	}
	
	[Owned]
	public class Code
	{
		public int Id { get; set; }
		public int Value { get; set; }
	}
}