#pragma once

#include <gtkmm/window.h>
#include <gtkmm/label.h>

#include <filesystem>
#include <vector>
#include <string>
#include <thread>
#include <atomic>

#include "forgery_mod.h"

using namespace Gtk;
namespace fs = std::filesystem;

class Patcher : public Window
{
public:
	Patcher();
	~Patcher() override;
	
	void apply_mods(const std::vector<forgery_mod_entry>& mods, const fs::path& nubby_install_directory, const fs::path& umc_path);
private:
	std::thread patcher_thread;
	std::atomic<bool> running {true};
	Label* thing_happening_now;
	void patch(std::vector<forgery_mod_entry> mods, fs::path nubby_install_directory, fs::path umc_path);
};