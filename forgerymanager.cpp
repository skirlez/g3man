#include "forgerymanager.h"
#include <iostream>
#include <filesystem>
#include <fstream>

#include "nlohmann/json.hpp"
#include <gtkmm/settings.h>

#include "directories.h"

using namespace Gtk;
namespace fs = std::filesystem;

void save_and_launch_game() {
	std::cout << "Test" << std::endl;
}

ForgeryManager::ForgeryManager() {
	signal_hide().connect([this]() {
        g_message("%s", "here");
    });

	page_box = make_managed<Box>(Orientation::HORIZONTAL, 0);
	mods_page = make_managed<Box>(Orientation::VERTICAL, 0);	
	settings_page = make_managed<Box>(Orientation::VERTICAL, 0);
	Box* about_page = make_managed<Box>(Orientation::VERTICAL, 0);

	std::array<Widget*, 3> pages = {mods_page, settings_page, about_page};
	std::array<std::string, 3> page_names = {"Mods", "Settings", "About"};
	page_stack = make_managed<Stack>();
	page_stack->set_hexpand();

	Box* page_buttons = make_managed<Box>(Orientation::VERTICAL, 0);	
	page_buttons->set_valign(Align::CENTER);
	page_buttons->set_halign(Align::CENTER);
	page_buttons->set_margin(10);

	for (size_t i = 0; i < page_names.size(); i++) {
		Button* page_button = make_managed<Button>(page_names[i]);
		page_button->set_margin(5);
		page_button->signal_clicked().connect([pages, i, this]() {
			this->switch_page(pages[i]); 
		});
		page_buttons->append(*page_button);
		page_stack->add(*pages[i]);
	}

	/*
	Box* page_box_spacer_left = make_managed<Box>(Orientation::HORIZONTAL);
	page_box_spacer_left->set_margin(10);
	page_box_spacer_left->set_hexpand(true);

	Box* page_box_spacer_right = make_managed<Box>(Orientation::HORIZONTAL);
	page_box_spacer_right->set_margin(10);
	page_box_spacer_right->set_hexpand(true);
	*/
	page_box->append(*page_buttons);
	page_box->append(*page_stack);
	page_box->set_vexpand(true);

	page_box->set_homogeneous(false);


	page_stack->set_visible_child(*mods_page);
	set_child(*page_box);


	Box* reorder_mods_box = make_managed<Box>(Orientation::HORIZONTAL);
	reorder_mods_box->set_valign(Align::CENTER);
	reorder_mods_box->set_halign(Align::CENTER);

	Button* move_mod_up = make_managed<Button>("↑");
	Button* move_mod_down = make_managed<Button>("↓");
	move_mod_up->signal_clicked().connect([this]() {
		this->reorder_button_pressed(-1);
	});
	move_mod_down->signal_clicked().connect([this]() {
		this->reorder_button_pressed(1);
	});
	reorder_mods_box->append(*move_mod_up);
	reorder_mods_box->append(*move_mod_down);
	move_mod_up->set_margin(5);
	move_mod_down->set_margin(5);

	mods_list = make_managed<ListBox>();
	for (int i = 1; i <= 5; i++) {
		// TODO: clean these up. They have to not be managed because we remove them from their widgets and reinsert, which would, if managed, cause their deletion.
		ListBoxRow* row = new ListBoxRow();
		row->set_hexpand();
		Label* label = make_managed<Label>("Mod " + std::to_string(i));
		label->set_margin(10);
		row->set_child(*label);
		mods_list->append(*row);
	}

	mods_list->set_hexpand();
	mods_page->set_hexpand();

	mods_page->append(*mods_list);
	mods_page->append(*reorder_mods_box);



	Box* install_directory_box = make_managed<Box>(Orientation::VERTICAL);
	nubby_install_directory = make_managed<Entry>();
	nubby_install_directory->signal_changed().connect([this]() {
		this->update_install_directory_status();
	});

	Label* nubby_install_directory_label = make_managed<Label>("Nubby's Number Factory game folder");
	install_directory_status = make_managed<Label>("");
	install_directory_status->set_halign(Align::START);

	nubby_install_directory->set_halign(Align::START);
	nubby_install_directory->set_max_width_chars(75);
	

	nubby_install_directory_label->set_halign(Align::START);
	

	Button* save_settings_button = make_managed<Button>("Save Settings");
	save_settings_button->signal_clicked().connect([this]() {
		this->save_settings();
	});
	save_settings_button->set_halign(Align::END);

	install_directory_box->append(*nubby_install_directory_label);
	install_directory_box->append(*nubby_install_directory);
	install_directory_box->append(*install_directory_status);
	install_directory_box->set_margin(10);

	Box* isolate_save_box = make_managed<Box>(Orientation::VERTICAL);
	isolate_save_check = make_managed<CheckButton>("Isolate save");
	isolate_save_check->set_tooltip_text("Separates your vanilla save from your modded save. This is highly recommended.");

	isolate_save_box->append(*isolate_save_check);
	isolate_save_box->set_halign(Align::START);
	isolate_save_box->set_margin(10);

	settings_page->append(*install_directory_box);
	settings_page->append(*isolate_save_box);
	//settings_page->append(*theme_box);
	settings_page->append(*save_settings_button);
	settings_page->set_margin(20);

	Label* about_label = make_managed<Label>("Forgery Manager v0");
	about_page->append(*about_label);
	about_page->set_valign(Align::CENTER);

	load_settings();
}

ForgeryManager::~ForgeryManager() {}
void ForgeryManager::switch_page(Widget* page) {
	this->page_stack->set_visible_child(*page);
}

void ForgeryManager::on_exit() {

}

void ForgeryManager::reorder_button_pressed(const int direction) {
	ListBoxRow* row = mods_list->get_selected_row();
	int index = row->get_index();
	if (direction == -1 && index == 0)
		return;
	mods_list->remove(*row);
	mods_list->insert(*row, index + direction);
	mods_list->unselect_row();
	mods_list->select_row(*row);
	index = row->get_index();
}


void ForgeryManager::update_install_directory_status() {
	std::string install_directory = nubby_install_directory->get_text();
	if (install_directory.empty()) {
		install_directory_status->set_text("");
		return;
	}
	fs::path path = install_directory;
	if (fs::exists(path/"NNF_FULLVERSION.exe"))
		install_directory_status->set_text("Game files detected");
	else {
		install_directory_status->set_text("Could not find NNF_FULLVERSION.exe");
	}

}

void ForgeryManager::save_settings() {
	fs::path config_file_path = directories::get_or_create_config_directory()/"settings.json";

	nlohmann::json json = {
		{"nubby_install_directory", this->nubby_install_directory->get_text()},
		{"isolate_save", this->isolate_save_check->get_active()}
	};

	std::ofstream file = std::ofstream(config_file_path.string());
	file << json.dump();
}

void ForgeryManager::load_settings() {
	fs::path config_file_path = directories::get_config_directory()/"settings.json";
	nlohmann::json json;

	if (!fs::exists(config_file_path)) {
		json = {
			{"nubby_install_directory", directories::try_guess_nubby_install_directory()},
			{"isolate_save", true}
		};
	}
	else {
		std::ifstream file = std::ifstream(config_file_path.string());
		file >> json;
	}

	this->nubby_install_directory->set_text(json["nubby_install_directory"].get<std::string>());
	this->isolate_save_check->set_active(json["isolate_save"].get<bool>());
}