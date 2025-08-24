#pragma once
#include <string>
#include <vector>
#include "nlohmann/json.hpp"

#include <filesystem>

struct forgery_mod {
	std::string mod_id;
	std::string display_name;
	std::string description;
	std::vector<std::string> credits;
	std::string version;
	unsigned int target_modloader_version;

	std::string patches_path;
};

struct forgery_mod_entry {
	forgery_mod mod;
	std::filesystem::path path;
};

NLOHMANN_DEFINE_TYPE_NON_INTRUSIVE(forgery_mod, mod_id, display_name, description, credits, version, target_modloader_version);