use crate::error::Error;

pub const DEFAULT_OUTPUT_LENGTH: usize = 32;
pub const ABSOLUTE_MAX_PARALLELISM: usize = 255;
pub const MIN_MEMORY_KIB: usize = 8192;
pub const MAX_MEMORY_KIB: usize = 1_048_576;
pub const MIN_ITERATIONS: usize = 1;
pub const MAX_ITERATIONS: usize = 20;
pub const MAX_PARALLELISM_POLICY: usize = 64;
pub const MIN_SALT: usize = 16;
pub const MAX_SALT: usize = 64;
pub const MIN_OUTPUT: usize = 16;
pub const MAX_OUTPUT: usize = 64;
pub const MAX_PASSWORD: usize = 1_048_576;

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
        Self::interactive()
    }
}

impl Params {
    pub fn interactive() -> Self {
        Self {
            memory_kib: 65_536,
            iterations: 3,
            parallelism: 1,
            output_length: DEFAULT_OUTPUT_LENGTH,
            salt_length: 16,
        }
    }

    pub fn development() -> Self {
        Self {
            memory_kib: MIN_MEMORY_KIB,
            iterations: 1,
            parallelism: 1,
            output_length: DEFAULT_OUTPUT_LENGTH,
            salt_length: 16,
        }
    }

    pub fn sensitive() -> Self {
        Self {
            memory_kib: 262_144,
            iterations: 4,
            parallelism: 2,
            output_length: DEFAULT_OUTPUT_LENGTH,
            salt_length: 16,
        }
    }

    pub fn validate(&self) -> Result<(), Error> {
        if self.memory_kib < MIN_MEMORY_KIB || self.memory_kib > MAX_MEMORY_KIB {
            return Err(Error::Params("memory out of range"));
        }
        if self.iterations < MIN_ITERATIONS || self.iterations > MAX_ITERATIONS {
            return Err(Error::Params("iterations out of range"));
        }
        if self.parallelism < 1
            || self.parallelism > MAX_PARALLELISM_POLICY
            || self.parallelism > ABSOLUTE_MAX_PARALLELISM
        {
            return Err(Error::Params("parallelism out of range"));
        }
        if self.memory_kib < self.parallelism * 8 {
            return Err(Error::Params("memory too small for lanes"));
        }
        if self.memory_kib % self.parallelism != 0 {
            return Err(Error::Params("memory not divisible by parallelism"));
        }
        let blocks_per_lane = self.memory_kib / self.parallelism;
        if blocks_per_lane % 4 != 0 {
            return Err(Error::Params("blocks per lane not divisible by 4"));
        }
        if self.output_length < MIN_OUTPUT || self.output_length > MAX_OUTPUT {
            return Err(Error::Params("output length out of range"));
        }
        if self.salt_length < MIN_SALT || self.salt_length > MAX_SALT {
            return Err(Error::Params("salt length out of range"));
        }
        Ok(())
    }
}
