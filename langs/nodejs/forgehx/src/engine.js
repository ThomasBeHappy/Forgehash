// ForgeHash-X v0 memory-hard engine (SPECIFICATION_X §§6–12).

import { rotl, permute } from "./forgeperm.js";
import { ForgeX } from "./forgex.js";
import {
  BLOCK_SIZE,
  WORDS_PER_BLOCK,
  MIN_SALT_LENGTH,
  MAX_SALT_LENGTH,
} from "./params.js";

const TAG_SEED = "ForgeX/v0/seed";
const TAG_EXPAND = "ForgeX/v0/expand";
const TAG_FINAL = "ForgeX/v0/final";
const TAG_OUTPUT = "ForgeX/v0/output";

const MASK64 = 0xffffffffffffffffn;

function le32(v) {
  const b = Buffer.alloc(4);
  b.writeUInt32LE(v >>> 0, 0);
  return b;
}

function le64(v) {
  const b = Buffer.alloc(8);
  b.writeBigUInt64LE(BigInt(v) & MASK64, 0);
  return b;
}

/** FastRange(x, n) = High64(x * n) using a 128-bit intermediate product. */
export function fastRange(x, n) {
  return ((x & MASK64) * (n & MASK64)) >> 64n;
}

function validateSalt(salt) {
  if (salt.length < MIN_SALT_LENGTH || salt.length > MAX_SALT_LENGTH) {
    throw new RangeError("ForgeHash-X: salt length out of range");
  }
}

function buildMaterial(password, salt, params) {
  return Buffer.concat([
    le32(0),
    le32(params.memoryKiB),
    le32(params.iterations),
    le32(params.parallelism),
    le32(params.outputLength),
    le64(password.length),
    password,
    le32(salt.length),
    salt,
  ]);
}

export function deriveSeed(password, salt, params) {
  params.validate();
  validateSalt(salt);
  return ForgeX.hash(TAG_SEED, buildMaterial(password, salt, params));
}

function blockOffset(lane, index, blocksPerLane) {
  return (lane * blocksPerLane + index) * WORDS_PER_BLOCK;
}

function getBlock(memory, lane, index, blocksPerLane) {
  const start = blockOffset(lane, index, blocksPerLane);
  return memory.slice(start, start + WORDS_PER_BLOCK);
}

function setBlock(memory, lane, index, blocksPerLane, words) {
  const start = blockOffset(lane, index, blocksPerLane);
  for (let i = 0; i < WORDS_PER_BLOCK; i++) {
    memory[start + i] = words[i];
  }
}

function bytesToWords(data) {
  const words = new Array(data.length / 8);
  for (let i = 0; i < words.length; i++) {
    words[i] = data.readBigUInt64LE(i * 8);
  }
  return words;
}

function wordsToBytes(words) {
  const buf = Buffer.alloc(words.length * 8);
  for (let i = 0; i < words.length; i++) {
    buf.writeBigUInt64LE(words[i] & MASK64, i * 8);
  }
  return buf;
}

function selectReference(
  previous,
  pass,
  slice,
  currentLane,
  blockIndex,
  parallelism,
  blocksPerLane,
  sliceLength
) {
  const addressWord =
    (previous[0] ^
      rotl(previous[9], 19) ^
      previous[31] ^
      BigInt(pass >>> 0) ^
      rotl(BigInt(blockIndex >>> 0), 11)) &
    MASK64;

  let lane;
  if (parallelism === 1) {
    lane = 0;
  } else if (blockIndex % 16 === 0) {
    lane = Number(fastRange(previous[1], BigInt(parallelism)));
  } else {
    lane = currentLane;
  }

  function allowed(refLane) {
    if (refLane === currentLane) {
      return pass === 0 ? blockIndex : blocksPerLane;
    }
    return slice * sliceLength;
  }

  let a = allowed(lane);
  if (a === 0) {
    lane = currentLane;
    a = allowed(lane);
  }
  return [lane, Number(fastRange(addressWord, BigInt(a)))];
}

