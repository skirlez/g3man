rmdir build /S /Q
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build
