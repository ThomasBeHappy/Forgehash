cmake_minimum_required(VERSION 3.20)
project(forgehx_cpp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(FORGEHX_RUST_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../rust/forgehx")
set(FORGEHX_RUST_LIB_DIR "${FORGEHX_RUST_DIR}/target/release")

add_custom_target(forgehx_rust
  COMMAND cargo build --release --manifest-path "${FORGEHX_RUST_DIR}/Cargo.toml"
  WORKING_DIRECTORY "${FORGEHX_RUST_DIR}"
  COMMENT "Building forgehx Rust cdylib")

add_library(forgehx STATIC src/forgehx.cpp)
add_dependencies(forgehx forgehx_rust)
target_include_directories(forgehx PUBLIC include)

if(WIN32)
  target_link_libraries(forgehx PUBLIC "${FORGEHX_RUST_LIB_DIR}/forgehx.lib")
else()
  target_link_libraries(forgehx PUBLIC "${FORGEHX_RUST_LIB_DIR}/libforgehx.a" pthread dl m)
endif()

add_executable(forgehx_vector_test tests/vector_test.cpp)
target_link_libraries(forgehx_vector_test PRIVATE forgehx)
target_compile_definitions(forgehx_vector_test PRIVATE
  FORGEHX_VECTOR_DIR="${CMAKE_CURRENT_SOURCE_DIR}/../../../implementers/x0/vectors")
