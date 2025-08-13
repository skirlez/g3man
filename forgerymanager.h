#pragma once

#include <gtkmm/button.h>
#include <gtkmm/window.h>
#include <gtkmm/box.h>
#include <gtkmm/label.h>
#include <gtkmm/stack.h>
#include <gtkmm/listbox.h>
#include <gtkmm/entry.h>
#include <gtkmm/dropdown.h>
#include <gtkmm/checkbutton.h>


using namespace Gtk;

class ForgeryManager : public Window
{

public:
  ForgeryManager();
  ~ForgeryManager() override;

private:
  Box* page_box;
  Box* mods_page;
  Box* settings_page;

  Stack* page_stack;

  ListBox* mods_list;

  Entry* nubby_install_directory;
  Label* install_directory_status;

  DropDown* theme_selection;
  CheckButton* isolate_save_check;

  void switch_page(Widget* page);
  void reorder_button_pressed(int direction);
  void update_install_directory_status();
  void save_settings();
  void load_settings();
  void on_exit();
};