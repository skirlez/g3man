using Gtk;

namespace g3man.UI;

public class PopupWindow : Window
{
    private Window owner;
    public PopupWindow(Window owner, string title, string message, string buttonText) {
        SetTitle(title);
        SetResizable(false);
        SetSizeRequest(400, 200);
        this.owner = owner;
        
        Label messageLabel = Label.New(message);
        messageLabel.SetJustify(Justification.Center);
        messageLabel.SetHalign(Align.Center);
        messageLabel.SetValign(Align.Center);
        messageLabel.SetVexpand(true);
        
        Button closeButton = Button.NewWithLabel(buttonText);
        closeButton.SetValign(Align.End);
        closeButton.SetHalign(Align.Center);
        closeButton.OnClicked += (_, _) => {
            Close();
        };
        
       
        
        Box box = Box.New(Orientation.Vertical, 5);
        box.Append(messageLabel);
        box.Append(closeButton);
        box.SetMargin(10);
        
        SetChild(box);
    }

    public void Dialog() {
        SetTransientFor(owner);
        SetModal(true);
        Present();
    }
}