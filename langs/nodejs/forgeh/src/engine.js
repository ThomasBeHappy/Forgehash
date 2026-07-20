// ForgeHash-B3 v1 — core engine: seed derivation, memory initialization,
// memory filling, and finalization. Implements SPECIFICATION.md §§9-17.
//
// This is a deliberately single-threaded reference implementation, matching
// the C#/Rust reference engines word-for-word.

import * as blake3 from "blake3";
import {
  BLOCK_SIZE,
  WORDS,
  mix,
  bytesToWords,
  wordsToBytes,
  xorWords,
  rotl,
} from "./forgemix.js";
import { MAX_PASSWORD } from "./params.js";

const SEED_CTX = "ForgeHash/v1/seed";
const EXPAND_PREFIX = Buffer.from("ForgeHash/v1/expand", "utf8");
const GROUP_PREFIX = Buffer.from("ForgeHash/v1/group", "utf8");
const GROUP_ROOT_PREFIX = Buffer.from("ForgeHash/v1/group-root", "utf8");
const FINAL_PREFIX = Buffer.from("ForgeHash/v1/final", "utf8");
const OUTPUT_PREFIX = Buffer.from("ForgeHash/v1/output", "utf8");

const MASK64 = (1n << 64n) - 1n;

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

/**
 * Build the little-endian input buffer described in SPECIFICATION.md §9:
 *
 *   LE32(version=1) || LE32(memoryKiB) || LE32(iterations) || LE32(parallelism)
 *   || LE32(outputLength) || LE64(passwordLength) || password
 *   || LE32(saltLength) || salt || LE32(contextLength=0)
 */
function buildEncodedInput(password, salt, params) {
  return Buffer.concat([
    le32(1),
    le32(params.memoryKiB),
    le32(params.iterations),
    le32(params.parallelism),
    le32(params.outputLength),
    le64(password.length),
    password,
    le32(salt.length),
    salt,
    le32(0), // empty context, always present
  ]);
}

function deriveSeedBytes(material) {
  return blake3.deriveKey(SEED_CTX, material, { length: 32 });
}

/** Expand(input, outputLength) = BLAKE3-XOF("ForgeHash/v1/expand" || input) */
function expand(input, outLen) {
  const hasher = blake3.createHash();
  hasher.update(EXPAND_PREFIX);
  hasher.update(input);
  return hasher.digest(undefined, { length: outLen });
}

function blake3Hash(input) {
  return blake3.hash(input, { length: 32 });
}

function blake3Xof(input, outLen) {
  const hasher = blake3.createHash();
  hasher.update(input);
  return hasher.digest(undefined, { length: outLen });
}

/** FastRange(x, n) = High64(x * n) using a 128-bit intermediate product. */
function fastRange(x, n) {
  return (x * n) >> 64n;
}

class Memory {
  constructor(blockCount, parallelism) {
    this.parallelism = parallelism;
    this.blocksPerLane = blockCount / parallelism;
    this.sliceLength = this.blocksPerLane / 4;
    this.words = new Array(blockCount * WORDS).fill(0n);
  }

  flatOffset(lane, index) {
    return (lane * this.blocksPerLane + index) * WORDS;
  }

  getBlock(lane, index, out) {
    const start = this.flatOffset(lane, index);
    const dest = out || new Array(WORDS);
    for (let i = 0; i < WORDS; i++) dest[i] = this.words[start + i];
    return dest;
  }

  setBlock(lane, index, block) {
    const start = this.flatOffset(lane, index);
    for (let i = 0; i < WORDS; i++) this.words[start + i] = block[i];
  }
}

/**
 * Derive the ForgeHash initial seed (32 bytes) from a password, salt, and
 * parameters. Throws if the password or parameters are invalid.
 */
export function deriveSeed(password, salt, params) {
  if (password.length > MAX_PASSWORD) {
    throw new RangeError("ForgeHash: password too long");
  }
  params.validate();
  if (salt.length < 16 || salt.length > 64) {
    throw new RangeError("ForgeHash: salt length out of range");
  }
  const material = buildEncodedInput(password, salt, params);
  return deriveSeedBytes(material);
}

/**
 * Derive the full ForgeHash-B3 output (length `params.outputLength` bytes)
 * from a password, salt, and parameters. This performs the full
 * memory-hard computation and may take a significant amount of time and
 * memory depending on `params`.
 */
export function deriveHash(password, salt, params) {
  if (password.length > MAX_PASSWORD) {
    throw new RangeError("ForgeHash: password too long");
  }
  params.validate();
  if (salt.length < 16 || salt.length > 64) {
    throw new RangeError("ForgeHash: salt length out of range");
  }

  const material = buildEncodedInput(password, salt, params);
  const seed = deriveSeedBytes(material);

  const memory = new Memory(params.memoryKiB, params.parallelism);
  initializeMemory(memory, seed);
  fillMemory(memory, params.iterations);
  return finalize(memory, seed, params);
}

function initializeMemory(memory, seed) {
  for (let lane = 0; lane < memory.parallelism; lane++) {
    for (let blockIndex = 0; blockIndex < 2; blockIndex++) {
      const input = Buffer.concat([seed, le32(lane), le32(blockIndex)]);
      const blockBytes = expand(input, BLOCK_SIZE);
      const words = bytesToWords(blockBytes);
      memory.setBlock(lane, blockIndex, words);
    }
  }
}

function fillMemory(memory, iterations) {
  for (let pass = 0; pass < iterations; pass++) {
    for (let slice = 0; slice < 4; slice++) {
      for (let lane = 0; lane < memory.parallelism; lane++) {
        processSlice(memory, pass, slice, lane);
      }
    }
  }
}

