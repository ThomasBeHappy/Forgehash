#include "forgeh.hpp"

#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

namespace {

std::string read_file(const std::string& path) {
  std::ifstream in(path);
  if (!in) {
    throw std::runtime_error("cannot open " + path);
  }
  std::ostringstream ss;
  ss << in.rdbuf();
  return ss.str();
}

std::string json_string(const std::string& json, const std::string& key) {
  const std::string needle = "\"" + key + "\": \"";
  auto pos = json.find(needle);
  if (pos == std::string::npos) {
    // try without space
    const std::string needle2 = "\"" + key + "\":\"";
    pos = json.find(needle2);
    if (pos == std::string::npos) {
      throw std::runtime_error("missing key " + key);
    }
    pos += needle2.size();
  } else {
    pos += needle.size();
  }
  auto end = json.find('"', pos);
  return json.substr(pos, end - pos);
}

std::size_t json_number(const std::string& json, const std::string& key) {
  const std::string needle = "\"" + key + "\": ";
  auto pos = json.find(needle);
  if (pos == std::string::npos) {
    const std::string needle2 = "\"" + key + "\":";
    pos = json.find(needle2);
    if (pos == std::string::npos) {
      throw std::runtime_error("missing key " + key);
    }
    pos += needle2.size();
  } else {
    pos += needle.size();
  }
  return static_cast<std::size_t>(std::stoull(json.substr(pos)));
}

std::vector<std::uint8_t> from_hex(const std::string& hex) {
  std::vector<std::uint8_t> out;
  out.reserve(hex.size() / 2);
  for (std::size_t i = 0; i + 1 < hex.size(); i += 2) {
    out.push_back(static_cast<std::uint8_t>(std::stoul(hex.substr(i, 2), nullptr, 16)));
  }
  return out;
}

std::string to_hex(const std::vector<std::uint8_t>& data) {
  static const char* k = "0123456789abcdef";
  std::string out;
  out.resize(data.size() * 2);
  for (std::size_t i = 0; i < data.size(); ++i) {
    out[i * 2] = k[data[i] >> 4];
    out[i * 2 + 1] = k[data[i] & 0xf];
  }
  return out;
}

void check_vector(const std::string& path) {
  const auto json = read_file(path);
  const auto password = from_hex(json_string(json, "passwordHex"));
  const auto salt = from_hex(json_string(json, "saltHex"));
  forgeh::Params params{
      json_number(json, "memoryKiB"),
      json_number(json, "iterations"),
      json_number(json, "parallelism"),
      32,
      salt.size(),
  };
  const auto expect_seed = json_string(json, "seedHex");
  const auto expect_hash = json_string(json, "hashHex");
  const auto expect_encoded = json_string(json, "encoded");

  std::string_view pw(reinterpret_cast<const char*>(password.data()), password.size());
  auto seed = forgeh::derive_seed(pw, salt, params);
  auto hash = forgeh::derive_hash(pw, salt, params);
  auto encoded = forgeh::encode(params, salt, hash);

  if (to_hex(seed) != expect_seed) {
    throw std::runtime_error(path + ": seed mismatch");
  }
  if (to_hex(hash) != expect_hash) {
    throw std::runtime_error(path + ": hash mismatch");
  }
  if (encoded != expect_encoded) {
    throw std::runtime_error(path + ": encoded mismatch");
  }
  std::cout << "ok " << path << "\n";
}

}  // namespace

int main() {
  try {
    const std::string root = std::string(FORGEH_VECTOR_DIR);
    check_vector(root + "/vector1_empty_password_zero_salt.json");
    check_vector(root + "/vector2_password_incrementing_salt.json");
    check_vector(root + "/vector3_utf8_two_lanes.json");
    check_vector(root + "/vector4_null_bytes_four_lanes.json");
    std::cout << "all vectors passed\n";
    return 0;
  } catch (const std::exception& ex) {
    std::cerr << ex.what() << "\n";
    return 1;
  }
}
