#include "directories.h"


namespace fs = std::filesystem;


namespace directories {
	/** 
	 * On Windows, this function returns LOCALAPPDATA. 
	 * On Linux, this will get the LOCALAPPDATA from Nubby's wineprefix from Steam.
	 * This is why it requires steam_folder, but my hope is it will be optimized out on Windows */
	std::variant<fs::path, std::string> get_local_appdata(const fs::path& steam_folder) {
		#ifdef _WIN32
			char* local_appdata = std::getenv("LOCALAPPDATA");
			if (!local_appdata) {
				throw std::runtime_error("LOCALAPPDATA isn't set. Please have it set.");
			}
			return fs::path(local_appdata);
		#elif __linux__
			if (!fs::exists(steam_folder/"steamapps"/"compatdata")) {
				return std::string("Steam folder is invalid: steamapps/compatdata not found");
			}
			fs::path compatdata = steam_folder/"steamapps"/"compatdata";
			if (!fs::exists(compatdata/"3191030")) { 
				return std::string("Nubby wineprefix not found in Steam folder - please select the Steam folder that contains Nubby, or run the game once if you haven't");
			}
			fs::path local_appdata = compatdata/"3191030"/"pfx"/"drive_c"/"users"/"steamuser"/"AppData"/"Local";
			if (!fs::exists(local_appdata)) {
				return std::string("Nubby wineprefix is invalid. Please create an issue on the forgery-manager GitHub");
			}
			return local_appdata;
		#else
			throw std::logic_error("Critical code not written for this OS yet");
		#endif
	}

	fs::path get_config_directory() {
		#ifdef _WIN32
			return get_local_appdata()/"forgery-manager";
		#elif __linux__
			const char* xdg = std::getenv("XDG_CONFIG_HOME");
			if (!xdg) {
				const char* home = std::getenv("HOME");
				if (!home) {
					throw std::runtime_error("Both XDG_CONFIG_HOME and HOME are not set, cannot save config. Please have any of them set.");
				}
				return fs::path(home)/".config"/"forgery-manager";
			}
			return fs::path(xdg)/"forgery-manager";
		#else
			throw std::logic_error("Critical code not written for this OS yet");
		#endif
	}

	fs::path get_or_create_config_directory() {
		fs::path config_path = get_config_directory();
		fs::create_directories(config_path);
		return config_path;
	}

	fs::path try_guess_steam_directory() {
		#ifdef _WIN32
			// TODO
		#elif __linux__
			const char* home = std::getenv("HOME");
			if (!home)
				return fs::path();
			fs::path symlink = fs::path(home)/".steam"/"steam";
			if (fs::exists(symlink) && fs::is_directory(symlink)) {
				fs::path resolved_path = fs::canonical(symlink);
				return resolved_path;
			}
			return fs::path();
		#else
			g_message("%s", "If you're seeing this, you should probably implement this function for this OS")
		#endif
	}


	fs::path try_guess_nubby_install_directory() {
		fs::path steam_folder = try_guess_steam_directory();
		if (steam_folder.empty())
			return steam_folder;
		fs::path guess = steam_folder/"steamapps"/"common"/"Nubby's Number Factory";
		if (fs::exists(guess))
			return guess;
		else
			return fs::path();
	}

	std::variant<fs::path, std::string> get_nubby_save_directory(const fs::path& steam_folder, bool isolated) {
		std::variant<fs::path, std::string> maybe_local_appdata = get_local_appdata(steam_folder);
		if (std::holds_alternative<std::string>(maybe_local_appdata)) {
			return maybe_local_appdata;
		}
		
		fs::path local_appdata = std::get<fs::path>(maybe_local_appdata);
		if (!fs::exists(local_appdata/"NNF_FULLVERSION")) {
			return std::string("Couldn't find save file. If you have saved before, please open an issue on the forgery-manager GitHub");
		}
		if (isolated) {
			fs::path save_directory = local_appdata/"nubbys_forgery";
			if (!fs::exists(save_directory))
				fs::create_directory(save_directory);
			return save_directory;
		}
		return local_appdata/"NNF_FULLVERSION";
	}
}