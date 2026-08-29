#pragma once

#include <stddef.h>
#include <stdint.h>

int initialize();


// xdelta
int start_decode(const char* source_path, const char* input_path);
int start_decode_mem(const char* source, size_t source_length);
int decode(uint8_t** written_buffer, size_t* written_count);


// libgit2
void get_string_from_git_buf(void *buf, char **ptr, size_t *size);
void free_git_buf(void* buf);

void* create_diff(const char *original, size_t original_size,
                  const char *modified, size_t modified_size,
                  const char *filename);
void* apply_diff(const char *text, size_t text_size, const char *diff_text,
                 size_t diff_text_size);
void* three_way_merge(const char *base, size_t base_size, const char *ours,
                      size_t ours_size, const char *theirs, size_t theirs_size, int* automerged);
void* diff_get_target_filename(const char* diff, size_t diff_size);
void* get_last_git_error();
