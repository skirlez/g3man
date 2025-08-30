#include "directories.h"


namespace fs = std::filesystem;


namespace directories {
	fs::path get_config_directory() {
		#if defined(__linux__)
			const char* xdg = std::getenv("XDG_CONFIG_HOME");
			if (!xdg) {
				const char* home = std::getenv("HOME");
				if (!home) {
					throw std::runtime_error("Both XDG_CONFIG_HOME and HOME are not set, cannot save config. Please have any of them set.");
				}
				return fs::path(home)/".config"/"forgery-manager";
			}
			return fs::path(xdg)/"forgery-manager";
		#elif defined(_WIN32)
			char* local_appdata = std::getenv("LOCALAPPDATA");
			if (!local_appdata) {
				throw std::runtime_error("LOCALAPPDATA isn't set. Please have it set.");
			}
			return fs::path(local_appdata)/"forgery-manager"
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
		#if defined(__linux__)
			const char* home = std::getenv("HOME");
			if (!home)
				return fs::path();
			fs::path symlink = fs::path(home)/".steam"/"steam";
			if (fs::exists(symlink) && fs::is_directory(symlink)) {
				fs::path resolved_path = fs::canonical(symlink);
				return resolved_path;
			}
			return fs::path();
		#elif defined(_WIN32)
			// TODO
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

	#ifdef REQUIRE_WINEPREFIX
		fs::path try_guess_nubby_wineprefix_directory() {
			fs::path steam_folder = try_guess_steam_directory();
			if (steam_folder.empty())
				return steam_folder;
			fs::path guess = steam_folder/"steamapps"/"compatdata"/"3191030"/"pfx";
			if (!fs::exists(guess))
				return fs::path();
			if (!fs::exists(guess/"drive_c"/"users"/"steamuser"/"AppData"/"Local"))
				return fs::path();
			return guess;
		}
	#endif

	/*
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
	*/
}