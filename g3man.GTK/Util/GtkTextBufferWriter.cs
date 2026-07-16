using System.Text;
using Gtk;

namespace g3man.GTK.Util;

public class GtkTextBufferWriter : TextWriter {
	public override Encoding Encoding { get; } = Encoding.UTF8;
	
	private readonly Gtk.TextBuffer gtkBuffer = new Gtk.TextBuffer();
	
	public override void Write(char c) {
		UI.RunOnMainThreadEventually(() => {
			gtkBuffer.GetEndIter(out TextIter iter);
			gtkBuffer.Insert(iter, c.ToString(), 1);
		});
	}
	
	public override void Write(char[]? buffer) {
		if (buffer is null)
			return;
		UI.RunOnMainThreadEventually(() => {
			gtkBuffer.GetEndIter(out TextIter iter);
			gtkBuffer.Insert(iter, new string(buffer), buffer.Length);
		});
	}

	public override void Write(char[] buffer, int index, int count) {
		UI.RunOnMainThreadEventually(() => {
			gtkBuffer.GetEndIter(out TextIter iter);
			gtkBuffer.Insert(iter, new string(buffer, index, count), count);
		});
	}

	public override void Write(string? value) {
		if (value is null)
			return;
		UI.RunOnMainThreadEventually(() => {
			gtkBuffer.GetEndIter(out TextIter iter);
			gtkBuffer.Insert(iter, value, value.Length);
		});
	}

	public override void WriteLine(string? value) {
		if (value is null)
			return;
		UI.RunOnMainThreadEventually(() => {
			gtkBuffer.GetEndIter(out TextIter iter);
			gtkBuffer.Insert(iter, value, value.Length);
			gtkBuffer.Insert(iter, Environment.NewLine, Environment.NewLine.Length);
		});
	}
	
	
	public TextBuffer GetBuffer() {
		return gtkBuffer;
	}
}