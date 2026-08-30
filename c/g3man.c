#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "g3man.h"

#include "git2/apply.h"
#include "git2/buffer.h"
#include "git2/diff.h"
#include "git2/errors.h"
#include "git2/global.h"
#include "git2/merge.h"
#include "git2/patch.h"

#include "xdelta3/xdelta3.h"

#define LIBGIT2_NO_FEATURES_H
#include "libgit2/apply.h"
#include "libgit2/patch.h"
#include "libgit2/patch_parse.h"
#include "util/str.h"

xd3_stream stream;
xd3_source source;

FILE *input_file;

// numbers pulled out of my
#define INPUT_BLOCK_SIZE 32768
#define SOURCE_BLOCK_SIZE 32768

uint8_t block[SOURCE_BLOCK_SIZE];

int get_block_file(xd3_stream *cstream, xd3_source *csource, xoff_t blkno) {
  csource->curblkno = blkno;
  fseek(csource->ioh, blkno * SOURCE_BLOCK_SIZE, SEEK_SET);
  csource->onblk =
      fread(block, sizeof(uint8_t), SOURCE_BLOCK_SIZE, csource->ioh);
  return 0;
}

int start_decode(const char *source_path, const char *input_path) {
  int ret;
  xd3_config config;
  xd3_init_config(&config, 0 /* flags */);
  ret = xd3_config_stream(&stream, &config);
  if (ret != 0) {
    return 1;
  }

  FILE *source_file = fopen(source_path, "rb");

  source.max_winsize = SOURCE_BLOCK_SIZE; // I don't understand this one
  source.name = "datafile";
  source.ioh = source_file;
  source.blksize = SOURCE_BLOCK_SIZE;
  source.curblkno = (xoff_t)-1;
  source.curblk = block;

  ret = xd3_set_source(&stream, &source);
  if (ret != 0) {
    return 1;
  }
  stream.getblk = get_block_file;
  input_file = fopen(input_path, "rb");
  return 0;
}

const uint8_t *in_memory_source;
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

int start_decode_from_memory(const uint8_t *source_data, usize_t source_length,
                             const char *input_path) {
  int ret;
  xd3_config config;
  xd3_init_config(&config, 0 /* flags */);
  ret = xd3_config_stream(&stream, &config);
  if (ret != 0) {
    return 1;
  }

  in_memory_source = source_data;
  in_memory_source_length = source_length;

  source.max_winsize = SOURCE_BLOCK_SIZE;
  source.name = "datafile";
  source.ioh = NULL;
  source.blksize = SOURCE_BLOCK_SIZE;
  source.curblkno = (xoff_t)-1;
  source.curblk = NULL;

  ret = xd3_set_source(&stream, &source);
  if (ret != 0) {
    return 1;
  }
  stream.getblk = get_block_memory;
  input_file = fopen(input_path, "rb");
  return 0;
}

typedef enum {
  TAKE_OUTPUT = 0,
  CALL_AGAIN = 1,
  DONE = 2,
  ERRORED = 3
} return_codes;

uint8_t buf[INPUT_BLOCK_SIZE];
int decode(uint8_t **written_buffer, size_t *written_count) {
  while (1) {
    int ret = xd3_decode_input(&stream);
    switch (ret) {
    case XD3_INPUT: {
      unsigned long read =
          fread(buf, sizeof(uint8_t), INPUT_BLOCK_SIZE, input_file);
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
      // TODO: is there any reason to go back to c# here?
      // can we just continue instead
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

int initialize() { return git_libgit2_init(); }

void get_string_from_git_buf(void *buf, char **ptr, size_t *size) {
  *ptr = ((git_buf *)buf)->ptr;
  *size = ((git_buf *)buf)->size;
}
void free_git_buf(void *buf) {
  git_buf_dispose(buf);
  free(buf);
}
git_buf *allocate_git_buf() {
  git_buf *buf = malloc(sizeof(git_buf));
  *buf = (git_buf)GIT_BUF_INIT;
  return buf;
}
git_buf *allocate_git_buf_for_string(const char *str, size_t len) {
  git_buf *buf = malloc(sizeof(git_buf));
  *buf = (git_buf)GIT_BUF_INIT;
  buf->ptr = malloc(len);
  memcpy(buf->ptr, str, len);
  buf->size = len;
  return buf;
}
git_buf *allocate_git_buf_for_c_string(const char *str) {
  size_t len = strlen(str);
  return allocate_git_buf_for_string(str, len);
}

void *create_diff(const char *original, size_t original_size,
                  const char *modified, size_t modified_size,
                  const char *filename) {
  git_patch *patch;
  git_diff_options options = GIT_DIFF_OPTIONS_INIT;

  if (git_patch_from_buffers(&patch, original, original_size, filename,
                             modified, modified_size, filename, &options))
    return NULL;

  git_buf *buf = allocate_git_buf();

  if (git_patch_to_buf(buf, patch))
    return NULL;
  git_patch_free(patch);
  return buf;
}

void *apply_diff(const char *text, size_t text_size, const char *diff_text,
                 size_t diff_text_size) {

  git_patch_options patch_options = GIT_PATCH_OPTIONS_INIT;
  git_patch *patch;
  if (git_patch_from_buffer(&patch, diff_text, diff_text_size, &patch_options))
    return NULL;

  git_str str = GIT_STR_INIT;
  char *filename;
  unsigned int outmode;

  git_apply_options options = GIT_APPLY_OPTIONS_INIT;
  if (git_apply__patch(&str, &filename, &outmode, text, text_size, patch,
                       &options))
    return NULL;

  git_buf *buf = allocate_git_buf();
  buf->ptr = str.ptr;
  buf->size = str.size;

  git_patch_free(patch);
  return buf;
}

void fill_file_input(git_merge_file_input *input, const char *ptr,
                     size_t size) {
  input->version = GIT_MERGE_FILE_INPUT_VERSION;
  input->path = NULL;
  input->ptr = ptr;
  input->size = size;
}

void *three_way_merge(const char *base, size_t base_size, const char *ours,
                      size_t ours_size, const char *theirs, size_t theirs_size,
                      int *automerged) {
  git_merge_file_result out;

  git_merge_file_input base_file;
  fill_file_input(&base_file, base, base_size);
  git_merge_file_input our_file;
  fill_file_input(&our_file, ours, ours_size);
  git_merge_file_input their_file;
  fill_file_input(&their_file, theirs, theirs_size);

  git_merge_file_options options = GIT_MERGE_FILE_OPTIONS_INIT;
  options.flags |= GIT_MERGE_FILE_IGNORE_WHITESPACE_CHANGE |
                   GIT_MERGE_FILE_IGNORE_WHITESPACE_EOL;
  if (git_merge_file(&out, &base_file, &our_file, &their_file, &options))
    return NULL;

  git_buf *buf = allocate_git_buf_for_string(out.ptr, out.len);
  git_merge_file_result_free(&out);
  *automerged = out.automergeable;

  return buf;
}
void *diff_get_target_filename(const char *diff_text, size_t diff_text_size) {

  git_patch_options patch_options = GIT_PATCH_OPTIONS_INIT;
  git_patch *patch;
  if (git_patch_from_buffer(&patch, diff_text, diff_text_size, &patch_options))
    return NULL;

  git_buf *buf = allocate_git_buf_for_c_string(patch->delta->old_file.path);

  git_patch_free(patch);

  return buf;
}

void *get_last_git_error() {
  const git_error *error = git_error_last();
  git_buf *buf = allocate_git_buf_for_c_string(error->message);
  return buf;
}
