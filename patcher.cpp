#include <fstream>
#include <glibmm/main.h>


#ifdef __linux__
	#include <unistd.h>
	#include <sys/wait.h>
#endif


#include "patcher.h"
#include "directories.h"
#include "embeds.h"

using namespace Gtk;
namespace fs = std::filesystem;

Patcher::Patcher() {
	thing_happening_now = make_managed<Label>();
	set_child(*thing_happening_now);
	set_size_request(300, 160);
	set_resizable(false);


	signal_close_request().connect([this]() {
		if (this->running)
			return true;
		return false;
	}, true);
}

Patcher::~Patcher() {}


bool create_file(const fs::path& path, const char* data, size_t size) {
	std::ofstream out = std::ofstream(path, std::ios::binary);
	if (!out)
		return false;
	out.write(data, size);
	out.close();
	if (!out)
		return false;
	return true;
}


void Patcher::patch(std::vector<forgery_mod_entry> mods, fs::path nubby_install_directory, fs::path umc_path) {
	fs::path temp = std::filesystem::temp_directory_path()/"forgery-manager";
	Glib::signal_idle().connect_once([this]() {
		this->thing_happening_now->set_text("Creating temporary files...");
	});

	if (!fs::exists(temp) && !fs::create_directories(temp))
		return;

	if (!create_file(temp/"forgery.win", embeds::get_modloader_data_start(), embeds::get_modloader_data_size()))
		return;
	fs::path modloader_patches_directory = temp/"modloader_patches";
	if (!fs::exists(modloader_patches_directory) && !fs::create_directories(modloader_patches_directory))
		return;
	std::string superpatch = embeds::get_modloader_superpatch_text();
	if (!create_file(temp/"modloader_patches"/"superpatch.gmlp", superpatch.data(), superpatch.size()))
		return;
	std::string merger_script = embeds::get_merger_script_text();
	if (!create_file(temp/"merger.csx", merger_script.data(), merger_script.size()))
		return;
	
	fs::path clean_data_path = nubby_install_directory/"clean_forgery_data.win";
	fs::path nubby_data_path = nubby_install_directory/"data.win";
	if (!fs::exists(clean_data_path)) {
		if (!fs::copy_file(nubby_data_path, clean_data_path))
			return;
	}
	
	Glib::signal_idle().connect_once([this]() {
		this->thing_happening_now->set_text("Patching using UndertaleModCli...");
	});

	std::string patch_directores = modloader_patches_directory.string();
	for (forgery_mod_entry entry : mods) {
		patch_directores += ":" + (entry.path/entry.mod.patches_path).string();
	}

	// no need for ICU
	g_setenv("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1", true);

	g_setenv("FORGERYMANAGER_PATCH_DIRECTORIES", patch_directores.c_str(), false);
	g_setenv("FORGERYMANAGER_FORGERY_DATA_PATH", (temp/"forgery.win").c_str(), false);

	std::vector<std::string> command = {
		umc_path.string(),
		"load",
		clean_data_path.string(),
		"--scripts",
		(temp / "merger.csx").string(),
		"--output",
		nubby_data_path.string()
	};

	#ifdef __linux__
		std::vector<char*> argv;
		for (std::string& command : command) 
			argv.push_back(&command[0]);
		argv.push_back(nullptr);

		pid_t pid = fork();
		if (pid == 0) {
			execv(argv[0], argv.data());
			_exit(127);
		} 
		else if (pid > 0) {
			int status;
			waitpid(pid, &status, 0);
			if (WIFEXITED(status) && WEXITSTATUS(status) == 0) {
				Glib::signal_idle().connect_once([this]() {
					this->thing_happening_now->set_text("Done!");
				});
			}
			else {
				Glib::signal_idle().connect_once([this]() {
					this->thing_happening_now->set_text("Something went wrong while calling UndertaleModCli");
				});
			}
		} 
		else {
			Glib::signal_idle().connect_once([this]() {
				this->thing_happening_now->set_text("Something went wrong while calling UndertaleModCli");
			});
		}
	#else
		g_message("Calling UndertaleModCli not implemented on this OS");
	#endif

	g_message("Done");
	this->running = false;
}

void Patcher::apply_mods(const std::vector<forgery_mod_entry>& mods, const fs::path& nubby_install_directory, const fs::path& umc_path) {
	patcher_thread = std::thread(&Patcher::patch, this, mods, nubby_install_directory, umc_path);
	patcher_thread.detach();
}
