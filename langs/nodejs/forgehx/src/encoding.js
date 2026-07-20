// Canonical `$forgehx$v=0$...` encoding / parsing.

import {
  Params,
  MIN_OUTPUT_LENGTH,
  MAX_OUTPUT_LENGTH,
} from "./params.js";

export const ALGORITHM_ID = "forgehx";
export const VERSION = 0;

/** Unpadded RFC 4648 Base64 encoding of a Buffer. */
export function b64Encode(buf) {
  return buf.toString("base64").replace(/=+$/, "");
}

/** Decode unpadded RFC 4648 Base64, rejecting non-canonical / malformed input. */
export function b64Decode(text) {
  if (text.length === 0) {
    throw new Error("ForgeHash-X: empty base64 field");
  }
  if (/[^A-Za-z0-9+/]/.test(text)) {
    throw new Error("ForgeHash-X: malformed base64");
  }
  const rem = text.length % 4;
  if (rem === 1) {
    throw new Error("ForgeHash-X: malformed base64 length");
  }
  let padded = text;
  if (rem === 2) padded += "==";
  else if (rem === 3) padded += "=";

  const decoded = Buffer.from(padded, "base64");
  if (b64Encode(decoded) !== text) {
    throw new Error("ForgeHash-X: non-canonical base64");
  }
  return decoded;
}

/**
 * Build the canonical encoded representation.
 * `$forgehx$v=0$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>`
 */
export function encode(params, salt, hash) {
  return `$${ALGORITHM_ID}$v=${VERSION}$m=${params.memoryKiB},t=${params.iterations},p=${params.parallelism}$${b64Encode(
    salt
  )}$${b64Encode(hash)}`;
}

function parseStrictNonNegativeInt(text) {
  if (
    text.length === 0 ||
    text.startsWith("+") ||
    text.startsWith("-") ||
    !/^[0-9]+$/.test(text)
  ) {
    throw new Error("ForgeHash-X: invalid integer field");
  }
  if (text.length > 1 && text.startsWith("0")) {
    throw new Error("ForgeHash-X: leading zero not allowed");
  }
  const value = Number(text);
  if (!Number.isSafeInteger(value) || value < 0) {
    throw new Error("ForgeHash-X: integer field out of range");
  }
  return value;
}

function parseStrictPositiveInt(text) {
  const value = parseStrictNonNegativeInt(text);
  if (value === 0) {
    throw new Error("ForgeHash-X: integer field out of range");
  }
  return value;
}

function parseCostField(segment, name) {
  const eq = segment.indexOf("=");
  if (eq === -1) {
    throw new Error("ForgeHash-X: malformed cost field");
  }
  const key = segment.slice(0, eq);
  const value = segment.slice(eq + 1);
  if (key !== name) {
    throw new Error(`ForgeHash-X: expected field '${name}'`);
  }
  return parseStrictPositiveInt(value);
}

/**
 * Parse a canonical encoded ForgeHash-X string. Version 0 is allowed as a
 * single digit (`v=0`).
 */
export function parse(encoded) {
  if (typeof encoded !== "string") {
    throw new Error("ForgeHash-X: encoded hash must be a string");
  }
  if (encoded.includes("\0") || /\s/.test(encoded)) {
    throw new Error("ForgeHash-X: whitespace or null byte in encoded hash");
  }

  const parts = encoded.split("$");
  if (parts.length !== 6 || parts[0] !== "") {
    throw new Error("ForgeHash-X: malformed encoded hash");
  }

  const [, algorithm, versionField, costsField, saltField, hashField] = parts;
  if (algorithm !== ALGORITHM_ID) {
    throw new Error("ForgeHash-X: unsupported algorithm identifier");
  }
  if (!versionField.startsWith("v=")) {
    throw new Error("ForgeHash-X: malformed version field");
  }
  const version = parseStrictNonNegativeInt(versionField.slice(2));
  if (version !== VERSION) {
    throw new Error("ForgeHash-X: unsupported algorithm version");
  }

  const costSegments = costsField.split(",");
  if (costSegments.length !== 3) {
    throw new Error("ForgeHash-X: malformed cost parameters");
  }
  const memoryKiB = parseCostField(costSegments[0], "m");
  const iterations = parseCostField(costSegments[1], "t");
  const parallelism = parseCostField(costSegments[2], "p");

  const salt = b64Decode(saltField);
  const hash = b64Decode(hashField);
  if (hash.length < MIN_OUTPUT_LENGTH || hash.length > MAX_OUTPUT_LENGTH) {
    throw new Error("ForgeHash-X: invalid hash length");
  }

  const params = new Params({
    memoryKiB,
    iterations,
    parallelism,
    outputLength: hash.length,
    saltLength: salt.length,
  });
  params.validate();

  const canonical = encode(params, salt, hash);
  if (canonical !== encoded) {
    throw new Error("ForgeHash-X: encoded hash is not canonical");
  }

  return { version, params, salt, hash, encoded: canonical };
}
