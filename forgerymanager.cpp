#include "forgerymanager.h"
#include <iostream>
#include <filesystem>
#include <fstream>
#include <optional>

#include <gtkmm/alertdialog.h>
#include "nlohmann/json.hpp"

#include "patcher.h"
#include "directories.h"

using namespace Gtk;
namespace fs = std::filesystem;


ForgeryManager::ForgeryManager() {


	
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


	Box* manage_mods_box = make_managed<Box>(Orientation::HORIZONTAL);
	manage_mods_box->set_valign(Align::CENTER);
	manage_mods_box->set_halign(Align::CENTER);

	Button* move_mod_up = make_managed<Button>("↑");
	Button* move_mod_down = make_managed<Button>("↓");
	move_mod_up->signal_clicked().connect([this]() {
		this->reorder_button_pressed(-1);
	});
	move_mod_down->signal_clicked().connect([this]() {
		this->reorder_button_pressed(1);
	});
	Button* refresh_mods_button = make_managed<Button>("Refresh");
	refresh_mods_button->signal_clicked().connect([this]() {
		this->load_mod_listing();
	});
	Button* install_from_zip_button = make_managed<Button>("Install from ZIP");
	install_from_zip_button->signal_clicked().connect([this]() {
		//TODO
	});
	Button* remove_mod_button = make_managed<Button>("Remove selected");
	remove_mod_button->signal_clicked().connect([this]() {
		//TODO
	});
	manage_mods_box->append(*refresh_mods_button);
	manage_mods_box->append(*move_mod_up);
	manage_mods_box->append(*move_mod_down);
	manage_mods_box->append(*install_from_zip_button);
	manage_mods_box->append(*remove_mod_button);
	move_mod_up->set_margin(5);
	move_mod_down->set_margin(5);
	refresh_mods_button->set_margin(5);
	install_from_zip_button->set_margin(5);
	remove_mod_button->set_margin(5);

	

	Button* apply_mods_button = make_managed<Button>("Apply mods!");
	apply_mods_button->set_halign(Align::CENTER);
	apply_mods_button->signal_clicked().connect([this]() {
		apply_mods();
	});
	mod_information = make_managed<Label>();
	mod_information->set_halign(Align::CENTER);
	mod_information->set_size_request(-1, 100);

	Box* bottom_box = make_managed<Box>(Orientation::VERTICAL);
	bottom_box->set_valign(Align::END);
	bottom_box->set_vexpand(true);
	bottom_box->append(*apply_mods_button);
	bottom_box->append(*mod_information);

	mods_list = make_managed<ListBox>();
	mods_list->signal_row_selected().connect([this](ListBoxRow* row) {
		if (row == nullptr)
			this->mod_information->set_text("");
		else
			this->update_mod_information((forgery_mod*)row->get_data("mod"));
	});
	
	mods_list->set_hexpand();

	mods_page->set_hexpand();
	mods_page->set_vexpand();

	mods_page->append(*mods_list);
	mods_page->append(*manage_mods_box);
	
	mods_page->append(*bottom_box);
	mods_page->set_homogeneous(false);



	Label* steam_directory_label = make_managed<Label>("Steam directory");
	steam_directory_label->set_halign(Align::START);


	steam_directory_entry = make_managed<Entry>();
	steam_directory_entry->signal_changed().connect([this]() {
		this->update_steam_entry_status();
	});
	steam_directory_entry->set_halign(Align::START);
	steam_directory_entry->set_max_width_chars(75);

	Button* steam_directory_browse = make_managed<Button>("Browse");
	steam_directory_browse->set_margin_end(10);
	steam_directory_browse->signal_clicked().connect([this]() {
		this->browse_button_clicked("Select the Steam folder", this->steam_directory_entry, true);
	});

	Box* steam_browse_and_directory_box = make_managed<Box>(Orientation::HORIZONTAL);
	steam_browse_and_directory_box->append(*steam_directory_browse);
	steam_browse_and_directory_box->append(*steam_directory_entry);

	steam_status_label = make_managed<Label>("");
	steam_status_label->set_halign(Align::START);

	Box* steam_directory_box = make_managed<Box>(Orientation::VERTICAL);
	steam_directory_box->append(*steam_directory_label);
	steam_directory_box->append(*steam_browse_and_directory_box);
	steam_directory_box->append(*steam_status_label);
	steam_directory_box->set_margin(10);

	Label* umc_label = make_managed<Label>("UndertaleModCli executable path");
	umc_label->set_halign(Align::START);

	umc_path_entry = make_managed<Entry>();
	umc_path_entry->set_halign(Align::START);
	umc_path_entry->set_max_width_chars(75);

	Button* umc_path_browse = make_managed<Button>("Browse");
	umc_path_browse->set_margin_end(10);
	umc_path_browse->signal_clicked().connect([this]() {
		this->browse_button_clicked("Select the UndertaleModCli executable", this->umc_path_entry, false);
	});

	Box* umc_browse_and_path_box = make_managed<Box>(Orientation::HORIZONTAL);
	umc_browse_and_path_box->append(*umc_path_browse);
	umc_browse_and_path_box->append(*umc_path_entry);

	
	Box* umc_path_box = make_managed<Box>(Orientation::VERTICAL);
	umc_path_box->append(*umc_label);
	umc_path_box->append(*umc_browse_and_path_box);
	umc_path_box->set_margin(10);




	Box* isolate_save_box = make_managed<Box>(Orientation::VERTICAL);
	isolate_save_check = make_managed<CheckButton>("Isolate save");
	isolate_save_check->set_active(false);
	isolate_save_check->set_tooltip_text("Separates your vanilla save from your modded save. This is highly recommended.");

	isolate_save_box->append(*isolate_save_check);
	isolate_save_box->set_halign(Align::START);
	isolate_save_box->set_margin(10);


	Button* save_settings_button = make_managed<Button>("Save Settings");
	save_settings_button->signal_clicked().connect([this]() {
		this->save_settings();
	});
	save_settings_button->set_halign(Align::END);
	save_settings_button->set_valign(Align::END);
	save_settings_button->set_vexpand(true);
	save_settings_button->set_margin(20);

	settings_page->append(*steam_directory_box);
	settings_page->append(*umc_path_box);
	settings_page->append(*isolate_save_box);
	//settings_page->append(*theme_box);
	settings_page->append(*save_settings_button);
	settings_page->set_margin(20);

	Label* about_label = make_managed<Label>("Forgery Manager v0");
	about_page->append(*about_label);
	about_page->set_valign(Align::CENTER);


	page_box = make_managed<Box>(Orientation::HORIZONTAL, 0);
	page_box->append(*page_buttons);
	page_box->append(*page_stack);
	page_box->set_vexpand(true);

	page_box->set_homogeneous(false);
	page_stack->set_visible_child(*mods_page);
	
	set_child(*page_box);

	load_settings();
	load_mod_listing();
}

