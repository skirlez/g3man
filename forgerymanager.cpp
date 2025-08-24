#include "forgerymanager.h"
#include <iostream>
#include <filesystem>
#include <fstream>
#include <optional>

#include <gtkmm/alertdialog.h>
#include <gtkmm/stacksidebar.h>
#include "nlohmann/json.hpp"

#include "patcher.h"
#include "directories.h"
#include "embeds.h"


using namespace Gtk;
namespace fs = std::filesystem;

ForgeryManager::ForgeryManager() {
	mods_page = make_managed<Box>(Orientation::VERTICAL, 0);	
	settings_page = make_managed<Box>(Orientation::VERTICAL, 0);
	Box* about_page = make_managed<Box>(Orientation::VERTICAL, 0);

	std::array<Widget*, 3> pages = {mods_page, settings_page, about_page};
	std::array<std::string, 3> page_names = {"mods", "settings", "about"};
	std::array<std::string, 3> page_titles = {"Mods", "Settings", "About"};
	page_stack = make_managed<Stack>();
	page_stack->set_hexpand();

	StackSidebar* page_sidebar = make_managed<StackSidebar>();
	page_sidebar->set_stack(*page_stack);
	page_sidebar->set_valign(Align::FILL);
	page_sidebar->set_vexpand(true);

	for (size_t i = 0; i < page_names.size(); i++) {
		page_stack->add(*pages[i], page_names[i], page_titles[i]);
	}

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
		this->create_mods_directory_and_load_listing();
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
			this->update_mod_information((forgery_mod_entry*)row->get_data("mod_entry"));
	});
	
	mods_list->set_hexpand();

	mods_page->set_hexpand();
	mods_page->set_vexpand();

	mods_page->append(*mods_list);
	mods_page->append(*manage_mods_box);
	
	mods_page->append(*bottom_box);
	mods_page->set_homogeneous(false);

	Label* nubby_directory_label = make_managed<Label>("Nubby install directory");
	nubby_directory_label->set_halign(Align::START);

	nubby_directory_entry = make_managed<Entry>();
	nubby_directory_entry->signal_changed().connect([this]() {
		this->update_nubby_directory_label();
	});
	nubby_directory_entry->set_halign(Align::START);
	nubby_directory_entry->set_max_width_chars(75);

	Button* nubby_directory_browse = make_managed<Button>("Browse");
	nubby_directory_browse->set_margin_end(10);
	nubby_directory_browse->signal_clicked().connect([this]() {
		this->browse_button_clicked("Select the Nubby folder", this->nubby_directory_entry, true);
	});

	Box* nubby_browse_and_directory_box = make_managed<Box>(Orientation::HORIZONTAL);
	nubby_browse_and_directory_box->append(*nubby_directory_browse);
	nubby_browse_and_directory_box->append(*nubby_directory_entry);

	nubby_directory_status_label = make_managed<Label>("");
	nubby_directory_status_label->set_halign(Align::START);

	Box* nubby_directory_box = make_managed<Box>(Orientation::VERTICAL);
	nubby_directory_box->append(*nubby_directory_label);
	nubby_directory_box->append(*nubby_browse_and_directory_box);
	nubby_directory_box->append(*nubby_directory_status_label);
	nubby_directory_box->set_margin(10);

	#ifdef REQUIRE_WINEPREFIX
		Label* nubby_wineprefix_label = make_managed<Label>("Nubby wineprefix location");
		nubby_wineprefix_label->set_halign(Align::START);

		nubby_wineprefix_entry = make_managed<Entry>();
		nubby_wineprefix_entry->signal_changed().connect([this]() {
			this->update_nubby_directory_label();
		});
		nubby_wineprefix_entry->set_halign(Align::START);
		nubby_wineprefix_entry->set_max_width_chars(75);

		Button* nubby_wineprefix_browse = make_managed<Button>("Browse");
		nubby_wineprefix_browse->set_margin_end(10);
		nubby_wineprefix_browse->signal_clicked().connect([this]() {
			this->browse_button_clicked("Select the Wineprefix with Nubby's save data", this->nubby_wineprefix_entry, true);
		});

		Box* nubby_browse_and_wineprefix_box = make_managed<Box>(Orientation::HORIZONTAL);
		nubby_browse_and_wineprefix_box->append(*nubby_wineprefix_browse);
		nubby_browse_and_wineprefix_box->append(*nubby_wineprefix_entry);

		nubby_wineprefix_status_label = make_managed<Label>("");
		nubby_wineprefix_status_label->set_halign(Align::START);

		Box* nubby_wineprefix_box = make_managed<Box>(Orientation::VERTICAL);
		nubby_wineprefix_box->append(*nubby_wineprefix_label);
		nubby_wineprefix_box->append(*nubby_browse_and_wineprefix_box);
		nubby_wineprefix_box->append(*nubby_wineprefix_status_label);
		nubby_wineprefix_box->set_margin(10);
	#endif

	#ifndef FORGERYMANAGER_UMC_PATH
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
	#endif

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

	settings_page->append(*nubby_directory_box);
	#ifdef REQUIRE_WINEPREFIX
		settings_page->append(*nubby_wineprefix_box);
	#endif
	#ifndef FORGERYMANAGER_UMC_PATH
		settings_page->append(*umc_path_box);
	#endif
	settings_page->append(*isolate_save_box);
	//settings_page->append(*theme_box);
	settings_page->append(*save_settings_button);
	settings_page->set_margin(20);

	Label* about_label = make_managed<Label>("Forgery Manager v0");
	about_page->append(*about_label);
	about_page->set_valign(Align::CENTER);


	page_box = make_managed<Box>(Orientation::HORIZONTAL, 0);
	page_box->append(*page_sidebar);
	page_box->append(*page_stack);
	page_box->set_vexpand(true);

	page_box->set_homogeneous(false);
	page_stack->set_visible_child(*mods_page);
	set_child(*page_box);

	load_settings();
	create_mods_directory_and_load_listing();

	signal_hide().connect([this]() {
		on_exit();
	});
}

