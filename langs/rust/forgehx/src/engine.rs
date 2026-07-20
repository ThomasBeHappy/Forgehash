//! ForgeHash-X v0 memory-hard engine (SPECIFICATION_X §§6–12).

use crate::error::Error;
use crate::forgeperm::{self, WORDS as PERM_WORDS};
use crate::forgex::ForgeX;
use crate::params::{Params, BLOCK_SIZE, MAX_SALT_LENGTH, MIN_SALT_LENGTH, WORDS_PER_BLOCK};

const TAG_SEED: &str = "ForgeX/v0/seed";
const TAG_EXPAND: &str = "ForgeX/v0/expand";
const TAG_FINAL: &str = "ForgeX/v0/final";
const TAG_OUTPUT: &str = "ForgeX/v0/output";

#[inline]
fn fast_range(x: u64, n: u64) -> u64 {
    ((x as u128) * (n as u128) >> 64) as u64
}

fn validate_salt(salt: &[u8]) -> Result<(), Error> {
    if !(MIN_SALT_LENGTH..=MAX_SALT_LENGTH).contains(&salt.len()) {
        return Err(Error::Params("salt length out of range"));
    }
    Ok(())
}

fn build_material(password: &[u8], salt: &[u8], params: &Params) -> Vec<u8> {
    let mut buf = Vec::with_capacity(4 * 5 + 8 + password.len() + 4 + salt.len());
    buf.extend_from_slice(&0u32.to_le_bytes()); // algorithm family id for X sandbox
    buf.extend_from_slice(&(params.memory_kib as u32).to_le_bytes());
    buf.extend_from_slice(&(params.iterations as u32).to_le_bytes());
    buf.extend_from_slice(&(params.parallelism as u32).to_le_bytes());
    buf.extend_from_slice(&(params.output_length as u32).to_le_bytes());
    buf.extend_from_slice(&(password.len() as u64).to_le_bytes());
    buf.extend_from_slice(password);
    buf.extend_from_slice(&(salt.len() as u32).to_le_bytes());
    buf.extend_from_slice(salt);
    buf
}

pub fn derive_seed(password: &[u8], salt: &[u8], params: &Params) -> Result<[u8; 32], Error> {
    params.validate()?;
    validate_salt(salt)?;
    Ok(ForgeX::hash(TAG_SEED, &build_material(password, salt, params)))
}

fn bytes_to_words(data: &[u8]) -> Vec<u64> {
    data.chunks_exact(8)
        .map(|c| u64::from_le_bytes(c.try_into().unwrap()))
        .collect()
}

fn words_to_bytes(words: &[u64]) -> Vec<u8> {
    let mut out = Vec::with_capacity(words.len() * 8);
    for w in words {
        out.extend_from_slice(&w.to_le_bytes());
    }
    out
}

struct Memory {
    words: Vec<u64>,
    blocks_per_lane: usize,
}

impl Memory {
    fn new(block_count: usize, blocks_per_lane: usize) -> Self {
        Self {
            words: vec![0u64; block_count * WORDS_PER_BLOCK],
            blocks_per_lane,
        }
    }

    fn block(&self, lane: usize, index: usize) -> &[u64] {
        let start = (lane * self.blocks_per_lane + index) * WORDS_PER_BLOCK;
        &self.words[start..start + WORDS_PER_BLOCK]
    }

    fn block_mut(&mut self, lane: usize, index: usize) -> &mut [u64] {
        let start = (lane * self.blocks_per_lane + index) * WORDS_PER_BLOCK;
        &mut self.words[start..start + WORDS_PER_BLOCK]
    }

    fn set_block(&mut self, lane: usize, index: usize, words: &[u64]) {
        self.block_mut(lane, index).copy_from_slice(words);
    }
}

fn select_reference(
    previous: &[u64],
    pass: usize,
    slice: usize,
    current_lane: usize,
    block_index: usize,
    parallelism: usize,
    blocks_per_lane: usize,
    slice_length: usize,
) -> (usize, usize) {
    let address_word = previous[0]
        ^ forgeperm::rotl(previous[9], 19)
        ^ previous[31]
        ^ (pass as u64 & 0xFFFF_FFFF)
        ^ forgeperm::rotl(block_index as u64 & 0xFFFF_FFFF, 11);

    let mut lane = if parallelism == 1 {
        0
    } else if block_index % 16 == 0 {
        fast_range(previous[1], parallelism as u64) as usize
    } else {
        current_lane
    };

    let allowed = |ref_lane: usize| -> u64 {
        if ref_lane == current_lane {
            if pass == 0 {
                block_index as u64
            } else {
                blocks_per_lane as u64
            }
        } else {
            (slice * slice_length) as u64
        }
    };

    let mut a = allowed(lane);
    if a == 0 {
        lane = current_lane;
        a = allowed(lane);
    }
    let ref_index = fast_range(address_word, a) as usize;
    (lane, ref_index)
}

