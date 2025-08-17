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
#include <gtkmm/filedialog.h>
#include <gtkmm/filedialog.h>
#include "forgery_mod.h"

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
	Label* mod_information;

	Entry* steam_directory_entry;
	Label* steam_status_label;

	Entry* umc_path_entry;

	DropDown* theme_selection;
	CheckButton* isolate_save_check;

	void switch_page(Widget* page);
	void reorder_button_pressed(int direction);
	void update_steam_entry_status();
	void update_umc_entry_status();
	void save_settings();
	void load_settings();
	void free_mods_list_entries();
	void update_mod_information(forgery_mod* mod);
	std::string load_mod_listing();
	void get_mods_list();
	void on_exit();
	void browse_button_clicked(std::string title, Entry* entry, bool select_folder);

	void apply_mods();
};