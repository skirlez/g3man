# c

This is some C code g3man uses. The CMake project here produces libg3man and all the libraries it uses.

libg3man is a small library that interfaces with other c libraries to provide a simpler API for g3man to use (importantly, one that doesn't require defining any structs).

### libxdelta
The libxdelta here is based off of https://github.com/marco-calautti/xdelta, modified to build a .so/dll shared library.

g3man uses this library to apply .xdelta patches.

If you're packaging g3man, it will likely work if dynamically linked against the system's version of the library.

### libgit2
Libgit2 is a submodule that points to the main repo. 

g3man uses this library to create and apply git diffs, as well as perform three-way merges.

Due to the fact it also uses some internal functions, and that Windows was being annoying regarding their visiblity, I've opted to link it statically.