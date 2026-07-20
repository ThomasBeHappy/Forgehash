#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace forgehx {

struct Params {
  std::size_t memory_kib = 1024;
  std::size_t iterations = 1;
  std::size_t parallelism = 1;
  std::size_t output_length = 32;
  std::size_t salt_length = 16;

  static Params toy() { return Params{}; }
};

/// Experimental ForgeHash-X over Rust C ABI. Not for production. Not B3-compatible.
std::vector<std::uint8_t> derive_hash(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params);

std::vector<std::uint8_t> derive_seed(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params);

bool verify_password(std::string_view password, std::string_view encoded);

}  // namespace forgehx
