#include "patcher.h"
#include "directories.h"

using namespace Gtk;
namespace fs = std::filesystem;

Patcher::Patcher() {
	thing_happening_now = make_managed<Label>();
	set_child(*thing_happening_now);
	set_size_request(300, 160);
	set_resizable(false);
}

Patcher::~Patcher() {}


std::string Patcher::apply_mods(fs::path steam_directory, fs::path umc_path) {
	return "";
}



void patch() {

}