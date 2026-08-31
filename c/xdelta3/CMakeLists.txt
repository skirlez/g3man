cmake_minimum_required(VERSION 3.31)
project(xdelta3 VERSION 3.2.0)

include(CheckTypeSize)
check_type_size(size_t XD3_SIZEOF_SIZE_T)
check_type_size("unsigned long long" XD3_SIZEOF_UNSIGNED_LONG_LONG)

add_library(xdelta3 SHARED xdelta3.c)
target_include_directories(xdelta3 PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_directories(xdelta3 PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(xdelta3 ${XD3_TARGET_LIBS})

set(XD3_TARGET_LIBS "")

option( XD3_ENABLE_SECONDARY_COMPRESSION
								"Compiles builtin in support for secondary compression (DJW and FGK). \
								To also include lzma as secondary compression, you should enable it with the corresponding option"
								OFF)

option( XD3_ENABLE_LZMA
								"Includes lzma as secondary compression. \
								For this to work, libzma must be available to find_package when building
								This option has no effect if secondary compression is not enabled."
								OFF)

option( XD3_ENABLE_ENCODER
								"Enable also the encoding code, and not only the decoder."
								OFF)

option( XD3_ENABLE_VCDIFF_TOOLS
								"Enable additional vcdiff code (e.g., get xdelta patch's header information"
								OFF)

target_compile_definitions(xdelta3 PUBLIC SIZEOF_SIZE_T=${XD3_SIZEOF_SIZE_T})
target_compile_definitions(xdelta3 PUBLIC SIZEOF_UNSIGNED_LONG_LONG=${XD3_SIZEOF_UNSIGNED_LONG_LONG})
target_compile_definitions(xdelta3 PUBLIC XD3_USE_LARGEFILE64=1)
target_compile_definitions(xdelta3 PUBLIC XD3_MAIN=1) #enables main function
target_compile_definitions(xdelta3 PUBLIC EXTERNAL_COMPRESSION=0)
target_compile_definitions(xdelta3 PUBLIC XD3_USE_LARGESIZET=1)
if(WIN32)
	target_compile_definitions(xdelta3 PUBLIC XD3_WIN32=1) #this forces using the win32 io library
	target_compile_definitions(xdelta3 PUBLIC WIN32=1)

	# I don't know if this is correct and I hate cmake and windows
	target_compile_definitions(xdelta3 PUBLIC WINVER=0x0601) 
else()
	target_compile_definitions(xdelta3 PUBLIC XD3_WIN32=0)
	target_compile_definitions(xdelta3 PUBLIC WIN32=0)
	# here we are defaulting to using the stdio io library. If posix is need,
	# we must change  the following compile definition to use XD3_POSIX
	target_compile_definitions(xdelta3 PUBLIC XD3_STDIO=1)
endif()

target_compile_definitions(xdelta3 PUBLIC NOT_MAIN=1)
target_compile_definitions(xdelta3 PUBLIC XD3_ENCODER=0)

if(XD3_ENABLE_VCDIFF_TOOLS)
	target_compile_definitions(xdelta3 PUBLIC VCDIFF_TOOLS=1)
else()
	target_compile_definitions(xdelta3 PUBLIC VCDIFF_TOOLS=0)
endif()

if(XD3_ENABLE_SECONDARY_COMPRESSION)
	target_compile_definitions(xdelta3 PUBLIC SECONDARY_DJW=1) #(passing djwn chooses compression n with djw. Other second compressions do not seem to support level)
	target_compile_definitions(xdelta3 PUBLIC SECONDARY_FGK=1)
	if(XD3_ENABLE_LZMA)
		#find_package(LibLZMA 5.2.1 REQUIRED CONFIG)
		set(XD3_TARGET_LIBS liblzma ${XD3_TARGET_LIBS})
		target_compile_definitions(xdelta3 PUBLIC SECONDARY_LZMA=1)
	endif()
else()
	target_compile_definitions(xdelta3 PUBLIC SECONDARY_DJW=0)
	target_compile_definitions(xdelta3 PUBLIC SECONDARY_FGK=0)
	target_compile_definitions(xdelta3 PUBLIC SECONDARY_LZMA=0)
endif()
