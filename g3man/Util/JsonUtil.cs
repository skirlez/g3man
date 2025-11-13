using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace g3man.Util;
/*
 * Utility class for reading from a JsonDocument.
 * TODO: I started rewriting this in the json_new branch but it sucks so bad there.
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
	
	
	


	public static T GetOrDefaultClass<T>(JsonElement root, string field, T fallback) where T : class {
		try {
			return (fallback switch {
				string => GetStringOrThrow(root, field) as T,
				string[] => GetStringArrayOrThrow(root, field) as T,
				JsonElement[] => GetObjectArrayOrThrow(root, field) as T,
				_ => throw new ArgumentException($"Unsupported type ({typeof(T).Name})"),
			})!;
		}
		catch (Exception _) {
			return fallback;
		}
	}
	public static T GetOrDefault<T>(JsonElement root, string field, T fallback) where T : struct {
		try {
			return (fallback switch {
				bool => Unsafe.BitCast<bool, T>(GetBooleanOrThrow(root, field)),
				int => Unsafe.BitCast<int, T>(GetNumberOrThrow(root, field)),
				_ => throw new ArgumentException(),
			})!;
		}
		catch (Exception _) {
			return fallback;
		}
	}
}