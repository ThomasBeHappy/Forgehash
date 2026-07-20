// ForgeHash-B3 v1 — parameter definitions and validation (SPECIFICATION.md §7).

export const DEFAULT_OUTPUT_LENGTH = 32;
export const ABSOLUTE_MAX_PARALLELISM = 255;
export const MIN_MEMORY_KIB = 8192;
export const MAX_MEMORY_KIB = 1_048_576;
export const MIN_ITERATIONS = 1;
export const MAX_ITERATIONS = 20;
export const MAX_PARALLELISM_POLICY = 64;
export const MIN_SALT = 16;
export const MAX_SALT = 64;
export const MIN_OUTPUT = 16;
export const MAX_OUTPUT = 64;
export const MAX_PASSWORD = 1_048_576;

/**
 * ForgeHash-B3 parameters. Mirrors the reference `ForgeHashParameters` /
 * Rust `Params` struct.
 */
export class Params {
  constructor({
    memoryKiB = 65_536,
    iterations = 3,
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

  /** Recommended interactive profile: 64 MiB, 3 iterations, 1 lane. */
  static interactive() {
    return new Params({
      memoryKiB: 65_536,
      iterations: 3,
      parallelism: 1,
      outputLength: DEFAULT_OUTPUT_LENGTH,
      saltLength: 16,
    });
  }

  /** Development-only profile: fast, low cost. Never use in production. */
  static development() {
    return new Params({
      memoryKiB: MIN_MEMORY_KIB,
      iterations: 1,
      parallelism: 1,
      outputLength: DEFAULT_OUTPUT_LENGTH,
      saltLength: 16,
    });
  }

  /** Recommended sensitive profile: 256 MiB, 4 iterations, 2 lanes. */
  static sensitive() {
    return new Params({
      memoryKiB: 262_144,
      iterations: 4,
      parallelism: 2,
      outputLength: DEFAULT_OUTPUT_LENGTH,
      saltLength: 16,
    });
  }

  /** Throws a descriptive Error if any parameter is out of range. */
  validate() {
    if (
      !Number.isInteger(this.memoryKiB) ||
      this.memoryKiB < MIN_MEMORY_KIB ||
      this.memoryKiB > MAX_MEMORY_KIB
    ) {
      throw new RangeError("ForgeHash: memory out of range");
    }
    if (
      !Number.isInteger(this.iterations) ||
      this.iterations < MIN_ITERATIONS ||
      this.iterations > MAX_ITERATIONS
    ) {
      throw new RangeError("ForgeHash: iterations out of range");
    }
    if (
      !Number.isInteger(this.parallelism) ||
      this.parallelism < 1 ||
      this.parallelism > MAX_PARALLELISM_POLICY ||
      this.parallelism > ABSOLUTE_MAX_PARALLELISM
    ) {
      throw new RangeError("ForgeHash: parallelism out of range");
    }
    if (this.memoryKiB < this.parallelism * 8) {
      throw new RangeError("ForgeHash: memory too small for lane count");
    }
    if (this.memoryKiB % this.parallelism !== 0) {
      throw new RangeError("ForgeHash: memory not divisible by parallelism");
    }
    const blocksPerLane = this.memoryKiB / this.parallelism;
    if (blocksPerLane % 4 !== 0) {
      throw new RangeError("ForgeHash: blocks per lane not divisible by 4");
    }
    if (
      !Number.isInteger(this.outputLength) ||
      this.outputLength < MIN_OUTPUT ||
      this.outputLength > MAX_OUTPUT
    ) {
      throw new RangeError("ForgeHash: output length out of range");
    }
    if (
      !Number.isInteger(this.saltLength) ||
      this.saltLength < MIN_SALT ||
      this.saltLength > MAX_SALT
    ) {
      throw new RangeError("ForgeHash: salt length out of range");
    }
  }
}
