#include "forgery_mod.h"


related_mod related_mod_from_json(const nlohmann::json& json) {
	related_mod mod;
	json.at("mod_id").get_to(mod.mod_id);

	return mod;
}


forgery_mod forgery_mod_from_json(const nlohmann::json& json) {
	forgery_mod mod;
	json.at("mod_id").get_to(mod.mod_id);
	json.at("display_name").get_to(mod.display_name);
	json.at("description").get_to(mod.description);
	json.at("credits").get_to(mod.credits);
	json.at("version").get_to(mod.version);
	json.at("target_version").get_to(mod.target_version);
	json.at("patches_path").get_to(mod.patches_path);
	json.at("datafile_path").get_to(mod.datafile_path);
	for (const nlohmann::json& dependency : json.at("depends")) {
		mod.depends.push_back(related_mod_from_json(dependency));
	}
	for (const nlohmann::json& breaker : json.at("breaks")) {
		mod.breaks.push_back(related_mod_from_json(breaker));
	}
	return mod;
}