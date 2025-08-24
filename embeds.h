#pragma once
#include <string>

namespace embeds {
	std::string get_merger_script_text();
	std::string get_modloader_superpatch_text();
	const char* get_modloader_data_start();
	size_t get_modloader_data_size();
}