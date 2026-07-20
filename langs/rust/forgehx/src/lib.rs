//! Experimental ForgeHash-X v0 — Rust reference (custom ForgeX sponge).
//!
//! **Not for production password storage. Not compatible with ForgeHash-B3.**

mod encoding;
mod engine;
mod error;
mod forgeperm;
mod forgex;
mod params;

pub use encoding::{encode, parse, ParsedHash};
pub use engine::{derive_hash, derive_seed};
pub use error::Error;
pub use params::Params;

/// Hash a password with a random salt; returns the canonical encoded string.
pub fn hash_password(password: &[u8], params: &Params) -> Result<String, Error> {
    params.validate()?;
    let mut salt = vec![0u8; params.salt_length];
    getrandom::fill(&mut salt).map_err(|_| Error::Random)?;
    let hash = derive_hash(password, &salt, params)?;
    Ok(encode(params, &salt, &hash))
}

/// Verify a password against an encoded hash using a constant-time compare.
pub fn verify_password(password: &[u8], encoded: &str) -> bool {
    let parsed = match parse(encoded) {
        Ok(p) => p,
        Err(_) => return false,
    };
    let actual = match derive_hash(password, &parsed.salt, &parsed.params) {
        Ok(h) => h,
        Err(_) => return false,
    };
    constant_time_eq(&actual, &parsed.hash)
}

fn constant_time_eq(a: &[u8], b: &[u8]) -> bool {
    if a.len() != b.len() {
        return false;
    }
    let mut diff = 0u8;
    for (x, y) in a.iter().zip(b.iter()) {
        diff |= x ^ y;
    }
    diff == 0
}

/// C ABI for FFI consumers.
pub mod ffi {
    use crate::{derive_hash, derive_seed, params::Params, verify_password};
    use std::os::raw::{c_char, c_int, c_uchar};
    use std::slice;

    #[no_mangle]
    pub unsafe extern "C" fn forgehx_derive_seed(
        password: *const c_uchar,
        password_len: usize,
        salt: *const c_uchar,
        salt_len: usize,
        memory_kib: u32,
        iterations: u32,
        parallelism: u32,
        output_length: u32,
        out: *mut c_uchar,
    ) -> c_int {
        if password.is_null() || salt.is_null() || out.is_null() {
            return 1;
        }
        let password = slice::from_raw_parts(password, password_len);
        let salt = slice::from_raw_parts(salt, salt_len);
        let params = Params {
            memory_kib: memory_kib as usize,
            iterations: iterations as usize,
            parallelism: parallelism as usize,
            output_length: output_length as usize,
            salt_length: salt_len,
        };
        match derive_seed(password, salt, &params) {
            Ok(seed) => {
                std::ptr::copy_nonoverlapping(seed.as_ptr(), out, 32);
                0
            }
            Err(_) => 3,
        }
    }

    #[no_mangle]
    pub unsafe extern "C" fn forgehx_derive_hash(
        password: *const c_uchar,
        password_len: usize,
        salt: *const c_uchar,
        salt_len: usize,
        memory_kib: u32,
        iterations: u32,
        parallelism: u32,
        output_length: u32,
        out: *mut c_uchar,
    ) -> c_int {
        if password.is_null() || salt.is_null() || out.is_null() {
            return 1;
        }
        let password = slice::from_raw_parts(password, password_len);
        let salt = slice::from_raw_parts(salt, salt_len);
        let params = Params {
            memory_kib: memory_kib as usize,
            iterations: iterations as usize,
            parallelism: parallelism as usize,
            output_length: output_length as usize,
            salt_length: salt_len,
        };
        match derive_hash(password, salt, &params) {
            Ok(hash) if hash.len() == output_length as usize => {
                std::ptr::copy_nonoverlapping(hash.as_ptr(), out, hash.len());
                0
            }
            _ => 3,
        }
    }

    #[no_mangle]
    pub unsafe extern "C" fn forgehx_verify_password(
        password: *const c_uchar,
        password_len: usize,
        encoded: *const c_char,
    ) -> c_int {
        if password.is_null() || encoded.is_null() {
            return 0;
        }
        let password = slice::from_raw_parts(password, password_len);
        let Ok(encoded) = std::ffi::CStr::from_ptr(encoded).to_str() else {
            return 0;
        };
        i32::from(verify_password(password, encoded))
    }
}
