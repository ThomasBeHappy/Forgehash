/* Minimal C smoke test against the Rust forgeh C ABI. */
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

int forgeh_derive_hash(const unsigned char* password, size_t password_len,
                       const unsigned char* salt, size_t salt_len,
                       uint32_t memory_kib, uint32_t iterations,
                       uint32_t parallelism, uint32_t output_length,
                       unsigned char* out);

int forgeh_encode(uint32_t memory_kib, uint32_t iterations, uint32_t parallelism,
                  const unsigned char* salt, size_t salt_len,
                  const unsigned char* hash, size_t hash_len, char* out,
                  size_t out_len);

static int from_hex(const char* hex, unsigned char* out, size_t out_len) {
  size_t n = strlen(hex);
  if (n / 2 > out_len) return -1;
  for (size_t i = 0; i < n; i += 2) {
    unsigned int b;
    if (sscanf(hex + i, "%2x", &b) != 1) return -1;
    out[i / 2] = (unsigned char)b;
  }
  return (int)(n / 2);
}

static void to_hex(const unsigned char* in, size_t n, char* out) {
  static const char* k = "0123456789abcdef";
  for (size_t i = 0; i < n; i++) {
    out[i * 2] = k[in[i] >> 4];
    out[i * 2 + 1] = k[in[i] & 0xf];
  }
  out[n * 2] = 0;
}

int main(void) {
  /* vector2 */
  unsigned char password[16];
  unsigned char salt[16];
  unsigned char hash[32];
  char hex[65];
  char encoded[256];

  int pw_len = from_hex("70617373776f7264", password, sizeof password);
  int salt_len = from_hex("000102030405060708090a0b0c0d0e0f", salt, sizeof salt);
  if (pw_len < 0 || salt_len != 16) {
    fprintf(stderr, "hex decode failed\n");
    return 1;
  }

  if (forgeh_derive_hash(password, (size_t)pw_len, salt, 16, 8192, 1, 1, 32, hash) != 0) {
    fprintf(stderr, "derive failed\n");
    return 1;
  }
  to_hex(hash, 32, hex);
  if (strcmp(hex, "02acdfa7faa0f149fe700b2f46b792fda8eaecd5f14844142c67709c561a6a98") != 0) {
    fprintf(stderr, "hash mismatch: %s\n", hex);
    return 1;
  }

  int n = forgeh_encode(8192, 1, 1, salt, 16, hash, 32, encoded, sizeof encoded);
  if (n < 0) {
    fprintf(stderr, "encode failed\n");
    return 1;
  }
  const char* expect =
      "$forgeh$v=1$m=8192,t=1,p=1$AAECAwQFBgcICQoLDA0ODw$Aqzfp/qg8Un+cAsvRreS/ajq7NXxSEQULGdwnFYaapg";
  if (strcmp(encoded, expect) != 0) {
    fprintf(stderr, "encoded mismatch: %s\n", encoded);
    return 1;
  }

  printf("ok vector2 via C ABI\n");
  return 0;
}
