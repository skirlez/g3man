using System.Collections;
using g3man.Core.Models;
using Gtk;

namespace g3man.GTK.Util;

/**
 * This class is used for both profile lists and game lists. It is used to keep 3 components in sync:
 * - The list of instances
 * - The ListBox of GTK
 * - The corresponding selection buttons for each instance
 *
 * For convenience, it also handles the "placeholder" no items widget.
 */
public class UIList<T> : IList<T> {

	private IList<T> list;
	private List<Button> buttons;
	private ListBox listBox;
	private Func<T, (ListBoxRow, Button)> makeRow;
	private Widget placeholder;
	
	public UIList(IList<T> list, ListBox listBox, Func<T, (ListBoxRow, Button)> makeRow, Widget placeholder) {
		this.list = list;
		this.buttons = [];
		this.listBox = listBox;
		this.makeRow = makeRow;
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
		(ListBoxRow row, Button button) = makeRow(item); 
		listBox.Append(row);
		buttons.Add(button);
	}
	public void Clear() {
		list.Clear();
		listBox.RemoveAll();
		listBox.SetPlaceholder(placeholder);
		buttons.Clear();
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
		list.RemoveAt(index);
		listBox.Remove(listBox.GetRowAtIndex(index)!);
		buttons.RemoveAt(index);
		return true;
	}
	public int Count => list.Count;
	public bool IsReadOnly => false;

	public int IndexOf(T item) {
		return list.IndexOf(item);
	}
	public void Insert(int index, T item) {
		list.Insert(index, item);
		(ListBoxRow row, Button button) = makeRow(item); 
		listBox.Insert(row, index);
		buttons.Insert(index, button);
	}
	public void RemoveAt(int index) {
		list.RemoveAt(index);
		listBox.Remove(listBox.GetRowAtIndex(index)!);
		buttons.RemoveAt(index);
	}

	public T this[int index] {
		get => list[index];
		set {
			list[index] = value;
			listBox.Remove(listBox.GetRowAtIndex(index)!);
			(ListBoxRow row, Button button) = makeRow(value); 
			listBox.Insert(row, index);
			buttons[index] = button;
		}
	}

	public void SetOrRemoveAt(T? item, int index) {
		if (item is null)
			RemoveAt(index);
		else
			this[index] = item;
	}
	
	public void UpdateButtonStates(T? item) {
		foreach (Button button in buttons) {
			button.SetSensitive(true);
		}
		if (item is null)
			return;
		int index = list.IndexOf(item);
		buttons[index].SetSensitive(false);
	}
}