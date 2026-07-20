// ForgeHash-B3 v1 — canonical encoded hash format (SPECIFICATION.md §6):
//
//   $forgeh$v=1$m=<memoryKiB>,t=<iterations>,p=<parallelism>$<salt-b64>$<hash-b64>
//
// Base64 is RFC 4648 standard alphabet without padding.

import { Params, DEFAULT_OUTPUT_LENGTH } from "./params.js";

/** Unpadded RFC 4648 Base64 encoding of a Buffer. */
function b64Encode(buf) {
  return buf.toString("base64").replace(/=+$/, "");
}

/** Decode unpadded RFC 4648 Base64, rejecting non-canonical / malformed input. */
function b64Decode(text) {
  if (text.length === 0) {
    throw new Error("ForgeHash: empty base64 field");
  }
  if (/[^A-Za-z0-9+/]/.test(text)) {
    throw new Error("ForgeHash: malformed base64");
  }
  const rem = text.length % 4;
  let padded = text;
  if (rem === 2) padded += "==";
  else if (rem === 3) padded += "=";
  else if (rem === 1) throw new Error("ForgeHash: malformed base64 length");

  const decoded = Buffer.from(padded, "base64");
  // Reject inputs that Node's lenient base64 decoder silently accepted but
  // which do not round-trip to the same canonical text (e.g. stray bits).
  if (b64Encode(decoded) !== text) {
    throw new Error("ForgeHash: non-canonical base64");
  }
  return decoded;
}

/**
 * Build the canonical encoded representation for a given algorithm
 * version, parameters, salt, and hash.
 */
export function encode(version, params, salt, hash) {
  return `$forgeh$v=${version}$m=${params.memoryKiB},t=${params.iterations},p=${params.parallelism}$${b64Encode(
    salt
  )}$${b64Encode(hash)}`;
}

function parseStrictPositiveInt(text) {
  if (
    text.length === 0 ||
    text.startsWith("+") ||
    text.startsWith("-") ||
    !/^[0-9]+$/.test(text)
  ) {
    throw new Error("ForgeHash: invalid integer field");
  }
  if (text.length > 1 && text.startsWith("0")) {
    throw new Error("ForgeHash: leading zero not allowed");
  }
  const value = Number(text);
  if (!Number.isSafeInteger(value) || value === 0) {
    throw new Error("ForgeHash: integer field out of range");
  }
  return value;
}

function parseCostField(segment, name) {
  const eq = segment.indexOf("=");
  if (eq === -1) {
    throw new Error("ForgeHash: malformed cost field");
  }
  const key = segment.slice(0, eq);
  const value = segment.slice(eq + 1);
  if (key !== name) {
    throw new Error(`ForgeHash: expected field '${name}'`);
  }
  return parseStrictPositiveInt(value);
}

/**
 * Parse a canonical encoded ForgeHash string. Throws on any malformed,
 * duplicate, reordered, or non-canonical input. Performs no memory
 * allocation or hashing.
 */
export function parse(encoded) {
  if (typeof encoded !== "string") {
    throw new Error("ForgeHash: encoded hash must be a string");
  }
  if (encoded.includes("\0") || /\s/.test(encoded)) {
    throw new Error("ForgeHash: whitespace or null byte in encoded hash");
  }
  const parts = encoded.split("$");
  if (parts.length !== 6 || parts[0] !== "") {
    throw new Error("ForgeHash: malformed encoded hash");
  }
  const [, algorithm, versionField, costsField, saltField, hashField] = parts;

  if (algorithm !== "forgeh") {
    throw new Error("ForgeHash: unsupported algorithm identifier");
  }
  if (!versionField.startsWith("v=")) {
    throw new Error("ForgeHash: malformed version field");
  }
  const version = parseStrictPositiveInt(versionField.slice(2));
  if (version !== 1) {
    throw new Error("ForgeHash: unsupported algorithm version");
  }

  const costSegments = costsField.split(",");
  if (costSegments.length !== 3) {
    throw new Error("ForgeHash: malformed cost parameters");
  }
  const memoryKiB = parseCostField(costSegments[0], "m");
  const iterations = parseCostField(costSegments[1], "t");
  const parallelism = parseCostField(costSegments[2], "p");

  const salt = b64Decode(saltField);
  const hash = b64Decode(hashField);
  if (hash.length !== DEFAULT_OUTPUT_LENGTH) {
    throw new Error("ForgeHash: invalid hash length");
  }

  const params = new Params({
    memoryKiB,
    iterations,
    parallelism,
    outputLength: hash.length,
    saltLength: salt.length,
  });
  // validate() enforces the same policy limits as the encoder/parser tests.
  params.validate();

  const canonical = encode(version, params, salt, hash);
  if (canonical !== encoded) {
    throw new Error("ForgeHash: encoded hash is not canonical");
  }

  return { version, memoryKiB, iterations, parallelism, salt, hash, params, encoded: canonical };
}
