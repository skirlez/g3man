#include "directories.h"

namespace fs = std::filesystem;

fs::path try_guess_steam_folder() {
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

namespace directories {
	fs::path get_config_directory() {
		#ifdef _WIN32
			char* local_appdata = std::getenv("LOCALAPPDATA");
			if (!local_appdata) {
				throw std::runtime_error("LOCALAPPDATA isn't set. Please have it set.");
			}
			return fs::path(local_appdata)/"forgery-manager";
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

	fs::path try_guess_nubby_install_directory() {
		fs::path steam_folder = try_guess_steam_folder();
		if (steam_folder.empty())
			return steam_folder;
		fs::path guess = steam_folder/"steamapps"/"common"/"Nubby's Number Factory";
		if (fs::exists(guess))
			return guess;
		else
			return fs::path();
	}
}