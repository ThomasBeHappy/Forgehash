// ForgeHash-B3 v1 — ForgeMix compression function.
//
// This module implements section 14 of SPECIFICATION.md exactly:
// eight full rounds of row/column/diagonal quarter-round mixing, a fixed
// word permutation, and a feed-forward XOR. All arithmetic wraps modulo
// 2**64, matching the C#/Rust reference implementations bit-for-bit.

export const BLOCK_SIZE = 1024;
export const WORDS = 128;
export const ROUNDS = 8;

const MASK64 = (1n << 64n) - 1n;
const MASK32 = 0xffffffffn;

/** Lower 32 bits of a 64-bit value, as an unsigned BigInt. */
function low32(x) {
  return x & MASK32;
}

/** Rotate a 64-bit unsigned value left by `n` bits (0 <= n < 64). */
export function rotl(x, n) {
  const bn = BigInt(n);
  return ((x << bn) | (x >> (64n - bn))) & MASK64;
}

/** Rotate a 64-bit unsigned value right by `n` bits (0 <= n < 64). */
export function rotr(x, n) {
  const bn = BigInt(n);
  return ((x >> bn) | (x << (64n - bn))) & MASK64;
}

/**
 * ForgeMix quarter round. Mutates `words[base + i0..i3]` in place.
 *
 * a = a + b + 2 * Low32(a) * Low32(b)
 * d = RotateRight64(d XOR a, 32)
 * c = c + d + 2 * Low32(c) * Low32(d)
 * b = RotateRight64(b XOR c, 24)
 * a = a + b + 2 * Low32(a) * Low32(b)
 * d = RotateRight64(d XOR a, 16)
 * c = c + d + 2 * Low32(c) * Low32(d)
 * b = RotateRight64(b XOR c, 63)
 */
function quarterRound(words, base, i0, i1, i2, i3) {
  let a = words[base + i0];
  let b = words[base + i1];
  let c = words[base + i2];
  let d = words[base + i3];

  a = (a + b + 2n * low32(a) * low32(b)) & MASK64;
  d = rotr(d ^ a, 32);

  c = (c + d + 2n * low32(c) * low32(d)) & MASK64;
  b = rotr(b ^ c, 24);

  a = (a + b + 2n * low32(a) * low32(b)) & MASK64;
  d = rotr(d ^ a, 16);

  c = (c + d + 2n * low32(c) * low32(d)) & MASK64;
  b = rotr(b ^ c, 63);

  words[base + i0] = a;
  words[base + i1] = b;
  words[base + i2] = c;
  words[base + i3] = d;
}

/** The fixed eight quarter-round schedule shared by row/column/diagonal mixing. */
function applySchedule(words, base) {
  quarterRound(words, base, 0, 4, 8, 12);
  quarterRound(words, base, 1, 5, 9, 13);
  quarterRound(words, base, 2, 6, 10, 14);
  quarterRound(words, base, 3, 7, 11, 15);
  quarterRound(words, base, 0, 5, 10, 15);
  quarterRound(words, base, 1, 6, 11, 12);
  quarterRound(words, base, 2, 7, 8, 13);
  quarterRound(words, base, 3, 4, 9, 14);
}

function idx(row, col) {
  return row * 16 + col;
}

function mixRows(state) {
  for (let row = 0; row < 8; row++) {
    applySchedule(state, row * 16);
  }
}

// Scratch buffers reused across calls to avoid per-round allocation.
const virtualRow = new Array(16);
const group = new Array(16);

function mixColumns(state) {
  for (let pair = 0; pair < 8; pair++) {
    const colA = pair * 2;
    const colB = colA + 1;
    for (let row = 0; row < 8; row++) {
      virtualRow[row * 2] = state[idx(row, colA)];
      virtualRow[row * 2 + 1] = state[idx(row, colB)];
    }
    applySchedule(virtualRow, 0);
    for (let row = 0; row < 8; row++) {
      state[idx(row, colA)] = virtualRow[row * 2];
      state[idx(row, colB)] = virtualRow[row * 2 + 1];
    }
  }
}

function mixDiagonals(state) {
  for (let diagonalIndex = 0; diagonalIndex < 8; diagonalIndex++) {
    const base = diagonalIndex * 2;
    for (let k = 0; k < 8; k++) {
      group[k * 2] = state[idx(k, (base + k) & 15)];
      group[k * 2 + 1] = state[idx(k, (base + k + 8) & 15)];
    }
    applySchedule(group, 0);
    for (let k = 0; k < 8; k++) {
      state[idx(k, (base + k) & 15)] = group[k * 2];
      state[idx(k, (base + k + 8) & 15)] = group[k * 2 + 1];
    }
  }
}

/** destinationIndex = (sourceIndex * 73 + 19) mod 128 */
function permute(state, permuted) {
  for (let source = 0; source < WORDS; source++) {
    const dest = (source * 73 + 19) & 127;
    permuted[dest] = state[source];
  }
}

/**
 * ForgeMix(inputBlock, pass, lane, blockIndex) -> outputBlock
 *
 * `input` must be an array of 128 BigInt words (a 1024-byte block).
 * `pass`, `lane`, `blockIndex` are BigInt.
 * Writes the result into `output` (array of 128 BigInt) and returns it.
 */
export function mix(input, pass, lane, blockIndex, output) {
  const original = input;
  let state = new Array(WORDS);
  for (let i = 0; i < WORDS; i++) state[i] = input[i];

  state[0] ^= pass;
  state[1] ^= lane;
  state[2] ^= blockIndex;
  state[3] ^= rotl((pass + blockIndex) & MASK64, 17);

  let permuted = new Array(WORDS);
  for (let round = 0; round < ROUNDS; round++) {
    mixRows(state);
    mixColumns(state);
    mixDiagonals(state);
    permute(state, permuted);
    const tmp = state;
    state = permuted;
    permuted = tmp;
  }

  const out = output || new Array(WORDS);
  for (let i = 0; i < WORDS; i++) {
    out[i] = state[i] ^ original[i];
  }
  return out;
}

/** Interpret a 1024-byte Buffer as 128 little-endian u64 words. */
export function bytesToWords(bytes, out) {
  const words = out || new Array(WORDS);
  for (let i = 0; i < WORDS; i++) {
    words[i] = bytes.readBigUInt64LE(i * 8);
  }
  return words;
}

/** Serialize 128 little-endian u64 words into a 1024-byte Buffer. */
export function wordsToBytes(words, out) {
  const bytes = out || Buffer.alloc(BLOCK_SIZE);
  for (let i = 0; i < WORDS; i++) {
    bytes.writeBigUInt64LE(words[i] & MASK64, i * 8);
  }
  return bytes;
}

/** dest = left XOR right, word-wise. */
export function xorWords(left, right, out) {
  const dest = out || new Array(WORDS);
  for (let i = 0; i < WORDS; i++) {
    dest[i] = left[i] ^ right[i];
  }
  return dest;
}

export { MASK64 };
