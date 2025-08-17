#pragma once
#include <string>
#include <vector>
#include "nlohmann/json.hpp"

struct forgery_mod {
	std::string mod_id;
	std::string display_name;
	std::string description;
	std::vector<std::string> credits;
	std::string version;
	unsigned int target_modloader_version;
};
NLOHMANN_DEFINE_TYPE_NON_INTRUSIVE(forgery_mod, mod_id, display_name, description, credits, version, target_modloader_version);