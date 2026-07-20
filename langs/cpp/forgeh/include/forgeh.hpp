#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace forgeh {

struct Params {
  std::size_t memory_kib = 65536;
  std::size_t iterations = 3;
  std::size_t parallelism = 1;
  std::size_t output_length = 32;
  std::size_t salt_length = 16;

  static Params development() {
    return Params{8192, 1, 1, 32, 16};
  }
  static Params interactive() { return Params{}; }
  static Params sensitive() { return Params{262144, 4, 2, 32, 16}; }
};

/// Derive raw hash bytes (no salt generation).
std::vector<std::uint8_t> derive_hash(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params);

std::vector<std::uint8_t> derive_seed(std::string_view password,
                                      const std::vector<std::uint8_t>& salt,
                                      const Params& params);

std::string encode(const Params& params,
                   const std::vector<std::uint8_t>& salt,
                   const std::vector<std::uint8_t>& hash);

std::string hash_password(std::string_view password, const Params& params);
bool verify_password(std::string_view password, std::string_view encoded);

}  // namespace forgeh
