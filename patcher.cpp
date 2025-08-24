#include "patcher.h"
#include "directories.h"
#include "embeds.h"

#include <fstream>

using namespace Gtk;
namespace fs = std::filesystem;

Patcher::Patcher() {
	thing_happening_now = make_managed<Label>();
	set_child(*thing_happening_now);
	set_size_request(300, 160);
	set_resizable(false);
}

Patcher::~Patcher() {}


bool create_file(const fs::path& path, const char* data, size_t size) {
	std::ofstream out = std::ofstream(path, std::ios::binary);
	if (!out)
		return false;
	out.write(data, size);
	out.close();
	if (!out)
		return false;
	return true;
}

std::string Patcher::apply_mods(std::vector<forgery_mod_entry*> mods, fs::path umc_path) {
	fs::path temp = std::filesystem::temp_directory_path()/"forgery-manager";
	std::string directories_error = "Could not create temporary files!";
	fs::create_directories(temp);
	if (!fs::exists(temp))
		return directories_error;
	if (!create_file(temp/"forgery.win", embeds::get_modloader_data_start(), embeds::get_modloader_data_size()))
		return directories_error;
	std::string merger_script = embeds::get_merger_script_text();
	if (!create_file(temp/"merger.csx", merger_script.data(), merger_script.size()))
		return directories_error;

	return "";
}



void patch() {

}