#pragma once

#include <gtkmm/window.h>
#include <gtkmm/label.h>
#include <gtkmm/button.h>

#include <filesystem>
#include <vector>
#include <string>
#include <thread>
#include <atomic>

#include "forgery_mod.h"


#ifdef __linux__
	#include <unistd.h>
#endif

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
	Button* force_stop_button;
	bool killed_by_us = false;	

	void patch(std::vector<forgery_mod_entry> mods, fs::path nubby_install_directory, fs::path umc_path);



	#ifdef __linux__
		pid_t undertalemodcli_pid;
	#endif

};