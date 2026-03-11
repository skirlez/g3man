using System.Diagnostics;
using System.Text;
using Gtk;

namespace g3man.Util;

public class GtkBufferTextWriter : TextWriter {
	
	
	public override Encoding Encoding { get; } = Encoding.UTF8;

	private readonly StringBuilder hold = new StringBuilder();
	private readonly Gtk.TextBuffer gtkBuffer = new Gtk.TextBuffer();
	
	public override void Write(char c) {
		hold.Append(c);
		if (c == '\n')
			Flush();
	}

	public override void Flush() {
		gtkBuffer.GetEndIter(out TextIter iter);
		int length = hold.Length;
		gtkBuffer.Insert(iter, hold.ToString(), length);
		hold.Clear();
	}
	
	public TextBuffer GetBuffer() {
		return gtkBuffer;
	}
}