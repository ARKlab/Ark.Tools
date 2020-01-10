using Ark.Tools.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto
{
	public enum BaseKind
	{
		A, B
	}

	[JsonConverter(typeof(PolymorphicConverter))]
	public abstract class Polymorphic
	{
		public string Id { get; set; }
		public BaseKind Kind { get; set; }
	}

	public class A : Polymorphic
	{
		public A() : base()
		{
			Kind = BaseKind.A;
		}

		public int ValueFromA { get; set; }
	}

	public class B : Polymorphic
	{
		public B() : base()
		{
			Kind = BaseKind.B;
		}

		public int ValueFromB { get; set; }
	}


	class PolymorphicConverter : JsonCreationConverter<Polymorphic>
	{
		protected override Polymorphic Create(Type objectType, JObject jObject)
		{
			if (jObject.TryGetValue(nameof(Polymorphic.Kind), StringComparison.InvariantCultureIgnoreCase, out var token))
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
