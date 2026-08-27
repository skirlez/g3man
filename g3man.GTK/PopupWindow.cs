using g3man.GTK.Util;
using Gtk;

namespace g3man.GTK;

public class PopupWindow : G3manWindow {
    public static Action<PopupWindow> CloseWindowAction = (window => {
        window.Close();
    });
    public PopupWindow(string title, string message, string buttonText) 
        : this(title, message, [buttonText], [CloseWindowAction]) {}

    public PopupWindow(string title, string message, 
            string[] buttonTexts, Action<PopupWindow>[] actions, Action<PopupWindow>? beforeClose = null) {
        SetTitle(title);
        SetResizable(false);
        SetSizeRequest(400, 200);

        Label messageLabel = Label.New(message);
        messageLabel.SetJustify(Justification.Center);
        messageLabel.SetHalign(Align.Center);
        messageLabel.SetValign(Align.Center);
        messageLabel.SetVexpand(true);
        
        Box buttonsBox =  Box.New(Orientation.Horizontal, 10);

        for (int i = 0; i < buttonTexts.Length; i++) {
            Button button = Button.NewWithLabel(buttonTexts[i]);
            int indexCapture = i;
            button.OnClicked += (_, _) => { actions[indexCapture](this); };
            buttonsBox.Append(button);
        }
        buttonsBox.SetValign(Align.End);
        buttonsBox.SetHalign(Align.Center);
        

        Box box = Box.New(Orientation.Vertical, 5);
        box.Append(messageLabel);
        box.Append(buttonsBox);
        box.SetMargin(10);
        
        SetChild(box);

        if (beforeClose is not null) {
            OnCloseRequest += (_, _) => {
                beforeClose(this);
                return false;
            };
        }
    }
    
    public static async Task<bool> PopupIfError(Window window, Func<Task> action) {
        try {
           await action();
        }
        catch (Exception e) {
            UI.Logger.Error(e);
            PopupWindow popup = new("Error!" ,e.Message, "Close");
            popup.Dialog(window);
            return true;
        }

        return false;
    }

    public void Dialog(Window window) {
        SetTransientFor(window);
        SetModal(true);
        Present();
    }
}