ForgeryManager::~ForgeryManager() {}

void ForgeryManager::switch_page(Widget* page) {
	this->page_stack->set_visible_child(*page);
}

void ForgeryManager::on_exit() {
	free_mods_list_entries();
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

path_status get_nubby_directory_status(fs::path path) {
	if (!fs::exists(path/"data.win"))
		return {false, "Could not find data.win"};
	if (!fs::exists(path/"NNF_FULLVERSION.exe"))
		return {false, "Could not find NNF_FULLVERSION.exe"};
	return {true, "Nubby game files found"};
}

path_status get_nubby_wineprefix_status(fs::path path) {
	if (!fs::exists(path/"data.win"))
		return {false, "Could not find data.win"};
	if (!fs::exists(path/"NNF_FULLVERSION.exe"))
		return {false, "Could not find NNF_FULLVERSION.exe"};
	return {true, "Nubby game files found"};
}

void ForgeryManager::update_nubby_directory_label() {
	std::string nubby_directory = nubby_directory_entry->get_text();
	if (nubby_directory.empty()) {
		nubby_directory_status_label->set_text("");
		return;
	}
	fs::path path = nubby_directory;

	path_status status = get_nubby_directory_status(path);
	nubby_directory_status_label->set_text(status.text);
}


#ifdef REQUIRE_WINEPREFIX
	void ForgeryManager::update_nubby_wineprefix_label() {
		std::string wineprefix_path = nubby_wineprefix_entry->get_text();
		if (wineprefix_path.empty()) {
			nubby_wineprefix_status_label->set_text("");
			return;
		}
		fs::path path = wineprefix_path;

		path_status status = get_nubby_directory_status(path);
		nubby_directory_status_label->set_text(status.text);
	}
#endif



void ForgeryManager::save_settings() {
	fs::path config_file_path = directories::get_or_create_config_directory()/"settings.json";

	nlohmann::json json = {
		{"nubby_install_directory", this->nubby_directory_entry->get_text()},

		#ifndef FORGERYMANAGER_UMC_PATH
		{"umc_path", this->umc_path_entry->get_text()},
		#endif

		#if REQUIRE_WINEPREFIX
		{"wineprefix_directory", this->nubby_wineprefix_entry->get_text()},
		#endif

		{"isolate_save", this->isolate_save_check->get_active()}
	};

	std::ofstream file = std::ofstream(config_file_path.string());
	file << json.dump();
}

void ForgeryManager::load_settings() {
	fs::path config_file_path = directories::get_config_directory()/"settings.json";
	nlohmann::json json = nlohmann::json::object();

	if (fs::exists(config_file_path)) {
		std::ifstream file = std::ifstream(config_file_path.string());
		file >> json;
	}
	this->nubby_directory_entry->set_text(json.value("nubby_install_directory", directories::try_guess_nubby_install_directory().string()));

	#ifndef FORGERYMANAGER_UMC_PATH
	this->umc_path_entry->set_text(json.value("umc_path", ""));
	#endif

	#if REQUIRE_WINEPREFIX
		this->nubby_wineprefix_entry->set_text(json.value("wineprefix_directory", directories::try_guess_nubby_wineprefix_directory().string()));
	#endif

	this->isolate_save_check->set_active(json.value("isolate_save", true));
}




void ForgeryManager::free_mods_list_entries() {
	mods_list->unselect_row();
	std::vector<Widget*> children = mods_list->get_children();
	for (Widget* widget : children) {
		ListBoxRow* row = (ListBoxRow*)widget;
		forgery_mod_entry* mod = (forgery_mod_entry*)row->get_data("mod_entry");
		delete mod;
		mods_list->remove(*row);
		delete row;
	}
}

std::optional<forgery_mod> read_mod_json(const fs::path& path) {
	fs::path mod_json = path/"mod.json";
	if (!fs::exists(mod_json))
		return std::nullopt;
	nlohmann::json json;
	try {
		std::ifstream file = std::ifstream(mod_json.string());
		file >> json;
		forgery_mod mod = json.get<forgery_mod>();
		return mod;
	}
	catch (const char* e) {
		return std::nullopt;
	}
}

void ForgeryManager::create_mods_directory_and_load_listing() {
	fs::path nubby_directory = fs::path(nubby_directory_entry->get_text());
	path_status status = get_nubby_directory_status(nubby_directory);
	if (!status.ok) {
		return;
	}
	fs::path mods_directory = nubby_directory/"mods";
	if (!fs::exists(mods_directory) && !fs::create_directory(mods_directory)) {
		return;
	}
	free_mods_list_entries();
	for (const fs::directory_entry& entry : fs::directory_iterator(mods_directory)) {
		if (entry.is_directory()) {
			fs::path mod_directory = entry.path();
			std::optional<forgery_mod> maybe_mod = read_mod_json(mod_directory);
			if (!maybe_mod.has_value())
				continue;
			forgery_mod mod = *maybe_mod;

			forgery_mod_entry* mod_entry = new forgery_mod_entry{mod, fs::path(mod_directory)};

			ListBoxRow* row = new ListBoxRow();
			row->set_data("mod_entry", mod_entry);
			row->set_hexpand();
			Label* label = make_managed<Label>(mod_entry->mod.display_name);
			label->set_margin(10);
			row->set_child(*label);
			mods_list->append(*row);
		}
	}
}

void ForgeryManager::update_mod_information(forgery_mod_entry* mod_entry) {
	forgery_mod& mod = mod_entry->mod;
	this->mod_information->set_text(mod.display_name + "\n" + mod.description + "\n");
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
	fs::path nubby_directory = fs::path(this->nubby_directory_entry->get_text());
	path_status nubby_status = get_nubby_directory_status(nubby_directory);
	if (!nubby_status.ok) {
		Glib::RefPtr<AlertDialog> dialog = AlertDialog::create("Nubby install directory is invalid:\n" + nubby_status.text);
		dialog->show(*this);
		return;
	}

	#ifndef FORGERYMANAGER_UMC_PATH
		fs::path umc_path = fs::path(umc_path_entry->get_text());
		path_status umc_status = file_path_exists_status(umc_path);
		if (!umc_status.ok) {
			Glib::RefPtr<AlertDialog> dialog = AlertDialog::create("UndertaleModCli path is invalid:\n" + umc_status.text);
			dialog->show(*this);
			return;
		}
	#else
		fs::path umc_path = fs::path(FORGERYMANAGER_UMC_PATH);
	#endif

	Patcher* patcher = make_managed<Patcher>();
	patcher->set_transient_for(*this);
	patcher->set_modal(true);
	patcher->present();

	g_message("%s", umc_path.string().c_str());

	std::vector<forgery_mod_entry*> mod_entries;
	std::string error = patcher->apply_mods(mod_entries, umc_path);
	g_message("%s", error.c_str());
	//patcher->apply_mods();
}