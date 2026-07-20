#include "forgehx.hpp"

#include <stdexcept>
#include <vector>

extern "C" {
int forgehx_derive_seed(const unsigned char* password, std::size_t password_len,
                        const unsigned char* salt, std::size_t salt_len,
                        std::uint32_t memory_kib, std::uint32_t iterations,
                        std::uint32_t parallelism, std::uint32_t output_length,
                        unsigned char* out);
int forgehx_derive_hash(const unsigned char* password, std::size_t password_len,
                        const unsigned char* salt, std::size_t salt_len,
                        std::uint32_t memory_kib, std::uint32_t iterations,
                        std::uint32_t parallelism, std::uint32_t output_length,
                        unsigned char* out);
int forgehx_verify_password(const unsigned char* password, std::size_t password_len,
                            const char* encoded);
}

namespace forgehx {

std::vector<std::uint8_t> derive_seed(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params) {
  std::vector<std::uint8_t> out(32);
  int rc = forgehx_derive_seed(
      reinterpret_cast<const unsigned char*>(password.data()), password.size(),
      salt.data(), salt.size(), static_cast<std::uint32_t>(params.memory_kib),
      static_cast<std::uint32_t>(params.iterations),
      static_cast<std::uint32_t>(params.parallelism),
      static_cast<std::uint32_t>(params.output_length), out.data());
  if (rc != 0) {
    throw std::runtime_error("forgehx_derive_seed failed");
  }
  return out;
}

std::vector<std::uint8_t> derive_hash(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params) {
  std::vector<std::uint8_t> out(params.output_length);
  int rc = forgehx_derive_hash(
      reinterpret_cast<const unsigned char*>(password.data()), password.size(),
      salt.data(), salt.size(), static_cast<std::uint32_t>(params.memory_kib),
      static_cast<std::uint32_t>(params.iterations),
      static_cast<std::uint32_t>(params.parallelism),
      static_cast<std::uint32_t>(params.output_length), out.data());
  if (rc != 0) {
    throw std::runtime_error("forgehx_derive_hash failed");
  }
  return out;
}

bool verify_password(std::string_view password, std::string_view encoded) {
  std::string encoded_z(encoded);
  return forgehx_verify_password(
             reinterpret_cast<const unsigned char*>(password.data()),
             password.size(), encoded_z.c_str()) != 0;
}

}  // namespace forgehx
