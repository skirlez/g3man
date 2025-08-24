#pragma once

#include <gtkmm/window.h>
#include <gtkmm/label.h>

#include <filesystem>
#include <vector>
#include <string>

#include "forgery_mod.h"

using namespace Gtk;
namespace fs = std::filesystem;

class Patcher : public Window
{
public:
	Patcher();
	~Patcher() override;

	std::string apply_mods(std::vector<forgery_mod_entry*> mods, fs::path umc_path);
private:
	Label* thing_happening_now;
};