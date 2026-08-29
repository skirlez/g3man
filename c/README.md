# c

This is some C code g3man uses. The CMake project here produces libg3man for use by g3man, libxdelta3, and libgit2.

libg3man is a small library that interfaces with libxdelta and libgit2 to provide a simpler API for g3man to use (importantly, one that doesn't require defining any structs).

The libxdelta here is based off of https://github.com/marco-calautti/xdelta, modified to build a .so/dll shared library. This could probably be devendored.

Libgit2 is a submodule that points to the main repo. Libg3man utilizes some internal functionality of libgit2 in order to do all of its operations in-memory, which involves changing the visiblity of some functions. This means it cannot be devendored.
