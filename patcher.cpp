#include <fstream>

#include <glibmm/main.h>
#include <gtkmm/box.h>

#ifdef __linux__
	#include <sys/wait.h>
#endif


#include "patcher.h"
#include "directories.h"
#include "embeds.h"

using namespace Gtk;
namespace fs = std::filesystem;

Patcher::Patcher() {
	set_size_request(300, 160);
	set_resizable(false);

	thing_happening_now = make_managed<Label>();
	thing_happening_now->set_margin(20);	



	force_stop_button = make_managed<Button>("KILL");
	force_stop_button->set_sensitive(false);
	force_stop_button->set_margin(20);
	force_stop_button->signal_clicked().connect([this]() {
		// there's probably some race condition bs here but worst case scenario it 
		// displays a wrong message if you time pressing this button just right, so who cares
		killed_by_us = true;

		#ifdef __linux__
			kill(undertalemodcli_pid, SIGTERM);
		#endif

		thing_happening_now->set_text("UndertaleModCli process was killed");
		force_stop_button->set_sensitive(false);
	});

	Box* box = make_managed<Box>(Orientation::VERTICAL);
	box->append(*thing_happening_now);
	box->append(*force_stop_button);

	box->set_valign(Align::CENTER);
	

	set_child(*box);

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
	fs::path temp = std::filesystem::temp_directory_path()/"forgerymanager_patching";
	Glib::signal_idle().connect_once([this]() {
		this->thing_happening_now->set_text("Creating temporary files...");
	});

	if (!fs::exists(temp) && !fs::create_directories(temp))
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
	
	// TODO
	std::string patch_directores = (mods[0].path/mods[0].mod.patches_path).string();
	std::string data_directores = (mods[0].path/mods[0].mod.datafile_path).string();
	for (size_t i = 1; i < mods.size(); i++) {
		forgery_mod_entry& entry = mods[i];
		if (!entry.mod.patches_path.empty())
			patch_directores += ":" + (entry.path/entry.mod.patches_path).string();
		if (!entry.mod.datafile_path.empty())
			data_directores += ":" + (entry.path/entry.mod.datafile_path).string();
	}	



	g_setenv("FORGERYMANAGER_PATCH_DIRECTORIES", patch_directores.c_str(), false);
	g_setenv("FORGERYMANAGER_DATA_PATHS", data_directores.c_str(), false);

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
		// no need for ICU
		g_setenv("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1", true);

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
			undertalemodcli_pid = pid;
			force_stop_button->set_sensitive(true);

			int status;
			waitpid(pid, &status, 0);
			force_stop_button->set_sensitive(false);
			if (killed_by_us) {

			}
			else if (WIFEXITED(status) && WEXITSTATUS(status) == 0) {
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
