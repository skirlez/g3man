# c

This is some C code g3man uses. The CMake project here produces libg3man and all the libraries it uses.

libg3man is a small library that interfaces with other c libraries to provide a simpler API for g3man to use (importantly, one that doesn't require defining any structs).

### libxdelta
The libxdelta here is based off of https://github.com/marco-calautti/xdelta, modified to build a .so/dll shared library.

g3man uses this library to apply .xdelta patches.

If packaging: it will likely work if linked against any other version.

### libgit2
Libgit2 is a submodule that points to the main repo. 

g3man uses this library to create and apply git diffs, as well as perform three-way merges.

If packaging: will likely NOT work if linked against another version, as in this CMake project, the visiblity preset is changed to expose internal libgit2 functions.
