use crate::error::Error;
use crate::forgemix::{
    bytes_to_words, mix, words_to_bytes, xor_words, BLOCK_SIZE, WORDS,
};
use crate::params::{Params, MAX_PASSWORD};

const SEED_CTX: &str = "ForgeHash/v1/seed";
const EXPAND_PREFIX: &str = "ForgeHash/v1/expand";
const GROUP_PREFIX: &str = "ForgeHash/v1/group";
const GROUP_ROOT_PREFIX: &str = "ForgeHash/v1/group-root";
const FINAL_PREFIX: &str = "ForgeHash/v1/final";
const OUTPUT_PREFIX: &str = "ForgeHash/v1/output";

fn le32(v: u32) -> [u8; 4] {
    v.to_le_bytes()
}
fn le64(v: u64) -> [u8; 8] {
    v.to_le_bytes()
}

fn build_encoded_input(password: &[u8], salt: &[u8], params: &Params) -> Vec<u8> {
    let mut buf = Vec::with_capacity(4 * 5 + 8 + password.len() + 4 + salt.len() + 4);
    buf.extend_from_slice(&le32(1));
    buf.extend_from_slice(&le32(params.memory_kib as u32));
    buf.extend_from_slice(&le32(params.iterations as u32));
    buf.extend_from_slice(&le32(params.parallelism as u32));
    buf.extend_from_slice(&le32(params.output_length as u32));
    buf.extend_from_slice(&le64(password.len() as u64));
    buf.extend_from_slice(password);
    buf.extend_from_slice(&le32(salt.len() as u32));
    buf.extend_from_slice(salt);
    buf.extend_from_slice(&le32(0)); // empty context
    buf
}

fn derive_seed_bytes(material: &[u8]) -> [u8; 32] {
    let mut hasher = blake3::Hasher::new_derive_key(SEED_CTX);
    hasher.update(material);
    *hasher.finalize().as_bytes()
}

fn expand(input: &[u8], out: &mut [u8]) {
    let mut hasher = blake3::Hasher::new();
    hasher.update(EXPAND_PREFIX.as_bytes());
    hasher.update(input);
    hasher.finalize_xof().fill(out);
}

fn blake3_hash(input: &[u8]) -> [u8; 32] {
    *blake3::hash(input).as_bytes()
}

fn blake3_xof(input: &[u8], out_len: usize) -> Vec<u8> {
    let mut hasher = blake3::Hasher::new();
    hasher.update(input);
    let mut out = vec![0u8; out_len];
    hasher.finalize_xof().fill(&mut out);
    out
}

fn fast_range(x: u64, n: u64) -> u64 {
    ((x as u128) * (n as u128) >> 64) as u64
}

fn rotl(x: u64, n: u32) -> u64 {
    x.rotate_left(n)
}

struct Memory {
    words: Vec<u64>,
    parallelism: usize,
    blocks_per_lane: usize,
    slice_length: usize,
}

impl Memory {
    fn new(block_count: usize, parallelism: usize) -> Self {
        let blocks_per_lane = block_count / parallelism;
        Self {
            words: vec![0u64; block_count * WORDS],
            parallelism,
            blocks_per_lane,
            slice_length: blocks_per_lane / 4,
        }
    }

    fn block_mut(&mut self, lane: usize, index: usize) -> &mut [u64] {
        let flat = lane * self.blocks_per_lane + index;
        let start = flat * WORDS;
        &mut self.words[start..start + WORDS]
    }

    fn block(&self, lane: usize, index: usize) -> &[u64] {
        let flat = lane * self.blocks_per_lane + index;
        let start = flat * WORDS;
        &self.words[start..start + WORDS]
    }

    fn copy_block(&self, lane: usize, index: usize, dest: &mut [u64; WORDS]) {
        dest.copy_from_slice(self.block(lane, index));
    }
}

pub fn derive_seed(password: &[u8], salt: &[u8], params: &Params) -> Result<[u8; 32], Error> {
    if password.len() > MAX_PASSWORD {
        return Err(Error::Params("password too long"));
    }
    params.validate()?;
    if salt.len() < 16 || salt.len() > 64 {
        return Err(Error::Params("salt length out of range"));
    }
    let material = build_encoded_input(password, salt, params);
    Ok(derive_seed_bytes(&material))
}

