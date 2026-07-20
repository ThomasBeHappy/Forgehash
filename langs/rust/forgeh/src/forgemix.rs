pub const BLOCK_SIZE: usize = 1024;
pub const WORDS: usize = 128;
pub const ROUNDS: usize = 8;

#[inline]
fn low32(x: u64) -> u64 {
    x & 0xffff_ffff
}

#[inline]
fn rotl(x: u64, n: u32) -> u64 {
    x.rotate_left(n)
}

#[inline]
fn rotr(x: u64, n: u32) -> u64 {
    x.rotate_right(n)
}

pub fn quarter_round(a: &mut u64, b: &mut u64, c: &mut u64, d: &mut u64) {
    *a = a.wrapping_add(*b).wrapping_add(2u64.wrapping_mul(low32(*a)).wrapping_mul(low32(*b)));
    *d = rotr(*d ^ *a, 32);

    *c = c.wrapping_add(*d).wrapping_add(2u64.wrapping_mul(low32(*c)).wrapping_mul(low32(*d)));
    *b = rotr(*b ^ *c, 24);

    *a = a.wrapping_add(*b).wrapping_add(2u64.wrapping_mul(low32(*a)).wrapping_mul(low32(*b)));
    *d = rotr(*d ^ *a, 16);

    *c = c.wrapping_add(*d).wrapping_add(2u64.wrapping_mul(low32(*c)).wrapping_mul(low32(*d)));
    *b = rotr(*b ^ *c, 63);
}

fn apply_schedule(words: &mut [u64], offset: usize) {
    let mut q = |i0, i1, i2, i3| {
        let mut a = words[offset + i0];
        let mut b = words[offset + i1];
        let mut c = words[offset + i2];
        let mut d = words[offset + i3];
        quarter_round(&mut a, &mut b, &mut c, &mut d);
        words[offset + i0] = a;
        words[offset + i1] = b;
        words[offset + i2] = c;
        words[offset + i3] = d;
    };
    q(0, 4, 8, 12);
    q(1, 5, 9, 13);
    q(2, 6, 10, 14);
    q(3, 7, 11, 15);
    q(0, 5, 10, 15);
    q(1, 6, 11, 12);
    q(2, 7, 8, 13);
    q(3, 4, 9, 14);
}

fn index(row: usize, col: usize) -> usize {
    row * 16 + col
}

fn mix_rows(state: &mut [u64; WORDS]) {
    for row in 0..8 {
        apply_schedule(state, row * 16);
    }
}

fn mix_columns(state: &mut [u64; WORDS]) {
    let mut virtual_row = [0u64; 16];
    for pair in 0..8 {
        let col_a = pair * 2;
        let col_b = col_a + 1;
        for row in 0..8 {
            virtual_row[row * 2] = state[index(row, col_a)];
            virtual_row[row * 2 + 1] = state[index(row, col_b)];
        }
        apply_schedule(&mut virtual_row, 0);
        for row in 0..8 {
            state[index(row, col_a)] = virtual_row[row * 2];
            state[index(row, col_b)] = virtual_row[row * 2 + 1];
        }
    }
}

fn mix_diagonals(state: &mut [u64; WORDS]) {
    let mut group = [0u64; 16];
    for diagonal_index in 0..8 {
        let base = diagonal_index * 2;
        for k in 0..8 {
            group[k * 2] = state[index(k, (base + k) & 15)];
            group[k * 2 + 1] = state[index(k, (base + k + 8) & 15)];
        }
        apply_schedule(&mut group, 0);
        for k in 0..8 {
            state[index(k, (base + k) & 15)] = group[k * 2];
            state[index(k, (base + k + 8) & 15)] = group[k * 2 + 1];
        }
    }
}

fn permute(state: &[u64; WORDS], permuted: &mut [u64; WORDS]) {
    for source in 0..WORDS {
        let dest = (source * 73 + 19) & 127;
        permuted[dest] = state[source];
    }
}

pub fn mix(input: &[u64; WORDS], pass: u64, lane: u64, block_index: u64, output: &mut [u64; WORDS]) {
    let original = *input;
    let mut state = *input;
    state[0] ^= pass;
    state[1] ^= lane;
    state[2] ^= block_index;
    state[3] ^= rotl(pass.wrapping_add(block_index), 17);

    let mut permuted = [0u64; WORDS];
    for _ in 0..ROUNDS {
        mix_rows(&mut state);
        mix_columns(&mut state);
        mix_diagonals(&mut state);
        permute(&state, &mut permuted);
        state = permuted;
    }
    for i in 0..WORDS {
        output[i] = state[i] ^ original[i];
    }
}

pub fn bytes_to_words(bytes: &[u8], words: &mut [u64; WORDS]) {
    for i in 0..WORDS {
        let start = i * 8;
        words[i] = u64::from_le_bytes(bytes[start..start + 8].try_into().unwrap());
    }
}

pub fn words_to_bytes(words: &[u64; WORDS], bytes: &mut [u8]) {
    for i in 0..WORDS {
        bytes[i * 8..i * 8 + 8].copy_from_slice(&words[i].to_le_bytes());
    }
}

pub fn xor_words(dest: &mut [u64; WORDS], left: &[u64; WORDS], right: &[u64; WORDS]) {
    for i in 0..WORDS {
        dest[i] = left[i] ^ right[i];
    }
}
