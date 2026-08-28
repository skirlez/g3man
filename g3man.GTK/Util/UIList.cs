using System.Collections;
using System.Diagnostics;
using g3man.Core.Models;
using Gtk;

namespace g3man.GTK.Util;

/**
 * This class is used for game, profile and mod lists. It is used to keep 3 components in sync:
 * - The list of instances
 * - The ListBox of GTK
 * - The "residuals": which are selection buttons for games/profiles, and the check buttons for mods.
 *
 * For convenience, it also handles the "placeholder" no items widget.
 */
public abstract class UIList<T, E> : IList<T> {

	protected IList<T> list;
	protected ListBox listBox;
	private Func<T, E> makeRow;
	private Func<E, ListBoxRow> extract;
	private Widget placeholder;

	public UIList(IList<T> list, ListBox listBox, Func<T, E> makeRow, Func<E, ListBoxRow> extract, Widget placeholder) {
		Debug.Assert(list.Count == 0);
		this.list = list;
		this.listBox = listBox;
		this.makeRow = makeRow;
		this.extract = extract;
		this.placeholder = placeholder;
		listBox.SetPlaceholder(placeholder);
	}
	
	public IEnumerator<T> GetEnumerator() {
		return list.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return list.GetEnumerator();
	}
	public void Add(T item) {
		list.Add(item);
		E result = makeRow(item); 
		listBox.Append(extract(result));
		AddResiduals(result);
	}

	protected abstract void AddResiduals(E result);
	
	public void Clear() {
		list.Clear();
		listBox.RemoveAll();
		listBox.SetPlaceholder(placeholder);
	}
	
	public bool Contains(T item) {
		return list.Contains(item);
	}
	public void CopyTo(T[] array, int arrayIndex) {
		list.CopyTo(array, arrayIndex);
	}
	public bool Remove(T item) {
		int index = list.IndexOf(item);
		if (index == -1)
			return false;
		RemoveAt(index);
		return true;
	}
	
	public int Count => list.Count;
	public bool IsReadOnly => false;

	public int IndexOf(T item) {
		return list.IndexOf(item);
	}
	public void Insert(int index, T item) {
		list.Insert(index, item);
		E result = makeRow(item); 
		listBox.Insert(extract(result), index);
		InsertResiduals(result, index);
		//buttons.Insert(index, button);
	}

	protected abstract void InsertResiduals(E result, int index);
	
	public void RemoveAt(int index) {
		list.RemoveAt(index);
		listBox.Remove(listBox.GetRowAtIndex(index)!);
		RemoveResidualsAt(index);
	}
	protected abstract void RemoveResidualsAt(int index);

	public T this[int index] {
		get => list[index];
		set {
			list[index] = value;
			listBox.Remove(listBox.GetRowAtIndex(index)!);
			E result = makeRow(value); 
			listBox.Insert(extract(result), index);
			SetResiduals(result, index);
		}
	}

	protected abstract void SetResiduals(E result, int index);
	
	public void SetOrRemoveAt(T? item, int index) {
		if (item is null)
			RemoveAt(index);
		else
			this[index] = item;
	}
	
}