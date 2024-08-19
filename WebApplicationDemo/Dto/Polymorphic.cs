using Ark.Tools.NewtonsoftJson;
using Ark.Tools.Nodatime;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using System;
using System.Collections.Generic;

namespace WebApplicationDemo.Dto
{
    public enum BaseKind
	{
		A, B
	}

	[JsonConverter(typeof(PolymorphicConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(PolymorphicConverterSTJ))]
	public abstract class Polymorphic
	{
		public string? Id { get; set; }
		public BaseKind Kind { get; set; }
	}

	public class A : Polymorphic
	{
		public A() : base()
		{
			Kind = BaseKind.A;
		}

		public int ValueFromA { get; set; }
		public LocalDateRange? Range { get; set; }

		public IList<string> StringList { get; set; } = new List<string>();
		public IDictionary<LocalDate, double?> Ts { get; set; } = new Dictionary<LocalDate, double?>();
	}

	public class B : Polymorphic
	{
		public B() : base()
		{
			Kind = BaseKind.B;
		}

		public int ValueFromB { get; set; }
	}

	class PolymorphicConverterSTJ : Ark.Tools.SystemTextJson.JsonPolymorphicConverter<Polymorphic, BaseKind>
	{
		public PolymorphicConverterSTJ() 
			: base(nameof(Polymorphic.Kind))
		{
		}

		protected override Type GetType(BaseKind discriminatorValue)
		{
			switch (discriminatorValue)
			{
				case BaseKind.A:
					return typeof(A);
				case BaseKind.B:
					return typeof(B);
			}

			throw new NotSupportedException();
		}
	}


	class PolymorphicConverter : JsonPolymorphicConverter<Polymorphic>
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
}
