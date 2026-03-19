#!/usr/bin/env bash

rm -rf build
mkdir -p build
cmake -B build
cd build
make
