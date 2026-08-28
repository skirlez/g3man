using Gtk;

namespace g3man.GTK.Util;

public class SelectableUIList<T> : UIList<T, (ListBoxRow, Button)> {
	private List<Button> buttons;

	public SelectableUIList(IList<T> list, ListBox listBox, Func<T, (ListBoxRow, Button)> makeRow, Widget placeholder) 
			: base(list, listBox, makeRow, x => x.Item1, placeholder) {
		buttons = [];
	}
	public new void Clear() {
		base.Clear();
		buttons.Clear();
	}

	protected override void AddResiduals((ListBoxRow, Button) result) {
		buttons.Add(result.Item2);
	}
	protected override void InsertResiduals((ListBoxRow, Button) result, int index) {
		buttons.Insert(index, result.Item2);
	}
	protected override void SetResiduals((ListBoxRow, Button) result, int index) {
		buttons[index] = result.Item2;
	}
	protected override void RemoveResidualsAt(int index) {
		buttons.RemoveAt(index);
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