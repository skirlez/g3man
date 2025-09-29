#pragma once
#include <string>
#include <vector>
#include "nlohmann/json.hpp"

#include <filesystem>

struct related_mod {
	std::string mod_id;
};

struct forgery_mod {
	std::string mod_id;
	std::string display_name;
	std::string description;
	std::vector<std::string> credits;
	std::string version;
	std::string target_version;
	std::vector<related_mod> depends;
	std::vector<related_mod> breaks;

	std::string patches_path;
	std::string datafile_path;
};

struct forgery_mod_entry {
	forgery_mod mod;
	std::filesystem::path path;
};

//NLOHMANN_DEFINE_TYPE_NON_INTRUSIVE(forgery_mod, mod_id, display_name, description, credits, version, target_modloader_version);



forgery_mod forgery_mod_from_json(const nlohmann::json& json);