//! ForgePerm — 16-word ARX permutation (SPECIFICATION_X §4.3).

pub const WORDS: usize = 16;
pub const ROUNDS: usize = 8;

#[inline]
fn low32(x: u64) -> u64 {
    x & 0xFFFF_FFFF
}

#[inline]
pub fn rotl(x: u64, n: u32) -> u64 {
    x.rotate_left(n & 63)
}

#[inline]
fn rotr(x: u64, n: u32) -> u64 {
    x.rotate_right(n & 63)
}

#[inline]
fn round_constant(round: usize, index: usize) -> u64 {
    let r = round as u64;
    let i = index as u64;
    let x = 0x9E37_79B9_7F4A_7C15u64
        ^ r.wrapping_mul(0xD1B5_4A32_D192_ED03)
        ^ i.wrapping_mul(0xA24B_AED4_963E_E407);
    rotl(x, ((round + index * 3) & 63) as u32)
}

fn quarter_round(s: &mut [u64; WORDS], ia: usize, ib: usize, ic: usize, id: usize) {
    let mut a = s[ia];
    let mut b = s[ib];
    let mut c = s[ic];
    let mut d = s[id];

    a = a
        .wrapping_add(b)
        .wrapping_add(2u64.wrapping_mul(low32(a)).wrapping_mul(low32(b)));
    d = rotr(d ^ a, 17);
    c = c
        .wrapping_add(d)
        .wrapping_add(2u64.wrapping_mul(low32(c)).wrapping_mul(low32(d)));
    b = rotr(b ^ c, 11);
    a = a
        .wrapping_add(b)
        .wrapping_add(2u64.wrapping_mul(low32(a)).wrapping_mul(low32(b)));
    d = rotr(d ^ a, 23);
    c = c
        .wrapping_add(d)
        .wrapping_add(2u64.wrapping_mul(low32(c)).wrapping_mul(low32(d)));
    b = rotr(b ^ c, 41);

    s[ia] = a;
    s[ib] = b;
    s[ic] = c;
    s[id] = d;
}

/// In-place ForgePerm on a 16-word state (8 rounds).
pub fn permute(state: &mut [u64; WORDS]) {
    let mut temp = [0u64; WORDS];
    for r in 0..ROUNDS {
        for i in 0..WORDS {
            state[i] ^= round_constant(r, i);
        }
        quarter_round(state, 0, 4, 8, 12);
        quarter_round(state, 1, 5, 9, 13);
        quarter_round(state, 2, 6, 10, 14);
        quarter_round(state, 3, 7, 11, 15);
        quarter_round(state, 0, 5, 10, 15);
        quarter_round(state, 1, 6, 11, 12);
        quarter_round(state, 2, 7, 8, 13);
        quarter_round(state, 3, 4, 9, 14);
        for i in 0..WORDS {
            temp[(i * 7 + 3) & 15] = state[i];
        }
        *state = temp;
    }
}
