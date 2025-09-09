#pragma once
#include <filesystem>
#include <variant>

namespace fs = std::filesystem;


namespace zips {
	bool extract_and_overwrite(fs::path file, fs::path out);
}