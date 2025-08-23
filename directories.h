#pragma once
#include <filesystem>
#include <variant>

namespace fs = std::filesystem;
namespace directories {
	std::variant<fs::path, std::string> get_local_appdata(const fs::path& steam_folder);
	fs::path get_config_directory();
	fs::path get_or_create_config_directory();
	fs::path try_guess_steam_directory();
	fs::path try_guess_nubby_install_directory();

	std::variant<fs::path, std::string> get_nubby_save_directory(const fs::path& steam_folder, bool isolated);
}