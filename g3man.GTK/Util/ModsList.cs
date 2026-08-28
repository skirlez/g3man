using System.Diagnostics;
using g3man.Core.Models;
using Gtk;

namespace g3man.GTK.Util;

public class ModsList : UIList<IMod, (ListBoxRow, CheckButton)> {
	public List<CheckButton> checkButtons;
	public ModsList(IList<IMod> list, ListBox listBox, Func<IMod, (ListBoxRow, CheckButton)> makeRow, Widget placeholder) : base(list, listBox, makeRow, x => x.Item1, placeholder) {
		checkButtons = [];
	}
	public new void Clear() {
		base.Clear();
		checkButtons.Clear();
	}

	protected override void AddResiduals((ListBoxRow, CheckButton) result) {
		checkButtons.Add(result.Item2);
	}
	protected override void InsertResiduals((ListBoxRow, CheckButton) result, int index) {
		checkButtons.Insert(index, result.Item2);
	}
	protected override void SetResiduals((ListBoxRow, CheckButton) result, int index) {
		checkButtons[index] = result.Item2;
	}
	protected override void RemoveResidualsAt(int index) {
		checkButtons.RemoveAt(index);
	}
	
	public Dictionary<IMod, bool> GetEnabledMods() {
		UI.ThreadAssert();
		return list.Select((x, i) => (x, checkButtons[i].GetActive())).ToDictionary(combined => (combined.Item1), combined => combined.Item2);
	}
	public List<IMod> GetEnabledModsList() {
		UI.ThreadAssert();
		return list.Where((x, i) => checkButtons[i].GetActive()).ToList();
	}

	public void Move(int index, int newIndex) {
		ListBoxRow row = listBox.GetRowAtIndex(index)!;
		listBox.Remove(row);
		listBox.Insert(row, newIndex);
		
		IMod mod = list[index];
		list.RemoveAt(index);
		list.Insert(newIndex, mod);
		
		CheckButton button = checkButtons[index];
		checkButtons.RemoveAt(index);
		checkButtons.Insert(newIndex, button);
	}

	public void SetEnabled(IMod mod, bool isEnabled) {
		int index = list.IndexOf(mod);
		checkButtons[index].SetActive(isEnabled);
	}
}