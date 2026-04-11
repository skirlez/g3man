using Gtk;

namespace g3man;

public static class ExtensionMethods
{
	public static void SetMargin(this Widget widget, int margin) {
		widget.SetMarginStart(margin);
		widget.SetMarginEnd(margin);
		widget.SetMarginTop(margin);
		widget.SetMarginBottom(margin);
	}
	
	public static void SetAlign(this Widget widget, Align align) {
		widget.SetHalign(align);
		widget.SetValign(align);
	}
	
	public static void SetExpand(this Widget widget, bool expand) {
		widget.SetHexpand(expand);
		widget.SetVexpand(true);
	}
	
	public static Box With(this Box box, params  Widget[] widgets) {
		foreach (Widget w in widgets) {
			box.Append(w);
		}
		return box;
	}
}