ForgeryManager::~ForgeryManager() {}

void ForgeryManager::switch_page(Widget* page) {
	this->page_stack->set_visible_child(*page);
}

void ForgeryManager::on_exit() {

}

void ForgeryManager::reorder_button_pressed(const int direction) {
	ListBoxRow* row = mods_list->get_selected_row();
	if (row == nullptr)
		return;
	int index = row->get_index();
	if (direction == -1 && index == 0)
		return;
	mods_list->remove(*row);
	mods_list->insert(*row, index + direction);
	mods_list->unselect_row();
	mods_list->select_row(*row);
	index = row->get_index();
}


typedef struct {
	bool ok;
	std::string text;
} path_status;


path_status file_path_exists_status(fs::path path) {
	if (!fs::exists(path))
		return { false, "File does not exist" };
	return {true, "File exists"};
}

path_status get_steam_directory_status(fs::path path) {
	if (!fs::exists(path/"steamapps"/"common"))
		return { false, "Couldn't find steamapps/common - this is likely not a Steam folder" };
	if (!fs::exists(path/"steamapps"/"common"/"Nubby's Number Factory"))
		return { false, "Couldn't find Nubby folder in steamapps/common\nIf it is installed on a different drive, paste the Steam folder from there" };
	if (!fs::exists(path/"steamapps"/"common"/"Nubby's Number Factory"/"NNF_FULLVERSION.exe"))
		return { false, "Found game folder, but could not find NNF_FULLVERSION.exe" };
	return {true, "Nubby game files found"};
}

void ForgeryManager::update_steam_entry_status() {
	std::string steam_directory = steam_directory_entry->get_text();
	if (steam_directory.empty()) {
		steam_status_label->set_text("");
		return;
	}
	fs::path path = steam_directory;

	path_status status = get_steam_directory_status(path);
	steam_status_label->set_text(status.text);
}


