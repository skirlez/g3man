#include "embeds.h"
#include <filesystem>

extern unsigned char _binary_gmlp_superpatch_gmlp_start[];
extern unsigned char _binary_gmlp_superpatch_gmlp_end[];

extern unsigned char _binary_csx_merger_csx_start[];
extern unsigned char _binary_csx_merger_csx_end[];

extern unsigned char _binary_win_forgery_win_start[];
extern unsigned char _binary_win_forgery_win_end[];

namespace fs = std::filesystem;


namespace embeds {

	std::string get_modloader_superpatch_text() {
		return std::string(reinterpret_cast<char*>(_binary_gmlp_superpatch_gmlp_start), 
			_binary_gmlp_superpatch_gmlp_end - _binary_gmlp_superpatch_gmlp_start);
		return "";
	}

	std::string replace(std::string string, std::string old_substring, std::string new_substring) {
		size_t pos = string.find(old_substring);
		if (pos == std::string::npos)
			throw std::logic_error("Replace target doesn't exist");

		return string.replace(pos, old_substring.length(), new_substring);
	}

	std::string get_merger_script_text() {
		std::string text = std::string(reinterpret_cast<char*>(_binary_csx_merger_csx_start), 
			_binary_csx_merger_csx_end - _binary_csx_merger_csx_start);

		return replace(text, "REPLACE_MODLOADER_PATCHES", get_modloader_superpatch_text());
	}

	const char* get_modloader_data_start() {
		return reinterpret_cast<const char*>(_binary_win_forgery_win_start);
	}

	size_t get_modloader_data_size() {
		return _binary_win_forgery_win_end - _binary_win_forgery_win_start;
	}

}