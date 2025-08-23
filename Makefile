CXX = g++
CXXFLAGS = -std=c++17 `pkg-config --cflags gtkmm-4.0` -Wall -Wextra -Wpedantic -g
LDFLAGS = `pkg-config --libs gtkmm-4.0`
OBJCOPY ?= objcopy

SRC := $(shell find . -name '*.cpp')
OBJ = $(patsubst %.cpp,out/%.o,$(SRC))

all: out/forgery-manager

out:
	mkdir -p out

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

out/%.o: %.cpp | out
	$(CXX) $(CXXFLAGS) -c $< -o $@

out/forgery-manager: $(OBJ)
	$(CXX) -o $@ $^ $(LDFLAGS)


clean:
	rm -rf out
	rm -f gmlp/superpatch.gmlp