pub fn derive_hash(password: &[u8], salt: &[u8], params: &Params) -> Result<Vec<u8>, Error> {
    if password.len() > MAX_PASSWORD {
        return Err(Error::Params("password too long"));
    }
    params.validate()?;
    if salt.len() < 16 || salt.len() > 64 {
        return Err(Error::Params("salt length out of range"));
    }

    let material = build_encoded_input(password, salt, params);
    let seed = derive_seed_bytes(&material);

    let mut memory = Memory::new(params.memory_kib, params.parallelism);
    initialize(&mut memory, &seed);
    fill(&mut memory, params.iterations);
    Ok(finalize(&memory, &seed, params))
}

fn initialize(memory: &mut Memory, seed: &[u8; 32]) {
    let mut expand_input = [0u8; 40];
    expand_input[..32].copy_from_slice(seed);
    let mut block_bytes = [0u8; BLOCK_SIZE];
    let mut words = [0u64; WORDS];

    for lane in 0..memory.parallelism {
        for block_index in 0..2 {
            expand_input[32..36].copy_from_slice(&le32(lane as u32));
            expand_input[36..40].copy_from_slice(&le32(block_index as u32));
            expand(&expand_input, &mut block_bytes);
            bytes_to_words(&block_bytes, &mut words);
            memory.block_mut(lane, block_index).copy_from_slice(&words);
        }
    }
}

fn fill(memory: &mut Memory, iterations: usize) {
    let mut combined = [0u64; WORDS];
    let mut mixed = [0u64; WORDS];
    let mut previous = [0u64; WORDS];
    let mut reference = [0u64; WORDS];
    let mut output = [0u64; WORDS];

    for pass in 0..iterations {
        for slice in 0..4 {
            for lane in 0..memory.parallelism {
                process_slice(
                    memory,
                    pass,
                    slice,
                    lane,
                    &mut combined,
                    &mut mixed,
                    &mut previous,
                    &mut reference,
                    &mut output,
                );
            }
        }
    }
}

fn process_slice(
    memory: &mut Memory,
    pass: usize,
    slice: usize,
    lane: usize,
    combined: &mut [u64; WORDS],
    mixed: &mut [u64; WORDS],
    previous: &mut [u64; WORDS],
    reference: &mut [u64; WORDS],
    output: &mut [u64; WORDS],
) {
    let mut start = slice * memory.slice_length;
    let end = start + memory.slice_length;
    if pass == 0 && slice == 0 {
        start = 2;
    }

    for block_index in start..end {
        let previous_index = if block_index > 0 {
            block_index - 1
        } else {
            memory.blocks_per_lane - 1
        };
        memory.copy_block(lane, previous_index, previous);

        let (ref_lane, ref_index) =
            select_reference(memory, pass, slice, lane, block_index, previous);
        memory.copy_block(ref_lane, ref_index, reference);
        xor_words(combined, previous, reference);

        if pass == 0 {
            mix(combined, pass as u64, lane as u64, block_index as u64, output);
            memory.block_mut(lane, block_index).copy_from_slice(output);
        } else {
            mix(combined, pass as u64, lane as u64, block_index as u64, mixed);
            memory.copy_block(lane, block_index, previous); // reuse as old-current scratch
            xor_words(output, previous, mixed);
            memory.block_mut(lane, block_index).copy_from_slice(output);
        }
    }
}

fn select_reference(
    memory: &Memory,
    pass: usize,
    slice: usize,
    current_lane: usize,
    block_index: usize,
    previous: &[u64; WORDS],
) -> (usize, usize) {
    let address_word = previous[0]
        ^ rotl(previous[17], 13)
        ^ previous[73]
        ^ (pass as u64)
        ^ rotl(block_index as u64, 29);

    let mut reference_lane = if memory.parallelism == 1 {
        0
    } else if block_index % 32 == 0 {
        fast_range(previous[1], memory.parallelism as u64) as usize
    } else {
        current_lane
    };

    let mut allowed = allowed_block_count(memory, pass, slice, current_lane, reference_lane, block_index);
    if allowed == 0 {
        reference_lane = current_lane;
        allowed = allowed_block_count(memory, pass, slice, current_lane, reference_lane, block_index);
    }
    assert!(allowed > 0);
    let reference_index = fast_range(address_word, allowed as u64) as usize;
    (reference_lane, reference_index)
}

