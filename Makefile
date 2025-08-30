CXX ?= g++
LDFLAGS = `pkg-config --libs gtkmm-4.0`
OBJCOPY ?= objcopy

DEBUG_CXXFLAGS := -std=c++17 `pkg-config --cflags gtkmm-4.0` -Wall -Wextra -g
RELEASE_CXXFLAGS := -std=c++17 `pkg-config --cflags gtkmm-4.0` -Ofast

DEBUG_OBJ = $(patsubst %.cpp,out/debug/%.o,$(SRC)) 
RELEASE_OBJ = $(patsubst %.cpp,out/release/%.o,$(SRC)) 

SRC := $(shell find . -name '*.cpp')


.PHONY: all debug release clean nix

all: debug
debug: out/debug/forgery-manager
release: out/release/forgery-manager

nix: RELEASE_CXXFLAGS += -DFORGERYMANAGER_UMC_PATH="$(FORGERYMANAGER_UMC_PATH)"
nix: release

out:
	mkdir -p $@
out/debug:
	mkdir -p $@
out/release:
	mkdir -p $@


# merger script to be embedded 
out/merger.csx.o: csx/merger.csx | out
	$(OBJCOPY) --input binary --output elf64-x86-64 $< $@

.INTERMEDIATE: gmlp/superpatch.gmlp

# merge all patches with newlines
gmlp/superpatch.gmlp: gmlp/*.gmlp
	awk 'FNR==1 && NR!=1{print ""}1' gmlp/*.gmlp > $@

# modloader patches to be embedded
out/superpatch.gmlp.o: gmlp/superpatch.gmlp | out
	$(OBJCOPY) --input binary --output elf64-x86-64 $< $@

# modloader data.win to be embedded
out/forgery.win.o: win/forgery.win | out
	$(OBJCOPY) --input binary --output elf64-x86-64 $< $@

out/debug/%.o: %.cpp | out/debug
	$(CXX) $(DEBUG_CXXFLAGS) -c $< -o $@

out/debug/forgery-manager: $(DEBUG_OBJ) out/merger.csx.o out/superpatch.gmlp.o out/forgery.win.o
	$(CXX) -o $@ $^ $(LDFLAGS)


out/release/%.o: %.cpp | out/release
	$(CXX) $(RELEASE_CXXFLAGS) -c $< -o $@

out/release/forgery-manager: $(RELEASE_OBJ) out/merger.csx.o out/superpatch.gmlp.o out/forgery.win.o
	$(CXX) -o $@ $^ $(LDFLAGS)



clean:
	rm -rf out
	rm -f gmlp/superpatch.gmlp