void ForgeryManager::save_settings() {
	fs::path config_file_path = directories::get_or_create_config_directory()/"settings.json";

	nlohmann::json json = {
		{"steam_directory", this->steam_directory_entry->get_text()},
		{"umc_path", this->umc_path_entry->get_text()},
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
			{"steam_directory", directories::try_guess_steam_directory()},
			{"umc_path", ""},
			{"isolate_save", true}
		};
	}
	else {
		std::ifstream file = std::ifstream(config_file_path.string());
		file >> json;
	}

	this->steam_directory_entry->set_text(json["steam_directory"].get<std::string>());
	this->umc_path_entry->set_text(json["umc_path"].get<std::string>());
	this->isolate_save_check->set_active(json["isolate_save"].get<bool>());
}




void ForgeryManager::free_mods_list_entries() {
	mods_list->unselect_row();
	std::vector<Widget*> children = mods_list->get_children();
	for (Widget* widget : children) {
		ListBoxRow* row = (ListBoxRow*)widget;
		forgery_mod* mod = (forgery_mod*)row->get_data("mod");
		delete mod;
		mods_list->remove(*row);
		delete row;
	}
}

std::optional<forgery_mod*> read_mod_json(const fs::path& path) {
	fs::path mod_json = path/"mod.json";
	if (!fs::exists(mod_json))
		return std::nullopt;
	nlohmann::json json;
	try {
		std::ifstream file = std::ifstream(mod_json.string());
		file >> json;
		forgery_mod mod = json.get<forgery_mod>();
		return new forgery_mod(mod);
	}
	catch (const char* e) {
		return std::nullopt;
	}
}

std::string ForgeryManager::load_mod_listing() {
	std::variant<fs::path, std::string> maybe_save_directory =
		directories::get_nubby_save_directory(fs::path(steam_directory_entry->get_text()), isolate_save_check->get_active());
	
	if (std::holds_alternative<std::string>(maybe_save_directory)) {
		return std::get<std::string>(maybe_save_directory);
	}
	fs::path save_directory = std::get<fs::path>(maybe_save_directory);
	if (!fs::exists(save_directory/"mods"))
		return std::string("Mods folder does not exist");

	free_mods_list_entries();
	fs::path mods_directory = save_directory/"mods";
	for (const fs::directory_entry& entry : fs::directory_iterator(mods_directory)) {
		if (entry.is_directory()) {
			std::optional<forgery_mod*> maybe_mod = read_mod_json(entry.path());
			if (!maybe_mod.has_value())
				continue;
			forgery_mod* mod = *maybe_mod;

			ListBoxRow* row = new ListBoxRow();
			row->set_data("mod", mod);
			row->set_hexpand();
			Label* label = make_managed<Label>(mod->display_name);
			label->set_margin(10);
			row->set_child(*label);
			mods_list->append(*row);
		}
	}
	return std::string();
}

void ForgeryManager::update_mod_information(forgery_mod* mod) {
	this->mod_information->set_text(mod->display_name + "\n" + mod->description + "\n");
}

void ForgeryManager::browse_button_clicked(std::string title, Entry* entry, bool select_folder) {
	Glib::RefPtr<FileDialog> dialog = FileDialog::create();
	dialog->set_title(title);
	if (!select_folder) {
		dialog->open(*this, [this, dialog, entry](const Glib::RefPtr<Gio::AsyncResult> &result) {
			try {
				Glib::RefPtr<Gio::File> file = dialog->open_finish(result);
				Glib::ustring str = file->get_path();
				entry->set_text(str);
			}
			catch (Glib::Error &e) {}
		});
	}
	else {
		dialog->select_folder(*this, [this, dialog, entry](const Glib::RefPtr<Gio::AsyncResult> &result) {
			try {
				Glib::RefPtr<Gio::File> file = dialog->select_folder_finish(result);
				Glib::ustring str = file->get_path();
				entry->set_text(str);
			}
			catch (Glib::Error &e) {}
		});
	}
}

void ForgeryManager::apply_mods() {
	fs::path steam_directory = fs::path(this->steam_directory_entry->get_text());
	path_status steam_status = get_steam_directory_status(steam_directory);
	if (!steam_status.ok) {
		Glib::RefPtr<AlertDialog> dialog = AlertDialog::create("Steam directory is invalid:\n" + steam_status.text);
		dialog->show(*this);
		return;
	}

	fs::path umc_path = fs::path(umc_path_entry->get_text());
	path_status umc_status = file_path_exists_status(umc_path);
	if (!umc_status.ok) {
		Glib::RefPtr<AlertDialog> dialog = AlertDialog::create("UndertaleModCli path is invalid:\n" + umc_status.text);
		dialog->show(*this);
		return;
	}

	Patcher* patcher = make_managed<Patcher>();
	patcher->set_transient_for(*this);
	patcher->set_modal(true);
	patcher->present();
	//patcher->apply_mods();
}