fn block_mix(inp: &[u64], pass: usize, lane: usize, block_index: usize) -> [u64; WORDS_PER_BLOCK] {
    let mut out = [0u64; WORDS_PER_BLOCK];
    for k in 0..4 {
        let chunk_start = k * PERM_WORDS;
        let mut state = [0u64; PERM_WORDS];
        state.copy_from_slice(&inp[chunk_start..chunk_start + PERM_WORDS]);
        let chunk = state;
        state[0] ^= pass as u64;
        state[1] ^= lane as u64;
        state[2] ^= block_index as u64;
        state[3] ^= forgeperm::rotl(
            (pass as u64)
                .wrapping_add(block_index as u64)
                .wrapping_add(k as u64),
            13,
        );
        forgeperm::permute(&mut state);
        for i in 0..PERM_WORDS {
            out[chunk_start + i] = state[i] ^ chunk[i];
        }
    }
    out
}

fn process_lane_slice(
    memory: &mut Memory,
    pass: usize,
    slice: usize,
    lane: usize,
    parallelism: usize,
    blocks_per_lane: usize,
    slice_length: usize,
) {
    let mut start = slice * slice_length;
    let end = start + slice_length;
    if pass == 0 && slice == 0 {
        start = 2;
    }
    for block_index in start..end {
        let previous_index = if block_index > 0 {
            block_index - 1
        } else {
            blocks_per_lane - 1
        };
        let mut prev = [0u64; WORDS_PER_BLOCK];
        prev.copy_from_slice(memory.block(lane, previous_index));
        let (ref_lane, ref_index) = select_reference(
            &prev,
            pass,
            slice,
            lane,
            block_index,
            parallelism,
            blocks_per_lane,
            slice_length,
        );
        let mut combined = [0u64; WORDS_PER_BLOCK];
        let reference = memory.block(ref_lane, ref_index);
        for w in 0..WORDS_PER_BLOCK {
            combined[w] = prev[w] ^ reference[w];
        }
        let mixed = block_mix(&combined, pass, lane, block_index);
        if pass == 0 {
            memory.set_block(lane, block_index, &mixed);
        } else {
            let cur = memory.block_mut(lane, block_index);
            for w in 0..WORDS_PER_BLOCK {
                cur[w] ^= mixed[w];
            }
        }
    }
}

pub fn derive_hash(password: &[u8], salt: &[u8], params: &Params) -> Result<Vec<u8>, Error> {
    params.validate()?;
    validate_salt(salt)?;

    let seed = ForgeX::hash(TAG_SEED, &build_material(password, salt, params));
    let parallelism = params.parallelism;
    let blocks_per_lane = params.blocks_per_lane();
    let slice_length = params.slice_length();
    let mut memory = Memory::new(params.block_count(), blocks_per_lane);

    for lane in 0..parallelism {
        for i in 0..2 {
            let mut expand_input = Vec::with_capacity(40);
            expand_input.extend_from_slice(&seed);
            expand_input.extend_from_slice(&(lane as u32).to_le_bytes());
            expand_input.extend_from_slice(&(i as u32).to_le_bytes());
            let block_bytes = ForgeX::xof(TAG_EXPAND, &expand_input, BLOCK_SIZE);
            memory.set_block(lane, i, &bytes_to_words(&block_bytes));
        }
    }

    for pass in 0..params.iterations {
        for slice in 0..4 {
            for lane in 0..parallelism {
                process_lane_slice(
                    &mut memory,
                    pass,
                    slice,
                    lane,
                    parallelism,
                    blocks_per_lane,
                    slice_length,
                );
            }
        }
    }

    let mut fold = [0u64; WORDS_PER_BLOCK];
    let last = blocks_per_lane - 1;
    let q1 = blocks_per_lane / 4;
    let q2 = blocks_per_lane / 2;
    let q3 = (blocks_per_lane * 3) / 4;
    for lane in 0..parallelism {
        for &index in &[last, q1, q2, q3] {
            let blk = memory.block(lane, index);
            for w in 0..WORDS_PER_BLOCK {
                fold[w] ^= blk[w];
            }
        }
    }

    let fold_bytes = words_to_bytes(&fold);
    let mut final_input = Vec::with_capacity(32 + 512 + 16);
    final_input.extend_from_slice(&seed);
    final_input.extend_from_slice(&fold_bytes);
    final_input.extend_from_slice(&(params.memory_kib as u32).to_le_bytes());
    final_input.extend_from_slice(&(params.iterations as u32).to_le_bytes());
    final_input.extend_from_slice(&(params.parallelism as u32).to_le_bytes());
    final_input.extend_from_slice(&(params.output_length as u32).to_le_bytes());

    let root = ForgeX::hash(TAG_FINAL, &final_input);
    let mut output_input = Vec::with_capacity(64);
    output_input.extend_from_slice(&root);
    output_input.extend_from_slice(&seed);
    Ok(ForgeX::xof(TAG_OUTPUT, &output_input, params.output_length))
}
