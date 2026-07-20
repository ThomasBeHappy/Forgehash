//! Canonical `$forgehx$v=0$...` encoding / parsing.

use crate::error::Error;
use crate::params::{
    Params, MAX_ITERATIONS, MAX_MEMORY_KIB, MAX_OUTPUT_LENGTH, MAX_PARALLELISM, MAX_SALT_LENGTH,
    MIN_MEMORY_KIB, MIN_OUTPUT_LENGTH, MIN_SALT_LENGTH,
};

pub const ALGORITHM_ID: &str = "forgehx";
pub const VERSION: u32 = 0;

pub struct ParsedHash {
    pub version: u32,
    pub params: Params,
    pub salt: Vec<u8>,
    pub hash: Vec<u8>,
    pub encoded: String,
}

pub fn encode(params: &Params, salt: &[u8], hash: &[u8]) -> String {
    format!(
        "${ALGORITHM_ID}$v={VERSION}$m={},t={},p={}${}${}",
        params.memory_kib,
        params.iterations,
        params.parallelism,
        b64(salt),
        b64(hash)
    )
}

pub fn parse(encoded: &str) -> Result<ParsedHash, Error> {
    if encoded.contains('\0') || encoded.chars().any(|c| c.is_whitespace()) {
        return Err(Error::Format("whitespace or null"));
    }
    let parts: Vec<&str> = encoded.split('$').collect();
    if parts.len() != 6 || !parts[0].is_empty() {
        return Err(Error::Format("field count"));
    }
    if parts[1] != ALGORITHM_ID {
        return Err(Error::Unsupported);
    }
    let version = parse_version(parts[2])?;
    if version != VERSION {
        return Err(Error::Unsupported);
    }
    let (memory_kib, iterations, parallelism) = parse_costs(parts[3])?;
    let salt = b64_decode(parts[4])?;
    let hash = b64_decode(parts[5])?;
    if !(MIN_OUTPUT_LENGTH..=MAX_OUTPUT_LENGTH).contains(&hash.len()) {
        return Err(Error::Format("hash length"));
    }
    if !(MIN_MEMORY_KIB..=MAX_MEMORY_KIB).contains(&memory_kib)
        || !(1..=MAX_ITERATIONS).contains(&iterations)
        || !(1..=MAX_PARALLELISM).contains(&parallelism)
        || !(MIN_SALT_LENGTH..=MAX_SALT_LENGTH).contains(&salt.len())
    {
        return Err(Error::Format("parameter policy"));
    }

    let params = Params {
        memory_kib,
        iterations,
        parallelism,
        output_length: hash.len(),
        salt_length: salt.len(),
    };
    params.validate().map_err(|_| Error::Format("parameter policy"))?;

    let canonical = encode(&params, &salt, &hash);
    if canonical != encoded {
        return Err(Error::Format("non-canonical"));
    }
    Ok(ParsedHash {
        version,
        params,
        salt,
        hash,
        encoded: canonical,
    })
}

fn parse_version(field: &str) -> Result<u32, Error> {
    let Some(rest) = field.strip_prefix("v=") else {
        return Err(Error::Format("version"));
    };
    // Version 0 is allowed for ForgeHash-X sandbox.
    parse_strict_non_negative(rest)
}

fn parse_costs(field: &str) -> Result<(usize, usize, usize), Error> {
    let segs: Vec<&str> = field.split(',').collect();
    if segs.len() != 3 {
        return Err(Error::Format("costs"));
    }
    Ok((
        parse_pref(segs[0], "m")?,
        parse_pref(segs[1], "t")?,
        parse_pref(segs[2], "p")?,
    ))
}

fn parse_pref(seg: &str, name: &str) -> Result<usize, Error> {
    let Some((n, v)) = seg.split_once('=') else {
        return Err(Error::Format("cost field"));
    };
    if n != name {
        return Err(Error::Format("cost order"));
    }
    Ok(parse_strict_positive(v)? as usize)
}

fn parse_strict_non_negative(text: &str) -> Result<u32, Error> {
    if text.is_empty() || text.starts_with('+') || text.starts_with('-') {
        return Err(Error::Format("int"));
    }
    if text.len() > 1 && text.starts_with('0') {
        return Err(Error::Format("leading zero"));
    }
    if !text.bytes().all(|c| c.is_ascii_digit()) {
        return Err(Error::Format("int"));
    }
    text.parse().map_err(|_| Error::Format("int"))
}

fn parse_strict_positive(text: &str) -> Result<u32, Error> {
    let v = parse_strict_non_negative(text)?;
    if v == 0 {
        return Err(Error::Format("zero"));
    }
    Ok(v)
}

fn b64(data: &[u8]) -> String {
    const T: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut out = String::new();
    let mut i = 0;
    while i + 3 <= data.len() {
        let n = ((data[i] as u32) << 16) | ((data[i + 1] as u32) << 8) | (data[i + 2] as u32);
        out.push(T[((n >> 18) & 63) as usize] as char);
        out.push(T[((n >> 12) & 63) as usize] as char);
        out.push(T[((n >> 6) & 63) as usize] as char);
        out.push(T[(n & 63) as usize] as char);
        i += 3;
    }
    let rem = data.len() - i;
    if rem == 1 {
        let n = (data[i] as u32) << 16;
        out.push(T[((n >> 18) & 63) as usize] as char);
        out.push(T[((n >> 12) & 63) as usize] as char);
    } else if rem == 2 {
        let n = ((data[i] as u32) << 16) | ((data[i + 1] as u32) << 8);
        out.push(T[((n >> 18) & 63) as usize] as char);
        out.push(T[((n >> 12) & 63) as usize] as char);
        out.push(T[((n >> 6) & 63) as usize] as char);
    }
    out
}

fn b64_decode(text: &str) -> Result<Vec<u8>, Error> {
    if text.is_empty() || text.contains('=') || text.chars().any(|c| c.is_whitespace()) {
        return Err(Error::Format("base64"));
    }
    if !text
        .bytes()
        .all(|c| matches!(c, b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'+' | b'/'))
    {
        return Err(Error::Format("base64"));
    }
    let mut padded = text.to_string();
    match padded.len() % 4 {
        0 => {}
        2 => padded.push_str("=="),
        3 => padded.push('='),
        _ => return Err(Error::Format("base64")),
    }
    fn val(c: u8) -> Result<u8, Error> {
        match c {
            b'A'..=b'Z' => Ok(c - b'A'),
            b'a'..=b'z' => Ok(c - b'a' + 26),
            b'0'..=b'9' => Ok(c - b'0' + 52),
            b'+' => Ok(62),
            b'/' => Ok(63),
            b'=' => Ok(0),
            _ => Err(Error::Format("base64")),
        }
    }
    let bytes = padded.as_bytes();
    let mut out = Vec::new();
    let mut i = 0;
    while i < bytes.len() {
        let a = val(bytes[i])?;
        let b = val(bytes[i + 1])?;
        let c = val(bytes[i + 2])?;
        let d = val(bytes[i + 3])?;
        let n = ((a as u32) << 18) | ((b as u32) << 12) | ((c as u32) << 6) | (d as u32);
        out.push((n >> 16) as u8);
        if bytes[i + 2] != b'=' {
            out.push((n >> 8) as u8);
        }
        if bytes[i + 3] != b'=' {
            out.push(n as u8);
        }
        i += 4;
    }
    // Canonical check
    if b64(&out) != text {
        return Err(Error::Format("non-canonical base64"));
    }
    Ok(out)
}