fn allowed_block_count(
    memory: &Memory,
    pass: usize,
    slice: usize,
    current_lane: usize,
    reference_lane: usize,
    block_index: usize,
) -> usize {
    if reference_lane == current_lane {
        if pass == 0 {
            block_index
        } else {
            memory.blocks_per_lane
        }
    } else {
        slice * memory.slice_length
    }
}

fn finalize(memory: &Memory, seed: &[u8; 32], params: &Params) -> Vec<u8> {
    let mut acc = [0u64; WORDS];
    let last = memory.blocks_per_lane - 1;
    let quarter = memory.blocks_per_lane / 4;
    let half = memory.blocks_per_lane / 2;
    let three_quarter = (memory.blocks_per_lane * 3) / 4;
    let mut tmp = [0u64; WORDS];

    for lane in 0..memory.parallelism {
        for idx in [last, quarter, half, three_quarter] {
            memory.copy_block(lane, idx, &mut tmp);
            for i in 0..WORDS {
                acc[i] ^= tmp[i];
            }
        }
    }

    let mut accumulator = [0u8; BLOCK_SIZE];
    words_to_bytes(&acc, &mut accumulator);
    let group_root = compute_group_root(memory);

    let mut root_input = Vec::new();
    root_input.extend_from_slice(FINAL_PREFIX.as_bytes());
    root_input.extend_from_slice(seed);
    root_input.extend_from_slice(&accumulator);
    root_input.extend_from_slice(&group_root);
    root_input.extend_from_slice(&le32(params.memory_kib as u32));
    root_input.extend_from_slice(&le32(params.iterations as u32));
    root_input.extend_from_slice(&le32(params.parallelism as u32));
    root_input.extend_from_slice(&le32(params.output_length as u32));
    let root = blake3_hash(&root_input);

    let mut output_input = Vec::new();
    output_input.extend_from_slice(OUTPUT_PREFIX.as_bytes());
    output_input.extend_from_slice(&root);
    output_input.extend_from_slice(seed);
    blake3_xof(&output_input, params.output_length)
}

fn compute_group_root(memory: &Memory) -> [u8; 32] {
    let mut hasher = blake3::Hasher::new();
    hasher.update(GROUP_ROOT_PREFIX.as_bytes());

    const GROUP: usize = 64;
    let total = memory.parallelism * memory.blocks_per_lane;
    let group_count = (total + GROUP - 1) / GROUP;
    let mut group_buf = vec![0u8; GROUP * BLOCK_SIZE];
    let mut block_bytes = [0u8; BLOCK_SIZE];
    let mut words = [0u64; WORDS];

    for group_index in 0..group_count {
        let start = group_index * GROUP;
        let count = (total - start).min(GROUP);
        let byte_len = count * BLOCK_SIZE;
        for i in 0..count {
            let flat = start + i;
            let lane = flat / memory.blocks_per_lane;
            let block_index = flat % memory.blocks_per_lane;
            memory.copy_block(lane, block_index, &mut words);
            words_to_bytes(&words, &mut block_bytes);
            group_buf[i * BLOCK_SIZE..(i + 1) * BLOCK_SIZE].copy_from_slice(&block_bytes);
        }

        let mut group_hasher = blake3::Hasher::new();
        group_hasher.update(GROUP_PREFIX.as_bytes());
        group_hasher.update(&le64(group_index as u64));
        group_hasher.update(&le64(count as u64));
        group_hasher.update(&group_buf[..byte_len]);
        let digest = group_hasher.finalize();

        hasher.update(&le64(group_index as u64));
        hasher.update(digest.as_bytes());
    }

    *hasher.finalize().as_bytes()
}
