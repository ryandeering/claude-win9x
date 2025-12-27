/*
 * transfer.h - File upload/download via TCP
 */

#ifndef TRANSFER_H
#define TRANSFER_H

#include "claude.h"

int transfer_download(const char *remote_path, const char *local_path);

int transfer_upload(const char *local_path, const char *remote_path);

#endif /* TRANSFER_H */
