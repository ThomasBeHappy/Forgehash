use crate::error::Error;

pub const BLOCK_SIZE: usize = 512;
pub const WORDS_PER_BLOCK: usize = BLOCK_SIZE / 8;
pub const MIN_MEMORY_KIB: usize = 256;
pub const MAX_MEMORY_KIB: usize = 65_536;
pub const MIN_ITERATIONS: usize = 1;
pub const MAX_ITERATIONS: usize = 8;
pub const MIN_PARALLELISM: usize = 1;
pub const MAX_PARALLELISM: usize = 16;
pub const MIN_OUTPUT_LENGTH: usize = 16;
pub const MAX_OUTPUT_LENGTH: usize = 64;
pub const MIN_SALT_LENGTH: usize = 16;
pub const MAX_SALT_LENGTH: usize = 64;
pub const DEFAULT_OUTPUT_LENGTH: usize = 32;

#[derive(Clone, Debug)]
pub struct Params {
    pub memory_kib: usize,
    pub iterations: usize,
    pub parallelism: usize,
    pub output_length: usize,
    pub salt_length: usize,
}

impl Default for Params {
    fn default() -> Self {
        Self::toy()
    }
}

impl Params {
    /// Toy-vector profile (m=1024, t=1, p=1).
    pub fn toy() -> Self {
        Self {
            memory_kib: 1024,
            iterations: 1,
            parallelism: 1,
            output_length: DEFAULT_OUTPUT_LENGTH,
            salt_length: 16,
        }
    }

    pub fn block_count(&self) -> usize {
        self.memory_kib * 1024 / BLOCK_SIZE
    }

    pub fn blocks_per_lane(&self) -> usize {
        self.block_count() / self.parallelism
    }

    pub fn slice_length(&self) -> usize {
        self.blocks_per_lane() / 4
    }

    pub fn validate(&self) -> Result<(), Error> {
        if !(MIN_MEMORY_KIB..=MAX_MEMORY_KIB).contains(&self.memory_kib) {
            return Err(Error::Params("memoryKiB out of sandbox range"));
        }
        if (self.memory_kib * 1024) % BLOCK_SIZE != 0 {
            return Err(Error::Params("memory must yield whole 512-byte blocks"));
        }
        if !(MIN_ITERATIONS..=MAX_ITERATIONS).contains(&self.iterations) {
            return Err(Error::Params("iterations out of range"));
        }
        if !(MIN_PARALLELISM..=MAX_PARALLELISM).contains(&self.parallelism) {
            return Err(Error::Params("parallelism out of range"));
        }
        if self.block_count() % self.parallelism != 0 {
            return Err(Error::Params("blockCount must be divisible by parallelism"));
        }
        if self.blocks_per_lane() % 4 != 0 {
            return Err(Error::Params("blocksPerLane must be divisible by 4"));
        }
        if self.blocks_per_lane() < 8 {
            return Err(Error::Params("blocksPerLane must be at least 8"));
        }
        if !(MIN_OUTPUT_LENGTH..=MAX_OUTPUT_LENGTH).contains(&self.output_length) {
            return Err(Error::Params("outputLength out of range"));
        }
        if !(MIN_SALT_LENGTH..=MAX_SALT_LENGTH).contains(&self.salt_length) {
            return Err(Error::Params("saltLength out of range"));
        }
        Ok(())
    }
}
