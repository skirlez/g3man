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
	
	public static bool GetBooleanOrThrow(JsonElement element, string field) {
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind != JsonValueKind.False && inner.ValueKind != JsonValueKind.True)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected a boolean, but got {inner.ValueKind.ToString()})");
		return inner.GetBoolean();
	}
	
	/**
	* Returns a string property with the name `field`. 
	* If it does not exist, throws `InvalidDataException`.
	* If Fallback is specified and not null, instead of throwing when the key is missing,
	* it uses the fallback.
	*/
	public static string GetStringOrThrow(JsonElement element, string field, string? fallback = null) {
		if (fallback is not null && !element.TryGetProperty(field, out _))
			return fallback;
		JsonElement inner = GetPropertyOrThrow(element, field);
		if (inner.ValueKind != JsonValueKind.String)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected a string, but got {inner.ValueKind.ToString()})");

		string? result = inner.GetString();
		Debug.Assert(result is not null);
		return result;
	}
	public static int GetNumberOrThrow(JsonElement element, string field) {
		JsonElement inner = GetPropertyOrThrow(element, field);
		if (inner.ValueKind != JsonValueKind.Number)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected a number, but got {inner.ValueKind.ToString()})");
		try {
			return inner.GetInt32();
		}
		catch (FormatException e) {
			throw new InvalidDataException($"Field {field} should be a number, but is not an integer/is too big/is too small/is weird");
		}
	}
	public static string[] GetStringArrayOrThrow(JsonElement element, string field, string[]? fallback = null) {
		if (fallback is not null && !element.TryGetProperty(field, out _))
			return fallback;
		JsonElement inner = GetPropertyOrThrow(element, field);
		
		if (inner.ValueKind != JsonValueKind.Array)
			throw new InvalidDataException($"Field {field} is of the wrong type (Expected an array, but got {inner.ValueKind.ToString()})");

		JsonElement[] array = inner.EnumerateArray().ToArray();
		List<string> final = [];
		foreach (JsonElement arrayElement in array) {
			if (arrayElement.ValueKind != JsonValueKind.String) {
				throw new InvalidDataException($"Field {field} is of the wrong type (Expected a string array, but one of the elements of the array had type {element.ValueKind.ToString()})");
			}
			final.Add(arrayElement.GetString()!);
		}
		return final.ToArray();
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