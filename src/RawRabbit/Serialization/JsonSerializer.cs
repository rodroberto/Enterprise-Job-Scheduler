﻿using System;
using System.IO;
using Newtonsoft.Json;

namespace RawRabbit.Serialization
{
	public class JsonSerializer : StringSerializerBase
	{
		private readonly Newtonsoft.Json.JsonSerializer _json;
		private const string _applicationJson = "application/json";
		public override string ContentType => _applicationJson;

		public JsonSerializer(Newtonsoft.Json.JsonSerializer json)
		{
			_json = json;
		}

		public override string SerializeToString(object obj)
		{
			if (obj == null)
			{
				return string.Empty;
			}
			if (obj is string str)
			{
				return str;
			}
			string serialized;
			using (var sw = new StringWriter())
			{
				_json.Serialize(sw, obj);
				serialized = sw.GetStringBuilder().ToString();
			}
			return serialized;
		}

		public override object Deserialize(Type type, string str)
		{
			if (type == typeof(string))
			{
				return str;
			}
			object obj;
			using (var jsonReader = new JsonTextReader(new StringReader(str)))
			{
				obj = _json.Deserialize(jsonReader, type);
			}
			return obj;
		}
	}
}
