CXX = g++
CXXFLAGS = -std=c++17 `pkg-config --cflags gtkmm-4.0` -Wall -Wextra -g
LDFLAGS = `pkg-config --libs gtkmm-4.0`

SRC := $(shell find . -name '*.cpp')
OBJ = $(patsubst %.cpp,out/%.o,$(SRC))
TARGET = out/forgery-manager

all: $(TARGET)

csx/merger.csx.h: csx/merger.csx
	xxd -i $< > $@

gmlp/superpatch.gmlp:
	cat gmlp/*.gmlp > $@

$(TARGET): $(OBJ)
	$(CXX) -o $@ $^ $(LDFLAGS)

out/%.o: %.cpp csx/merger.csx.h | out
	$(CXX) $(CXXFLAGS) -c $< -o $@

out:
	mkdir -p out

clean:
	rm -rf out
	rm -f csx/merger.csx.h