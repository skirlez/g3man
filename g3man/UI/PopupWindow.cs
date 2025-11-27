using Gtk;

namespace g3man.UI;

public class PopupWindow : Window
{

    
    public static Action<PopupWindow> CloseAction = (window => {
        window.Close();
    });
    private Window owner;

    public PopupWindow(Window owner, string title, string message, string buttonText) 
        : this(owner, title, message, [buttonText], [CloseAction]) { }

    public PopupWindow(Window owner, string title, string message, 
            string[] buttonTexts, Action<PopupWindow>[] actions) {
        SetTitle(title);
        SetResizable(false);
        SetSizeRequest(400, 200);
        this.owner = owner;
        
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
    }

    public void Dialog() {
        SetTransientFor(owner);
        SetModal(true);
        Present();
    }
}