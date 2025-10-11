using System.Diagnostics.CodeAnalysis;

namespace g3man;

public class Result<T, E> {
	private readonly bool ok;
	private readonly object data;
	public Result([DisallowNull] T data) {
		this.data = data;
		ok = true;
	}
	public Result([DisallowNull] E data) {
		this.data = data;
		ok = false;
	}

	public bool IsOk() {
		return ok;
	}

	public T GetValue() {
		return (T)data;
	}
	public E GetError() {
		return (E)data;
	}
}

