#include "embeds.h"


namespace directories {
	#if __has_include("csx/merger.csx.h")
		#include "csx/merger.csx.h"
		std::string get_merger_script_text() {
			return std::string(reinterpret_cast<char*>(csx_merger_csx), csx_merger_csx_len);
		}
	#else
		std::string get_merger_script_text() {
			return "";
		}
	#endif

}