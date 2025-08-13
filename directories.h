#pragma once
#include <filesystem>

namespace fs = std::filesystem;
namespace directories {
	fs::path get_config_directory();
	fs::path get_or_create_config_directory();
	fs::path try_guess_nubby_install_directory();
}