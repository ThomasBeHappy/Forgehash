// @forgeh/forgeh — experimental ForgeHash-B3 v1 reference implementation.
//
// *** WARNING ***
// ForgeHash is experimental cryptographic software. It has not received
// sufficient independent review and must not be used to protect production
// credentials or other sensitive data. See SPECIFICATION.md for details.

import { randomBytes, timingSafeEqual } from "node:crypto";
import { Params } from "./params.js";
import { deriveHash, deriveSeed } from "./engine.js";
import { encode, parse } from "./encoding.js";

export { Params } from "./params.js";
export { encode, parse } from "./encoding.js";
export { deriveHash, deriveSeed } from "./engine.js";

/** Coerce a string or Buffer/Uint8Array password/salt input into a Buffer (UTF-8 for strings). */
function toBuffer(value, label) {
  if (Buffer.isBuffer(value)) return value;
  if (value instanceof Uint8Array) return Buffer.from(value);
  if (typeof value === "string") return Buffer.from(value, "utf8");
  throw new TypeError(`ForgeHash: ${label} must be a string, Buffer, or Uint8Array`);
}

/**
 * Hash a password with a fresh cryptographically random salt, returning the
 * canonical encoded ForgeHash-B3 v1 string.
 *
 * @param {string|Buffer|Uint8Array} password
 * @param {Params} [params] defaults to Params.interactive()
 * @returns {string} canonical `$forgeh$v=1$...` encoded hash
 */
export function hashPassword(password, params = Params.interactive()) {
  params.validate();
  const passwordBuf = toBuffer(password, "password");
  const salt = randomBytes(params.saltLength);
  const hash = deriveHash(passwordBuf, salt, params);
  return encode(1, params, salt, hash);
}

/**
 * Verify a password against a canonical encoded ForgeHash-B3 hash using a
 * constant-time comparison. Returns `false` for any malformed hash, mismatched
 * parameters, or incorrect password — never throws for attacker-controlled input.
 *
 * @param {string|Buffer|Uint8Array} password
 * @param {string} encodedHash
 * @returns {boolean}
 */
export function verifyPassword(password, encodedHash) {
  let parsed;
  try {
    parsed = parse(encodedHash);
  } catch {
    return false;
  }

  let passwordBuf;
  try {
    passwordBuf = toBuffer(password, "password");
  } catch {
    return false;
  }

  let actual;
  try {
    actual = deriveHash(passwordBuf, parsed.salt, parsed.params);
  } catch {
    return false;
  }

  if (actual.length !== parsed.hash.length) return false;
  return timingSafeEqual(actual, parsed.hash);
}

/**
 * Returns true if the stored encoded hash should be regenerated under the
 * given desired parameters (outdated version, weaker cost parameters, or a
 * non-canonical encoding). Never returns true merely because stored
 * parameters exceed the desired policy.
 *
 * @param {string} encodedHash
 * @param {Params} desiredParams
 * @returns {boolean}
 */
export function needsRehash(encodedHash, desiredParams) {
  let parsed;
  try {
    parsed = parse(encodedHash);
  } catch {
    return true;
  }
  if (parsed.version !== 1) return true;
  if (parsed.memoryKiB < desiredParams.memoryKiB) return true;
  if (parsed.iterations < desiredParams.iterations) return true;
  if (parsed.parallelism !== desiredParams.parallelism) return true;
  if (parsed.hash.length !== desiredParams.outputLength) return true;
  if (parsed.salt.length < desiredParams.saltLength) return true;
  return false;
}