// Scratch buffers reused across block iterations to avoid per-block allocation.
const scratchPrevious = new Array(WORDS);
const scratchReference = new Array(WORDS);
const scratchCombined = new Array(WORDS);
const scratchMixed = new Array(WORDS);
const scratchOldCurrent = new Array(WORDS);
const scratchOutput = new Array(WORDS);

function processSlice(memory, pass, slice, lane) {
  let start = slice * memory.sliceLength;
  const end = start + memory.sliceLength;
  if (pass === 0 && slice === 0) {
    start = 2;
  }

  for (let blockIndex = start; blockIndex < end; blockIndex++) {
    const previousIndex =
      blockIndex > 0 ? blockIndex - 1 : memory.blocksPerLane - 1;
    memory.getBlock(lane, previousIndex, scratchPrevious);

    const { referenceLane, referenceIndex } = selectReference(
      memory,
      pass,
      slice,
      lane,
      blockIndex,
      scratchPrevious
    );
    memory.getBlock(referenceLane, referenceIndex, scratchReference);
    xorWords(scratchPrevious, scratchReference, scratchCombined);

    const passBig = BigInt(pass);
    const laneBig = BigInt(lane);
    const blockIndexBig = BigInt(blockIndex);

    if (pass === 0) {
      mix(scratchCombined, passBig, laneBig, blockIndexBig, scratchOutput);
      memory.setBlock(lane, blockIndex, scratchOutput);
    } else {
      mix(scratchCombined, passBig, laneBig, blockIndexBig, scratchMixed);
      memory.getBlock(lane, blockIndex, scratchOldCurrent);
      xorWords(scratchOldCurrent, scratchMixed, scratchOutput);
      memory.setBlock(lane, blockIndex, scratchOutput);
    }
  }
}

function selectReference(memory, pass, slice, currentLane, blockIndex, previous) {
  const addressWord =
    (previous[0] ^
      rotl(previous[17], 13) ^
      previous[73] ^
      BigInt(pass) ^
      rotl(BigInt(blockIndex), 29)) &
    MASK64;

  let referenceLane;
  if (memory.parallelism === 1) {
    referenceLane = 0;
  } else if (blockIndex % 32 === 0) {
    referenceLane = Number(fastRange(previous[1], BigInt(memory.parallelism)));
  } else {
    referenceLane = currentLane;
  }

  let allowed = allowedBlockCount(
    memory,
    pass,
    slice,
    currentLane,
    referenceLane,
    blockIndex
  );
  if (allowed === 0) {
    referenceLane = currentLane;
    allowed = allowedBlockCount(
      memory,
      pass,
      slice,
      currentLane,
      referenceLane,
      blockIndex
    );
  }
  const referenceIndex = Number(fastRange(addressWord, BigInt(allowed)));
  return { referenceLane, referenceIndex };
}

function allowedBlockCount(memory, pass, slice, currentLane, referenceLane, blockIndex) {
  if (referenceLane === currentLane) {
    return pass === 0 ? blockIndex : memory.blocksPerLane;
  }
  return slice * memory.sliceLength;
}

function finalize(memory, seed, params) {
  const acc = new Array(WORDS).fill(0n);
  const last = memory.blocksPerLane - 1;
  const quarter = Math.floor(memory.blocksPerLane / 4);
  const half = Math.floor(memory.blocksPerLane / 2);
  const threeQuarter = Math.floor((memory.blocksPerLane * 3) / 4);
  const tmp = new Array(WORDS);

  for (let lane = 0; lane < memory.parallelism; lane++) {
    for (const index of [last, quarter, half, threeQuarter]) {
      memory.getBlock(lane, index, tmp);
      for (let i = 0; i < WORDS; i++) acc[i] ^= tmp[i];
    }
  }

  const accumulatorBytes = wordsToBytes(acc);
  const groupRoot = computeGroupRoot(memory);

  const rootInput = Buffer.concat([
    FINAL_PREFIX,
    seed,
    accumulatorBytes,
    groupRoot,
    le32(params.memoryKiB),
    le32(params.iterations),
    le32(params.parallelism),
    le32(params.outputLength),
  ]);
  const root = blake3Hash(rootInput);

  const outputInput = Buffer.concat([OUTPUT_PREFIX, root, seed]);
  return blake3Xof(outputInput, params.outputLength);
}

function computeGroupRoot(memory) {
  const rootHasher = blake3.createHash();
  rootHasher.update(GROUP_ROOT_PREFIX);

  const GROUP = 64;
  const total = memory.parallelism * memory.blocksPerLane;
  const groupCount = Math.ceil(total / GROUP);
  const groupBuf = Buffer.alloc(GROUP * BLOCK_SIZE);
  const words = new Array(WORDS);
  const blockBytes = Buffer.alloc(BLOCK_SIZE);

  for (let groupIndex = 0; groupIndex < groupCount; groupIndex++) {
    const start = groupIndex * GROUP;
    const count = Math.min(total - start, GROUP);
    for (let i = 0; i < count; i++) {
      const flat = start + i;
      const lane = Math.floor(flat / memory.blocksPerLane);
      const blockIndex = flat % memory.blocksPerLane;
      memory.getBlock(lane, blockIndex, words);
      wordsToBytes(words, blockBytes);
      blockBytes.copy(groupBuf, i * BLOCK_SIZE);
    }
    const byteLen = count * BLOCK_SIZE;

    const groupHasher = blake3.createHash();
    groupHasher.update(GROUP_PREFIX);
    groupHasher.update(le64(groupIndex));
    groupHasher.update(le64(count));
    groupHasher.update(groupBuf.subarray(0, byteLen));
    const digest = groupHasher.digest(undefined, { length: 32 });

    rootHasher.update(le64(groupIndex));
    rootHasher.update(digest);
  }

  return rootHasher.digest(undefined, { length: 32 });
}
