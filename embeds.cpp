#include "embeds.h"
#include <filesystem>


extern unsigned char _binary_csx_merger_csx_start[];
extern unsigned char _binary_csx_merger_csx_end[];
namespace fs = std::filesystem;


namespace embeds {
	std::string replace(std::string string, std::string old_substring, std::string new_substring) {
		size_t pos = string.find(old_substring);
		if (pos == std::string::npos)
			throw std::logic_error("Replace target doesn't exist");

		return string.replace(pos, old_substring.length(), new_substring);
	}
	
	std::string get_merger_script_text() {
		return std::string(reinterpret_cast<char*>(_binary_csx_merger_csx_start), 
			_binary_csx_merger_csx_end - _binary_csx_merger_csx_start);
	}
}