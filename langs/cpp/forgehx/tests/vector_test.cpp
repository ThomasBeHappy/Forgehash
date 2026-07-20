#include "forgehx.hpp"

#include <cstdio>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>

// Minimal JSON field extractors for toy vectors (no third-party JSON).
static std::string json_string(const std::string& body, const std::string& key) {
  std::string needle = "\"" + key + "\"";
  auto pos = body.find(needle);
  if (pos == std::string::npos) throw std::runtime_error("missing " + key);
  pos = body.find(':', pos);
  pos = body.find('"', pos);
  auto end = body.find('"', pos + 1);
  return body.substr(pos + 1, end - pos - 1);
}

static int json_int(const std::string& body, const std::string& key) {
  std::string needle = "\"" + key + "\"";
  auto pos = body.find(needle);
  if (pos == std::string::npos) throw std::runtime_error("missing " + key);
  pos = body.find(':', pos) + 1;
  return std::stoi(body.substr(pos));
}

static std::vector<std::uint8_t> from_hex(const std::string& hex) {
  std::vector<std::uint8_t> out;
  out.reserve(hex.size() / 2);
  for (std::size_t i = 0; i + 1 < hex.size(); i += 2) {
    out.push_back(static_cast<std::uint8_t>(std::stoul(hex.substr(i, 2), nullptr, 16)));
  }
  return out;
}

static std::string to_hex(const std::vector<std::uint8_t>& bytes) {
  static const char* k = "0123456789abcdef";
  std::string s;
  s.resize(bytes.size() * 2);
  for (std::size_t i = 0; i < bytes.size(); ++i) {
    s[2 * i] = k[bytes[i] >> 4];
    s[2 * i + 1] = k[bytes[i] & 0xf];
  }
  return s;
}

int main() {
  const char* dir = FORGEHX_VECTOR_DIR;
  const char* files[] = {
      "vector1_empty_password_zero_salt.json",
      "vector2_short_password_incrementing_salt.json",
      "vector3_two_lanes_toy.json",
  };

  for (const char* name : files) {
    std::string path = std::string(dir) + "/" + name;
    std::ifstream in(path);
    if (!in) {
      std::cerr << "missing " << path << "\n";
      return 1;
    }
    std::stringstream buf;
    buf << in.rdbuf();
    std::string body = buf.str();

    auto password = from_hex(json_string(body, "passwordHex"));
    auto salt = from_hex(json_string(body, "saltHex"));
    forgehx::Params params{
        static_cast<std::size_t>(json_int(body, "memoryKiB")),
        static_cast<std::size_t>(json_int(body, "iterations")),
        static_cast<std::size_t>(json_int(body, "parallelism")),
        static_cast<std::size_t>(json_int(body, "outputLength")),
        salt.size(),
    };

    std::string password_sv(reinterpret_cast<const char*>(password.data()), password.size());
    auto seed = forgehx::derive_seed(password_sv, salt, params);
    auto hash = forgehx::derive_hash(password_sv, salt, params);

    if (to_hex(seed) != json_string(body, "seedHex") ||
        to_hex(hash) != json_string(body, "hashHex")) {
      std::cerr << "mismatch " << name << "\n";
      return 2;
    }
    std::cout << "ok " << name << "\n";
  }
  return 0;
}
