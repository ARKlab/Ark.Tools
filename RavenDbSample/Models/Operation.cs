using Ark.Tools.Core;
using Microsoft.AspNet.OData.Builder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RavenDbSample.Models
{
	public enum BaseKind
	{
		A,B
	}

	[JsonConverter(typeof(BaseConverter))]
	public abstract class Base
	{
		[Key]
		public string Id { get; set; }
		[Required]
		public BaseKind Kind { get; set; }
	}

	public class A : Base
	{
		public A() : base()
		{
			Kind = BaseKind.A;
		}

		public int ValueFromA { get; set; }
	}

	public class B : Base
	{
		public B() : base()
		{
			Kind = BaseKind.B;
		}

		public int ValueFromB { get; set; }
	}


	public class BaseOperation : IAuditableEntity
	{
		[Key]
		public string Id { get; set; }
		
		[Contained]
		public List<Base> Operations { get; set; }

		[Contained]
		[Required]
		public Base B { get; set; }

		public Guid AuditId { get; set; }
	}
	
	class BaseConverter : JsonCreationConverter<Base>
	{
		protected override Base Create(Type objectType, JObject jObject)
		{
			if (jObject.TryGetValue(nameof(Base.Kind), StringComparison.InvariantCultureIgnoreCase, out var token))
			{
				var kind = token.ToObject<BaseKind>();
				switch (kind)
				{
					case BaseKind.A:
						return new A();
					case BaseKind.B:
						return new B();
				}
			}

			throw new InvalidOperationException("Can't deserialize SourceEntry. SourceEntry.Kind field not found or not valid.");
		}
	}

	public abstract class JsonCreationConverter<T> : JsonConverter
	{
		/// <summary>
		/// Create an instance of objectType, based properties in the JSON object
		/// </summary>
		/// <param name="objectType">type of object expected</param>
		/// <param name="jObject">
		/// contents of JSON object that will be deserialized
		/// </param>
		/// <returns></returns>
		protected abstract T Create(Type objectType, JObject jObject);

		public override bool CanConvert(Type objectType)
		{
			return typeof(T).IsAssignableFrom(objectType);
		}

		public override bool CanWrite => false;

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override object ReadJson(JsonReader reader,
										Type objectType,
										 object existingValue,
										 JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			// Load JObject from stream
			JObject jObject = JObject.Load(reader);

			// Create target object based on JObject
			T target = Create(objectType, jObject);

			// Populate the object properties
			serializer.Populate(jObject.CreateReader(), target);

			return target;
		}
	}





}
