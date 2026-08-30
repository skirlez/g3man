cmake_minimum_required(VERSION 3.31)
project(libg3man VERSION 1.0.0)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

add_subdirectory(xdelta3)

set_target_properties(xdelta3 PROPERTIES
		LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}")

set(USE_HTTP OFF CACHE BOOL "" FORCE)
set(USE_HTTPS OFF CACHE BOOL "" FORCE)
set(USE_SSH OFF CACHE BOOL "" FORCE)
set(BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(BUILD_CLI OFF CACHE BOOL "" FORCE)
set(USE_NTLMCLIENT OFF CACHE BOOL "" FORCE)
set(SONAME OFF CACHE BOOL "" FORCE)
# we need some internal libgit2 functions
set(CMAKE_C_VISIBILITY_PRESET default)
add_subdirectory(libgit2)
unset(CMAKE_C_VISIBILITY_PRESET)

set_target_properties(libgit2package PROPERTIES
		LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
		OUTPUT_NAME "git2")
		
add_library(g3man SHARED g3man.c)
set_property(TARGET g3man PROPERTY C_STANDARD 11)
target_include_directories(g3man PRIVATE 
	${CMAKE_CURRENT_SOURCE_DIR}/libgit2/include
	${CMAKE_CURRENT_SOURCE_DIR}/libgit2/src
	${CMAKE_CURRENT_SOURCE_DIR}/libgit2/src/util)
target_link_libraries(g3man	PUBLIC xdelta3 libgit2package)

set_target_properties(g3man PROPERTIES BUILD_RPATH "$ORIGIN")
