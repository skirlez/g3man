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

	Entry* nubby_directory_entry;
	Label* nubby_directory_status_label;
	void update_nubby_directory_label();

	#ifndef FORGERYMANAGER_UMC_PATH
	Entry* umc_path_entry;
	#endif

	DropDown* theme_selection;
	CheckButton* isolate_save_check;

	void switch_page(Widget* page);
	void reorder_button_pressed(int direction);
	
	void save_settings();
	void load_settings();
	void free_mods_list_entries();
	void update_mod_information(forgery_mod_entry* mod);
	void create_mods_directory_and_load_listing();
	void get_mods_list();
	void on_exit();
	void browse_button_clicked(std::string title, Entry* entry, bool select_folder);

	void apply_mods();
};