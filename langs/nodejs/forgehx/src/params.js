// Sandbox parameters for ForgeHash-X v0.

export const BLOCK_SIZE = 512;
export const WORDS_PER_BLOCK = BLOCK_SIZE / 8;
export const MIN_MEMORY_KIB = 256;
export const MAX_MEMORY_KIB = 65_536;
export const MIN_ITERATIONS = 1;
export const MAX_ITERATIONS = 8;
export const MIN_PARALLELISM = 1;
export const MAX_PARALLELISM = 16;
export const MIN_OUTPUT_LENGTH = 16;
export const MAX_OUTPUT_LENGTH = 64;
export const MIN_SALT_LENGTH = 16;
export const MAX_SALT_LENGTH = 64;
export const DEFAULT_OUTPUT_LENGTH = 32;

export class Params {
  constructor({
    memoryKiB = 1024,
    iterations = 1,
    parallelism = 1,
    outputLength = DEFAULT_OUTPUT_LENGTH,
    saltLength = 16,
  } = {}) {
    this.memoryKiB = memoryKiB;
    this.iterations = iterations;
    this.parallelism = parallelism;
    this.outputLength = outputLength;
    this.saltLength = saltLength;
  }

  /** Toy-vector profile used by implementers/x0 vectors. */
  static toy() {
    return new Params({
      memoryKiB: 1024,
      iterations: 1,
      parallelism: 1,
      outputLength: DEFAULT_OUTPUT_LENGTH,
      saltLength: 16,
    });
  }

  get blockCount() {
    return (this.memoryKiB * 1024) / BLOCK_SIZE;
  }

  get blocksPerLane() {
    return this.blockCount / this.parallelism;
  }

  get sliceLength() {
    return this.blocksPerLane / 4;
  }

  validate() {
    if (
      !Number.isInteger(this.memoryKiB) ||
      this.memoryKiB < MIN_MEMORY_KIB ||
      this.memoryKiB > MAX_MEMORY_KIB
    ) {
      throw new RangeError("ForgeHash-X: memoryKiB out of sandbox range");
    }
    if ((this.memoryKiB * 1024) % BLOCK_SIZE !== 0) {
      throw new RangeError("ForgeHash-X: memory must yield whole 512-byte blocks");
    }
    if (
      !Number.isInteger(this.iterations) ||
      this.iterations < MIN_ITERATIONS ||
      this.iterations > MAX_ITERATIONS
    ) {
      throw new RangeError("ForgeHash-X: iterations out of range");
    }
    if (
      !Number.isInteger(this.parallelism) ||
      this.parallelism < MIN_PARALLELISM ||
      this.parallelism > MAX_PARALLELISM
    ) {
      throw new RangeError("ForgeHash-X: parallelism out of range");
    }
    if (this.blockCount % this.parallelism !== 0) {
      throw new RangeError("ForgeHash-X: blockCount must be divisible by parallelism");
    }
    if (this.blocksPerLane % 4 !== 0) {
      throw new RangeError("ForgeHash-X: blocksPerLane must be divisible by 4");
    }
    if (this.blocksPerLane < 8) {
      throw new RangeError("ForgeHash-X: blocksPerLane must be at least 8");
    }
    if (
      !Number.isInteger(this.outputLength) ||
      this.outputLength < MIN_OUTPUT_LENGTH ||
      this.outputLength > MAX_OUTPUT_LENGTH
    ) {
      throw new RangeError("ForgeHash-X: outputLength out of range");
    }
    if (
      !Number.isInteger(this.saltLength) ||
      this.saltLength < MIN_SALT_LENGTH ||
      this.saltLength > MAX_SALT_LENGTH
    ) {
      throw new RangeError("ForgeHash-X: saltLength out of range");
    }
  }
}
