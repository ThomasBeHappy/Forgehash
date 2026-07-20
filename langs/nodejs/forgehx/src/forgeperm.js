// ForgePerm — 16-word ARX permutation (SPECIFICATION_X §4.3).

export const WORDS = 16;
export const ROUNDS = 8;

const MASK64 = 0xffffffffffffffffn;

export function rotl(x, n) {
  n &= 63;
  x &= MASK64;
  if (n === 0) return x;
  return ((x << BigInt(n)) | (x >> BigInt(64 - n))) & MASK64;
}

export function rotr(x, n) {
  n &= 63;
  x &= MASK64;
  if (n === 0) return x;
  return ((x >> BigInt(n)) | (x << BigInt(64 - n))) & MASK64;
}

function low32(x) {
  return x & 0xffffffffn;
}

export function roundConstant(round, index) {
  const x =
    (0x9e3779b97f4a7c15n ^
      (BigInt(round >>> 0) * 0xd1b54a32d192ed03n) ^
      (BigInt(index >>> 0) * 0xa24baed4963ee407n)) &
    MASK64;
  return rotl(x, (round + index * 3) & 63);
}

function quarterRound(s, ia, ib, ic, id) {
  let a = s[ia];
  let b = s[ib];
  let c = s[ic];
  let d = s[id];
  a = (a + b + 2n * low32(a) * low32(b)) & MASK64;
  d = rotr(d ^ a, 17);
  c = (c + d + 2n * low32(c) * low32(d)) & MASK64;
  b = rotr(b ^ c, 11);
  a = (a + b + 2n * low32(a) * low32(b)) & MASK64;
  d = rotr(d ^ a, 23);
  c = (c + d + 2n * low32(c) * low32(d)) & MASK64;
  b = rotr(b ^ c, 41);
  s[ia] = a;
  s[ib] = b;
  s[ic] = c;
  s[id] = d;
}

/** In-place ForgePerm over a 16-element BigInt[] state. */
export function permute(state) {
  if (state.length !== WORDS) {
    throw new Error("ForgePerm: state must be 16 words");
  }
  const temp = new Array(WORDS);
  for (let r = 0; r < ROUNDS; r++) {
    for (let i = 0; i < WORDS; i++) {
      state[i] = (state[i] ^ roundConstant(r, i)) & MASK64;
    }
    quarterRound(state, 0, 4, 8, 12);
    quarterRound(state, 1, 5, 9, 13);
    quarterRound(state, 2, 6, 10, 14);
    quarterRound(state, 3, 7, 11, 15);
    quarterRound(state, 0, 5, 10, 15);
    quarterRound(state, 1, 6, 11, 12);
    quarterRound(state, 2, 7, 8, 13);
    quarterRound(state, 3, 4, 9, 14);
    for (let i = 0; i < WORDS; i++) {
      temp[(i * 7 + 3) & 15] = state[i];
    }
    for (let i = 0; i < WORDS; i++) {
      state[i] = temp[i];
    }
  }
}
