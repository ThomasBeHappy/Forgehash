"""ForgeHash-B3 v1 ForgeMix compression (SPECIFICATION.md §14)."""

from __future__ import annotations

import struct
from typing import List, Optional

BLOCK_SIZE = 1024
WORDS = 128
ROUNDS = 8

MASK64 = (1 << 64) - 1
MASK32 = 0xFFFFFFFF

WordBlock = List[int]


def low32(x: int) -> int:
    return x & MASK32


def rotl(x: int, n: int) -> int:
    n &= 63
    return ((x << n) | (x >> (64 - n))) & MASK64


def rotr(x: int, n: int) -> int:
    n &= 63
    return ((x >> n) | (x << (64 - n))) & MASK64


def quarter_round(words: WordBlock, base: int, i0: int, i1: int, i2: int, i3: int) -> None:
    a = words[base + i0]
    b = words[base + i1]
    c = words[base + i2]
    d = words[base + i3]

    a = (a + b + 2 * low32(a) * low32(b)) & MASK64
    d = rotr(d ^ a, 32)

    c = (c + d + 2 * low32(c) * low32(d)) & MASK64
    b = rotr(b ^ c, 24)

    a = (a + b + 2 * low32(a) * low32(b)) & MASK64
    d = rotr(d ^ a, 16)

    c = (c + d + 2 * low32(c) * low32(d)) & MASK64
    b = rotr(b ^ c, 63)

    words[base + i0] = a
    words[base + i1] = b
    words[base + i2] = c
    words[base + i3] = d


def apply_schedule(words: WordBlock, base: int) -> None:
    quarter_round(words, base, 0, 4, 8, 12)
    quarter_round(words, base, 1, 5, 9, 13)
    quarter_round(words, base, 2, 6, 10, 14)
    quarter_round(words, base, 3, 7, 11, 15)
    quarter_round(words, base, 0, 5, 10, 15)
    quarter_round(words, base, 1, 6, 11, 12)
    quarter_round(words, base, 2, 7, 8, 13)
    quarter_round(words, base, 3, 4, 9, 14)


def _idx(row: int, col: int) -> int:
    return row * 16 + col


def mix_rows(state: WordBlock) -> None:
    for row in range(8):
        apply_schedule(state, row * 16)


_virtual_row = [0] * 16
_group = [0] * 16


def mix_columns(state: WordBlock) -> None:
    for pair in range(8):
        col_a = pair * 2
        col_b = col_a + 1
        for row in range(8):
            _virtual_row[row * 2] = state[_idx(row, col_a)]
            _virtual_row[row * 2 + 1] = state[_idx(row, col_b)]
        apply_schedule(_virtual_row, 0)
        for row in range(8):
            state[_idx(row, col_a)] = _virtual_row[row * 2]
            state[_idx(row, col_b)] = _virtual_row[row * 2 + 1]


def mix_diagonals(state: WordBlock) -> None:
    for diagonal_index in range(8):
        base = diagonal_index * 2
        for k in range(8):
            _group[k * 2] = state[_idx(k, (base + k) & 15)]
            _group[k * 2 + 1] = state[_idx(k, (base + k + 8) & 15)]
        apply_schedule(_group, 0)
        for k in range(8):
            state[_idx(k, (base + k) & 15)] = _group[k * 2]
            state[_idx(k, (base + k + 8) & 15)] = _group[k * 2 + 1]


def permute(state: WordBlock, permuted: WordBlock) -> None:
    for source in range(WORDS):
        dest = (source * 73 + 19) & 127
        permuted[dest] = state[source]


def mix(
    input_block: WordBlock,
    pass_: int,
    lane: int,
    block_index: int,
    output: Optional[WordBlock] = None,
) -> WordBlock:
    """ForgeMix(inputBlock, pass, lane, blockIndex) -> outputBlock."""
    original = input_block
    state = list(input_block)

    state[0] ^= pass_ & MASK64
    state[1] ^= lane & MASK64
    state[2] ^= block_index & MASK64
    state[3] ^= rotl((pass_ + block_index) & MASK64, 17)

    permuted = [0] * WORDS
    for _ in range(ROUNDS):
        mix_rows(state)
        mix_columns(state)
        mix_diagonals(state)
        permute(state, permuted)
        state, permuted = permuted, state

    out = output if output is not None else [0] * WORDS
    for i in range(WORDS):
        out[i] = state[i] ^ original[i]
    return out


def bytes_to_words(data: bytes | bytearray | memoryview, out: Optional[WordBlock] = None) -> WordBlock:
    words = out if out is not None else [0] * WORDS
    unpacked = struct.unpack_from("<128Q", data)
    for i in range(WORDS):
        words[i] = unpacked[i]
    return words


def words_to_bytes(words: WordBlock, out: Optional[bytearray] = None) -> bytes | bytearray:
    packed = struct.pack("<128Q", *[w & MASK64 for w in words])
    if out is None:
        return packed
    out[:BLOCK_SIZE] = packed
    return out


def xor_words(left: WordBlock, right: WordBlock, out: Optional[WordBlock] = None) -> WordBlock:
    dest = out if out is not None else [0] * WORDS
    for i in range(WORDS):
        dest[i] = left[i] ^ right[i]
    return dest
