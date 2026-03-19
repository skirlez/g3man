#pragma once
#include "xdelta3/xdelta3.h"

int start_decode(const char* source_path, const char* input_path);
int start_decode_mem(const char* source, size_t source_length);
int decode(uint8_t** written_buffer, usize_t* written_count);