function blockMix(inp, pass, lane, blockIndex) {
  const out = new Array(WORDS_PER_BLOCK);
  for (let k = 0; k < 4; k++) {
    const chunk = inp.slice(k * 16, (k + 1) * 16);
    const state = chunk.slice();
    state[0] ^= BigInt(pass) & MASK64;
    state[1] ^= BigInt(lane) & MASK64;
    state[2] ^= BigInt(blockIndex) & MASK64;
    state[3] ^= rotl(BigInt(pass + blockIndex + k) & MASK64, 13);
    permute(state);
    for (let i = 0; i < 16; i++) {
      out[k * 16 + i] = (state[i] ^ chunk[i]) & MASK64;
    }
  }
  return out;
}

function processLaneSlice(
  memory,
  pass,
  slice,
  lane,
  parallelism,
  blocksPerLane,
  sliceLength
) {
  let start = slice * sliceLength;
  const end = start + sliceLength;
  if (pass === 0 && slice === 0) {
    start = 2;
  }
  for (let blockIndex = start; blockIndex < end; blockIndex++) {
    const previousIndex = blockIndex > 0 ? blockIndex - 1 : blocksPerLane - 1;
    const prev = getBlock(memory, lane, previousIndex, blocksPerLane);
    const [refLane, refIndex] = selectReference(
      prev,
      pass,
      slice,
      lane,
      blockIndex,
      parallelism,
      blocksPerLane,
      sliceLength
    );
    const reference = getBlock(memory, refLane, refIndex, blocksPerLane);
    const combined = new Array(WORDS_PER_BLOCK);
    for (let w = 0; w < WORDS_PER_BLOCK; w++) {
      combined[w] = (prev[w] ^ reference[w]) & MASK64;
    }
    const mixed = blockMix(combined, pass, lane, blockIndex);
    if (pass === 0) {
      setBlock(memory, lane, blockIndex, blocksPerLane, mixed);
    } else {
      const cur = getBlock(memory, lane, blockIndex, blocksPerLane);
      const xored = new Array(WORDS_PER_BLOCK);
      for (let w = 0; w < WORDS_PER_BLOCK; w++) {
        xored[w] = (cur[w] ^ mixed[w]) & MASK64;
      }
      setBlock(memory, lane, blockIndex, blocksPerLane, xored);
    }
  }
}

export function deriveHash(password, salt, params) {
  params.validate();
  validateSalt(salt);

  const seed = ForgeX.hash(TAG_SEED, buildMaterial(password, salt, params));
  const parallelism = params.parallelism;
  const blocksPerLane = params.blocksPerLane;
  const sliceLength = params.sliceLength;
  const memory = new Array(params.blockCount * WORDS_PER_BLOCK).fill(0n);

  for (let lane = 0; lane < parallelism; lane++) {
    for (let i = 0; i < 2; i++) {
      const expandInput = Buffer.concat([seed, le32(lane), le32(i)]);
      const blockBytes = ForgeX.xof(TAG_EXPAND, expandInput, BLOCK_SIZE);
      setBlock(memory, lane, i, blocksPerLane, bytesToWords(blockBytes));
    }
  }

  for (let pass = 0; pass < params.iterations; pass++) {
    for (let slice = 0; slice < 4; slice++) {
      for (let lane = 0; lane < parallelism; lane++) {
        processLaneSlice(
          memory,
          pass,
          slice,
          lane,
          parallelism,
          blocksPerLane,
          sliceLength
        );
      }
    }
  }

  const fold = new Array(WORDS_PER_BLOCK).fill(0n);
  const last = blocksPerLane - 1;
  const q1 = Math.floor(blocksPerLane / 4);
  const q2 = Math.floor(blocksPerLane / 2);
  const q3 = Math.floor((blocksPerLane * 3) / 4);
  for (let lane = 0; lane < parallelism; lane++) {
    for (const index of [last, q1, q2, q3]) {
      const blk = getBlock(memory, lane, index, blocksPerLane);
      for (let w = 0; w < WORDS_PER_BLOCK; w++) {
        fold[w] ^= blk[w];
      }
    }
  }

  const foldBytes = wordsToBytes(fold);
  const finalInput = Buffer.concat([
    seed,
    foldBytes,
    le32(params.memoryKiB),
    le32(params.iterations),
    le32(params.parallelism),
    le32(params.outputLength),
  ]);
  const root = ForgeX.hash(TAG_FINAL, finalInput);
  return ForgeX.xof(TAG_OUTPUT, Buffer.concat([root, seed]), params.outputLength);
}
