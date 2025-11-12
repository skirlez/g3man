using System.Diagnostics;
using System.Text.Json;

namespace g3man.Util;
/*
 * Utility class for reading from a JsonDocument.
 * A lot of duplicated code, but it's worth it.
 */
public static class JsonUtil {
	private static JsonElement GetPropertyOrThrow(JsonElement element, string field) {
		try {
			return element.GetProperty(field);
		}
		catch (KeyNotFoundException e) {
			throw new InvalidDataException($"Required field {field} not found");
		}
		catch (InvalidOperationException e) {
			throw new InvalidDataException($"Tried to find {field} inside something that wasn't an object");
		}
	}



	public abstract class JsonType<T>(string name) {
		public abstract bool CheckValueKind(JsonValueKind valueKind);

		public string GetHumanName() {
			return name;
		}

		public abstract T Get(JsonElement element, Contingency contingency, T defaultValue = default(T)!);
	}

	private class BoolJsonType() : JsonType<bool>("a boolean") {
		public override bool CheckValueKind(JsonValueKind valueKind) {
			return valueKind == JsonValueKind.False || valueKind == JsonValueKind.True;
		}
		public override bool Get(JsonElement element, Contingency contingency, bool defaultValue = false) {
			return element.GetBoolean();
		}
	}
	
	private class IntJsonType() : JsonType<int>("an integer") {
		public override bool CheckValueKind(JsonValueKind valueKind) {
			return valueKind == JsonValueKind.Number;
		}
		public override int Get(JsonElement element, Contingency contingency, int defaultValue = 0) {
			try {
				return element.GetInt32();
			}
			catch (FormatException e) {
				if (contingency == Contingency.UseDefaultValue)
					return defaultValue;
				throw new InvalidDataException($"Expected {GetHumanName()}, but found a number that's too big/is too small/isn't round");
			}
		}
	}

	private class StringJsonType() : JsonType<string>("a string") {
		public override bool CheckValueKind(JsonValueKind valueKind) {
			return valueKind == JsonValueKind.String;
		}
		public override string Get(JsonElement element, Contingency contingency, string defaultValue = null!) {
			return element.GetString()!;
		}
	}
	
	private class StringArrayJsonType() : JsonType<string[]>("a string array") {
		public override bool CheckValueKind(JsonValueKind valueKind) {
			return valueKind == JsonValueKind.Array;
		}
		public override string[] Get(JsonElement element, Contingency contingency, string[] defaultValue) {
			JsonElement[] array = element.EnumerateArray().ToArray();
			List<string> final = [];
			foreach (JsonElement arrayElement in array) {
				JsonType<string> stringType = GetJsonType<string>();
				if (stringType.CheckValueKind(arrayElement.ValueKind)) {
					if (contingency == Contingency.UseDefaultValue)
						return defaultValue;
					throw new InvalidDataException($"Expected a string array, but one of the elements of the array had type {element.ValueKind.ToString()})");
				}
				string str = stringType.Get(arrayElement, Contingency.ThrowOnAnything);
				final.Add(str);
			}
			return final.ToArray();
		}
	}
	
	private static JsonType<T> GetJsonType<T>() {
		if (typeof(T) == typeof(string))
			return (new StringJsonType() as JsonType<T>)!;
		if (typeof(T) == typeof(bool))
			return (new BoolJsonType() as JsonType<T>)!;
		if (typeof(T) == typeof(int))
			return (new IntJsonType() as JsonType<T>)!;
		return null!;
	}
	
	public enum Contingency {
		UseDefaultValue,
		ThrowOnAnything
	}
	
	public static T GetWithContingency<T>(JsonElement element, string field, Contingency contingency, T defaultValue = default(T)!) {
		JsonElement inner;
		try {
			inner = element.GetProperty(field);
		}
		catch (KeyNotFoundException e) {
			if (contingency == Contingency.UseDefaultValue || contingency == Contingency.AllowMissing)
				return defaultValue;
			throw new InvalidDataException($"Required field {field} not found");
		}
		/*
		catch (InvalidOperationException e) {
			throw new InvalidDataException($"Tried to find {field} inside something that wasn't an object");
		}
		*/
		JsonType<T> jsonType = GetJsonType<T>();
		
		if (!jsonType.CheckValueKind(inner.ValueKind)) {
			if (contingency == Contingency.UseDefaultValue)
				return defaultValue;
			throw new InvalidDataException($"Expected ${jsonType.GetHumanName()}, but got {inner.ValueKind.ToString()})");
		}

		return jsonType.Get(inner, contingency, defaultValue);
	}
	
	
	public static string[] GetStringArrayOrThrow(JsonElement element, string field, string[]? fallback = null) {
		if (fallback is not null && !element.TryGetProperty(field, out _))
			return fallback;
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected an array, but got {inner.ValueKind.ToString()})");


	}
	public static JsonElement[] GetObjectArrayOrThrow(JsonElement element, string field, JsonElement[]? fallback = null) {
		if (fallback is not null && !element.TryGetProperty(field, out _))
			return fallback;
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected an array, but got {inner.ValueKind.ToString()})");

		JsonElement[] array = inner.EnumerateArray().ToArray();
		foreach (JsonElement arrayElement in array) {
			if (arrayElement.ValueKind != JsonValueKind.Object) {
				throw new InvalidDataException($"Field {field} is of the wrong type (Expected a string array, but one of the elements of the array had type {element.ValueKind.ToString()})");
			}
		}
		return array;
	}
	
	public static string[] GetStringOrStringArrayOrThrow(JsonElement element, string field) {
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind == JsonValueKind.String)
			return [GetStringOrThrow(element, field)];
		if (inner.ValueKind == JsonValueKind.Array)
			return GetStringArrayOrThrow(element, field);
		throw new InvalidDataException($"Field {field} is of the wrong type (Expected a string or a string array, but got {inner.ValueKind.ToString()})");
	}
	
	/*
	public static JsonElement[] GetArrayOrThrow(JsonElement element, string field) {
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected an array, but got {inner.ValueKind.ToString()})");

		return inner.EnumerateArray().ToArray();
	}
	*/
}