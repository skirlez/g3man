using Gtk;

namespace g3man;

public static class ExtendWidget
{
    public static void SetMargin(this Widget widget, int margin) {
        widget.SetMarginStart(margin);
        widget.SetMarginEnd(margin);
        widget.SetMarginTop(margin);
        widget.SetMarginBottom(margin);
    }
}