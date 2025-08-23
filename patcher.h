#pragma once

#include <string>
#include <gtkmm/window.h>
#include <gtkmm/label.h>

#include <filesystem>

using namespace Gtk;
namespace fs = std::filesystem;

class Patcher : public Window
{
public:
	Patcher();
	~Patcher() override;

	std::string apply_mods(fs::path steam_directory, fs::path umc_path);
private:
	Label* thing_happening_now;
};