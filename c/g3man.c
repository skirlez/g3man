#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include "g3man.h"



xd3_stream stream;
xd3_source source;

FILE* input_file;

// numbers pulled out of my
#define INPUT_BLOCK_SIZE 32768
#define SOURCE_BLOCK_SIZE 32768

uint8_t block[SOURCE_BLOCK_SIZE];

int get_block_file(xd3_stream *cstream, xd3_source *csource, xoff_t blkno) {
	csource->curblkno = blkno;
	fseek(csource->ioh, blkno * SOURCE_BLOCK_SIZE, SEEK_SET);
	csource->onblk = fread(block, sizeof(uint8_t), SOURCE_BLOCK_SIZE, csource->ioh);
	return 0;
}

int start_decode(const char* source_path, const char* input_path) {
	int ret;
	xd3_config config;
	xd3_init_config(&config, 0 /* flags */);
	ret = xd3_config_stream(&stream, &config);
	if (ret != 0) { 
		return 1;
	}
	
	FILE* source_file;
	ret = fopen_s(&source_file, source_path, "rb");
	if (ret != 0) { 
		return 1;
	}
	
	source.max_winsize = SOURCE_BLOCK_SIZE; // I don't understand this one
	source.name = "datafile";
	source.ioh = source_file;
	source.blksize = SOURCE_BLOCK_SIZE;
	source.curblkno = (xoff_t) -1;
	source.curblk = block;
	
	ret = xd3_set_source(&stream, &source);
	if (ret != 0) { 
		return 1;
	}
	stream.getblk = get_block_file;
	ret = fopen_s(&input_file, input_path, "rb");
	if (ret != 0) { 
		return 1;
	}
	return 0;
}

const uint8_t* in_memory_source;
usize_t in_memory_source_length;
int get_block_memory(xd3_stream *cstream, xd3_source *csource, xoff_t blkno) {
	csource->curblkno = blkno;
	usize_t offset = blkno * SOURCE_BLOCK_SIZE;
	if (offset + SOURCE_BLOCK_SIZE >= in_memory_source_length)
		csource->onblk = in_memory_source_length - offset;
	else
		csource->onblk = SOURCE_BLOCK_SIZE;
	csource->curblk = in_memory_source + offset;
	return 0;
}


int start_decode_from_memory(const uint8_t* source_data, usize_t source_length, const char* input_path) {
	int ret;
	xd3_config config;
	xd3_init_config(&config, 0 /* flags */);
	ret = xd3_config_stream (&stream, &config);
	if (ret != 0) { 
		return 1;
	}
	
	in_memory_source = source_data;
	in_memory_source_length = source_length;
	
	source.max_winsize = SOURCE_BLOCK_SIZE;
	source.name = "datafile";
	source.ioh = NULL;
	source.blksize = SOURCE_BLOCK_SIZE;
	source.curblkno = (xoff_t) -1;
	source.curblk = NULL;
	
	ret = xd3_set_source(&stream, &source);
	if (ret != 0) { 
		return 1;
	}
	stream.getblk = get_block_memory;
	ret = fopen_s(&input_file, input_path, "rb");
	if (ret != 0) { 
		return 1;
	}
	return 0;
}

typedef enum {
	TAKE_OUTPUT = 0,
	CALL_AGAIN = 1,
	DONE = 2,
	ERRORED = 3
} return_codes;


uint8_t buf[INPUT_BLOCK_SIZE];
int decode(uint8_t** written_buffer, usize_t* written_count) {
	while (1) {
		int ret = xd3_decode_input(&stream);
		switch (ret) {
			case XD3_INPUT: {
				unsigned long read = fread(buf, sizeof(uint8_t), INPUT_BLOCK_SIZE, input_file);
				xd3_avail_input(&stream, buf, read);
				if (read == 0) {
					fclose(input_file);
					if (source.ioh != NULL)
						fclose(source.ioh);
					xd3_close_stream(&stream);
					xd3_free_stream(&stream);
					return DONE;
				}
				continue;
			}
			case XD3_OUTPUT:
				(*written_count) = stream.avail_out;
				(*written_buffer) = stream.next_out;
				xd3_consume_output(&stream);
				return TAKE_OUTPUT;
			case XD3_GOTHEADER:
			case XD3_WINSTART:
			case XD3_WINFINISH:
				return CALL_AGAIN;
			case XD3_GETSRCBLK:
			default:
				if (stream.msg != NULL)
					printf("%s\n", stream.msg);
				else
					printf("No error given\n");
				return ERRORED;
		}
	}
}
