// forgehx — experimental ForgeHash-X v0 reference implementation.
//
// *** WARNING ***
// Experimental research software. Not for production password storage.
// Not compatible with ForgeHash-B3. Custom ForgeX sponge — no BLAKE3.

import { randomBytes, timingSafeEqual } from "node:crypto";
import { Params } from "./params.js";
import { deriveHash, deriveSeed } from "./engine.js";
import { encode, parse } from "./encoding.js";

export { Params } from "./params.js";
export { encode, parse } from "./encoding.js";
export { deriveHash, deriveSeed } from "./engine.js";

function toBuffer(value, label) {
  if (Buffer.isBuffer(value)) return value;
  if (value instanceof Uint8Array) return Buffer.from(value);
  if (typeof value === "string") return Buffer.from(value, "utf8");
  throw new TypeError(`ForgeHash-X: ${label} must be a string, Buffer, or Uint8Array`);
}

/**
 * Hash a password with a fresh random salt, returning the canonical
 * `$forgehx$v=0$...` encoded string.
 */
export function hashPassword(password, params = Params.toy()) {
  params.validate();
  const passwordBuf = toBuffer(password, "password");
  const salt = randomBytes(params.saltLength);
  const hash = deriveHash(passwordBuf, salt, params);
  return encode(params, salt, hash);
}

/**
 * Verify a password against a canonical encoded ForgeHash-X hash using a
 * constant-time comparison. Returns false for any malformed / mismatched input.
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
