use forgehx::{derive_hash, derive_seed, encode, Params};
use serde::Deserialize;
use std::fs;
use std::path::PathBuf;

#[derive(Deserialize)]
struct VectorFile {
    #[serde(rename = "passwordHex")]
    password_hex: String,
    #[serde(rename = "saltHex")]
    salt_hex: String,
    #[serde(rename = "memoryKiB")]
    memory_kib: usize,
    iterations: usize,
    parallelism: usize,
    #[serde(rename = "outputLength")]
    output_length: usize,
    #[serde(rename = "seedHex")]
    seed_hex: String,
    #[serde(rename = "hashHex")]
    hash_hex: String,
    encoded: String,
}

fn vectors_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../../implementers/x0/vectors")
}

fn load(name: &str) -> VectorFile {
    let path = vectors_dir().join(name);
    let data = fs::read_to_string(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    serde_json::from_str(&data).expect("parse vector json")
}

fn run_vector(file: &str) {
    let v = load(file);
    let password = hex::decode(&v.password_hex).unwrap();
    let salt = hex::decode(&v.salt_hex).unwrap();
    let params = Params {
        memory_kib: v.memory_kib,
        iterations: v.iterations,
        parallelism: v.parallelism,
        output_length: v.output_length,
        salt_length: salt.len(),
    };
    let seed = derive_seed(&password, &salt, &params).unwrap();
    let hash = derive_hash(&password, &salt, &params).unwrap();
    assert_eq!(hex::encode(seed), v.seed_hex, "seed mismatch in {file}");
    assert_eq!(hex::encode(&hash), v.hash_hex, "hash mismatch in {file}");
    assert_eq!(
        encode(&params, &salt, &hash),
        v.encoded,
        "encoded mismatch in {file}"
    );
}

#[test]
fn vector1() {
    run_vector("vector1_empty_password_zero_salt.json");
}

#[test]
fn vector2() {
    run_vector("vector2_short_password_incrementing_salt.json");
}

#[test]
fn vector3() {
    run_vector("vector3_two_lanes_toy.json");
}
