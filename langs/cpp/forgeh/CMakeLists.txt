cmake_minimum_required(VERSION 3.20)
project(forgeh_cpp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(FORGEH_RUST_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../rust/forgeh")
set(FORGEH_RUST_TARGET "${FORGEH_RUST_DIR}/target/release")
set(FORGEH_VECTOR_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../implementers/v1/vectors")

add_custom_target(forgeh_rust ALL
  COMMAND cargo build --release --manifest-path "${FORGEH_RUST_DIR}/Cargo.toml"
  WORKING_DIRECTORY "${FORGEH_RUST_DIR}"
  BYPRODUCTS
    "${FORGEH_RUST_TARGET}/forgeh.lib"
    "${FORGEH_RUST_TARGET}/forgeh.dll"
    "${FORGEH_RUST_TARGET}/libforgeh.a"
  COMMENT "Building Rust forgeh"
)

add_library(forgeh STATIC src/forgeh.cpp)
target_include_directories(forgeh PUBLIC include)
add_dependencies(forgeh forgeh_rust)

if(WIN32)
  # Prefer the Rust staticlib on Windows.
  target_link_libraries(forgeh PUBLIC
    "${FORGEH_RUST_TARGET}/forgeh.lib"
    bcrypt
    ws2_32
    userenv
    ntdll
  )
else()
  target_link_libraries(forgeh PUBLIC
    "${FORGEH_RUST_TARGET}/libforgeh.a"
    pthread
    dl
    m
  )
endif()

add_executable(forgeh_vector_test tests/vector_test.cpp)
target_compile_definitions(forgeh_vector_test PRIVATE
  "FORGEH_VECTOR_DIR=\"${FORGEH_VECTOR_DIR}\"")
target_link_libraries(forgeh_vector_test PRIVATE forgeh)
add_dependencies(forgeh_vector_test forgeh)
