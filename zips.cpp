#include <filesystem>
#include <variant>
#include <zip.h>
#include <cstring>
#include <fstream>
#include <glib-2.0/glib.h>

#include "directories.h"
#include "zips.h"
/*
bool path_starts_with(fs::path path, fs::path other_path) {
	fs::path::iterator it = path.begin();
	fs::path::iterator other_it = other_path.begin();
	while (it != path.end()) {
		if (*it != *other_it)
			return false;
		it++;
		other_it++;
	}
	return other_it == other_path.end();
}
*/

bool is_wrapped_needed(zip_t* archive) {
	zip_int64_t num_entries = zip_get_num_entries(archive, 0);
	for (zip_int64_t i = 0; i < num_entries; i++) {
		const char* name = zip_get_name(archive, i, 0);
		if (!name)
			continue;
		if (strcmp(name, "mod.json") == 0) {
			return true;
		}
	}
	return false;
}

namespace zips {
	/**
	 * Extracts all files from the zip found at `file` to `out`, overwriting what's there.
	 * Returns `true` if and only if the zip successfully extracted in its entirety.
	 * This function cannot produce an incomplete extraction, as it first extracts to a temporary location. 
	 * */


	bool extract_and_overwrite(fs::path file, fs::path out) {
		std::error_code ignore;

		int err = 0;
		zip_t* archive = zip_open(file.c_str(), ZIP_RDONLY, &err);
		if (!archive) {
			return false;
		}

		fs::path temp_out = fs::temp_directory_path() / "forgerymanager_copy_mods";
		fs::remove_all(temp_out, ignore);
		if (!fs::create_directories(temp_out, ignore))
			return false;

		bool wrapper_needed = is_wrapped_needed(archive);
		if (wrapper_needed) {
			temp_out /= file.stem();
			out /= file.stem();
		}

		if (!fs::exists(temp_out, ignore) && !fs::create_directories(temp_out, ignore))
			return false;

		zip_int64_t num_entries = zip_get_num_entries(archive, 0);
		for (zip_int64_t i = 0; i < num_entries; i++) {
			const char* name = zip_get_name(archive, i, 0);
			if (!name)
				continue;
			if (name[strlen(name) - 1] == '/')
				continue; // directory entry, don't care, we infer these from files
			fs::path entry_out = temp_out / name;
			g_message("%s", entry_out.c_str());

			fs::path parent_directory = entry_out.parent_path();
			if (!fs::exists(parent_directory, ignore) && !fs::create_directories(parent_directory, ignore)) {
				return false;
			}
			std::ofstream output_stream = std::ofstream(entry_out, std::ios::binary); 
			if (!output_stream)
				return false;

			zip_file_t* file = zip_fopen_index(archive, i, 0);
			if (!file)
				return false;

			char buffer[4096];
			zip_int64_t bytes_read;
			while ((bytes_read = zip_fread(file, buffer, sizeof(buffer))) > 0) {
				if (bytes_read == -1) {
					zip_fclose(file);
					return false;
				}
				output_stream.write(buffer, bytes_read);
			}

			if (zip_fclose(file) != 0)
				return false;
		}
		
		if (zip_close(archive) != 0)
			return false;

		fs::copy(temp_out, out, fs::copy_options::recursive|fs::copy_options::overwrite_existing, ignore);
		
		return true;
	}
}