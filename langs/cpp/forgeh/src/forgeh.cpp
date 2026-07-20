#include "forgeh.hpp"

#include <cstring>
#include <stdexcept>

#if defined(_WIN32)
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <bcrypt.h>
#pragma comment(lib, "bcrypt.lib")
#else
#include <fstream>
#endif

extern "C" {
int forgeh_derive_hash(const unsigned char* password, size_t password_len,
                       const unsigned char* salt, size_t salt_len,
                       uint32_t memory_kib, uint32_t iterations,
                       uint32_t parallelism, uint32_t output_length,
                       unsigned char* out);

int forgeh_derive_seed(const unsigned char* password, size_t password_len,
                       const unsigned char* salt, size_t salt_len,
                       uint32_t memory_kib, uint32_t iterations,
                       uint32_t parallelism, uint32_t output_length,
                       unsigned char* out);

int forgeh_verify_password(const unsigned char* password, size_t password_len,
                           const char* encoded);

int forgeh_encode(uint32_t memory_kib, uint32_t iterations, uint32_t parallelism,
                  const unsigned char* salt, size_t salt_len,
                  const unsigned char* hash, size_t hash_len, char* out,
                  size_t out_len);
}

namespace forgeh {
namespace {

void fill_random(std::uint8_t* buf, std::size_t len) {
#if defined(_WIN32)
  if (BCryptGenRandom(nullptr, buf, static_cast<ULONG>(len),
                      BCRYPT_USE_SYSTEM_PREFERRED_RNG) != 0) {
    throw std::runtime_error("random failed");
  }
#else
  std::ifstream urandom("/dev/urandom", std::ios::binary);
  if (!urandom.read(reinterpret_cast<char*>(buf),
                    static_cast<std::streamsize>(len))) {
    throw std::runtime_error("random failed");
  }
#endif
}

}  // namespace

std::vector<std::uint8_t> derive_hash(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params) {
  std::vector<std::uint8_t> out(params.output_length);
  const int rc = forgeh_derive_hash(
      reinterpret_cast<const unsigned char*>(password.data()), password.size(),
      salt.data(), salt.size(), static_cast<uint32_t>(params.memory_kib),
      static_cast<uint32_t>(params.iterations),
      static_cast<uint32_t>(params.parallelism),
      static_cast<uint32_t>(params.output_length), out.data());
  if (rc != 0) {
    throw std::runtime_error("forgeh_derive_hash failed");
  }
  return out;
}

std::vector<std::uint8_t> derive_seed(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params) {
  std::vector<std::uint8_t> out(32);
  const int rc = forgeh_derive_seed(
      reinterpret_cast<const unsigned char*>(password.data()), password.size(),
      salt.data(), salt.size(), static_cast<uint32_t>(params.memory_kib),
      static_cast<uint32_t>(params.iterations),
      static_cast<uint32_t>(params.parallelism),
      static_cast<uint32_t>(params.output_length), out.data());
  if (rc != 0) {
    throw std::runtime_error("forgeh_derive_seed failed");
  }
  return out;
}

std::string encode(const Params& params, const std::vector<std::uint8_t>& salt,
                   const std::vector<std::uint8_t>& hash) {
  std::string out(512, '\0');
  const int n = forgeh_encode(
      static_cast<uint32_t>(params.memory_kib),
      static_cast<uint32_t>(params.iterations),
      static_cast<uint32_t>(params.parallelism), salt.data(), salt.size(),
      hash.data(), hash.size(), out.data(), out.size());
  if (n < 0) {
    throw std::runtime_error("forgeh_encode failed");
  }
  out.resize(static_cast<std::size_t>(n));
  return out;
}

std::string hash_password(std::string_view password, const Params& params) {
  std::vector<std::uint8_t> salt(params.salt_length);
  fill_random(salt.data(), salt.size());
  auto hash = derive_hash(password, salt, params);
  return encode(params, salt, hash);
}

bool verify_password(std::string_view password, std::string_view encoded) {
  std::string cstr(encoded);
  return forgeh_verify_password(
             reinterpret_cast<const unsigned char*>(password.data()),
             password.size(), cstr.c_str()) == 1;
}

}  // namespace forgeh
