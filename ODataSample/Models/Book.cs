using Ark.Tools.EntityFrameworkCore.SystemVersioning.Auditing;
using Microsoft.AspNet.OData.Builder;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

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

		public void WithRefl(object obj)
		{
			var fields = this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			foreach (var field in fields)
			{
				field.SetValue(this, field.GetValue(obj));

				if (field.FieldType.IsClass)
				{
					WithRefl(obj);
				}

				if (field.FieldType.IsArray)
				{

				}
			}
		}


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
}