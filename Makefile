CXX = g++
CXXFLAGS = -std=c++17 `pkg-config --cflags gtkmm-4.0` -Wall -Wextra -g
LDFLAGS = `pkg-config --libs gtkmm-4.0`

SRC := $(shell find . -name '*.cpp')
OBJ = $(patsubst %.cpp,out/%.o,$(SRC))
TARGET = out/forgerymanager

all: $(TARGET)

$(TARGET): $(OBJ)
	$(CXX) -o $@ $^ $(LDFLAGS)

out/%.o: %.cpp | out
	$(CXX) $(CXXFLAGS) -c $< -o $@

out:
	mkdir -p out

clean:
	rm -rf out