#include "forgerymanager.h"
#include <gtkmm/application.h>

int main(int argc, char* argv[]) {
	#ifdef _WIN32
		// force Cairo (fixes black borders around the window on windows. not sure why this happens)
		g_setenv("GSK_RENDERER", "cairo", TRUE);
	#endif
	auto app = Gtk::Application::create("com.skirlez.forgery_manager");
	return app->make_window_and_run<ForgeryManager>(argc, argv);
}