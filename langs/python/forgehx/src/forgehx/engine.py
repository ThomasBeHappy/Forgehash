"""ForgeHash-X v0 memory-hard engine (SPECIFICATION_X §§6–12)."""

from __future__ import annotations

import struct
from typing import List

from . import forgeperm
from .forgex import ForgeX
from .params import (
    BLOCK_SIZE,
    MAX_SALT_LENGTH,
    MIN_SALT_LENGTH,
    WORDS_PER_BLOCK,
    Params,
)

TAG_SEED = "ForgeX/v0/seed"
TAG_EXPAND = "ForgeX/v0/expand"
TAG_FINAL = "ForgeX/v0/final"
TAG_OUTPUT = "ForgeX/v0/output"

_MASK64 = (1 << 64) - 1


def fast_range(x: int, n: int) -> int:
    return ((x & _MASK64) * (n & _MASK64)) >> 64


def _validate_salt(salt: bytes) -> None:
    if not (MIN_SALT_LENGTH <= len(salt) <= MAX_SALT_LENGTH):
        raise ValueError("ForgeHash-X: salt length out of range")


def _build_material(password: bytes, salt: bytes, params: Params) -> bytes:
    return b"".join(
        [
            struct.pack("<I", 0),
            struct.pack("<I", params.memory_kib),
            struct.pack("<I", params.iterations),
            struct.pack("<I", params.parallelism),
            struct.pack("<I", params.output_length),
            struct.pack("<Q", len(password)),
            password,
            struct.pack("<I", len(salt)),
            salt,
        ]
    )


def derive_seed(password: bytes, salt: bytes, params: Params) -> bytes:
    params.validate()
    _validate_salt(salt)
    return ForgeX.hash(TAG_SEED, _build_material(password, salt, params))


def _block_view(memory: List[int], lane: int, index: int, blocks_per_lane: int) -> List[int]:
    start = (lane * blocks_per_lane + index) * WORDS_PER_BLOCK
    return memory[start : start + WORDS_PER_BLOCK]


def _set_block(memory: List[int], lane: int, index: int, blocks_per_lane: int, words: List[int]) -> None:
    start = (lane * blocks_per_lane + index) * WORDS_PER_BLOCK
    memory[start : start + WORDS_PER_BLOCK] = words


def _bytes_to_words(data: bytes) -> List[int]:
    return list(struct.unpack("<" + "Q" * (len(data) // 8), data))


def _words_to_bytes(words: List[int]) -> bytes:
    return struct.pack("<" + "Q" * len(words), *[w & _MASK64 for w in words])


def _select_reference(
    previous: List[int],
    pass_: int,
    slice_: int,
    current_lane: int,
    block_index: int,
    parallelism: int,
    blocks_per_lane: int,
    slice_length: int,
) -> tuple[int, int]:
    address_word = (
        previous[0]
        ^ forgeperm.rotl(previous[9], 19)
        ^ previous[31]
        ^ (pass_ & 0xFFFFFFFF)
        ^ forgeperm.rotl(block_index & 0xFFFFFFFF, 11)
    ) & _MASK64

    if parallelism == 1:
        lane = 0
    elif block_index % 16 == 0:
        lane = fast_range(previous[1], parallelism)
    else:
        lane = current_lane

    def allowed(ref_lane: int) -> int:
        if ref_lane == current_lane:
            return block_index if pass_ == 0 else blocks_per_lane
        return slice_ * slice_length

    a = allowed(lane)
    if a == 0:
        lane = current_lane
        a = allowed(lane)
    return lane, fast_range(address_word, a)


def _block_mix(inp: List[int], pass_: int, lane: int, block_index: int) -> List[int]:
    out = [0] * WORDS_PER_BLOCK
    for k in range(4):
        chunk = inp[k * 16 : (k + 1) * 16]
        state = list(chunk)
        state[0] ^= pass_ & _MASK64
        state[1] ^= lane & _MASK64
        state[2] ^= block_index & _MASK64
        state[3] ^= forgeperm.rotl((pass_ + block_index + k) & _MASK64, 13)
        forgeperm.permute(state)
        for i in range(16):
            out[k * 16 + i] = (state[i] ^ chunk[i]) & _MASK64
    return out


def _process_lane_slice(
    memory: List[int],
    pass_: int,
    slice_: int,
    lane: int,
    parallelism: int,
    blocks_per_lane: int,
    slice_length: int,
) -> None:
    start = slice_ * slice_length
    end = start + slice_length
    if pass_ == 0 and slice_ == 0:
        start = 2
    for block_index in range(start, end):
        previous_index = block_index - 1 if block_index > 0 else blocks_per_lane - 1
        prev = _block_view(memory, lane, previous_index, blocks_per_lane)
        ref_lane, ref_index = _select_reference(
            prev, pass_, slice_, lane, block_index, parallelism, blocks_per_lane, slice_length
        )
        reference = _block_view(memory, ref_lane, ref_index, blocks_per_lane)
        combined = [(prev[w] ^ reference[w]) & _MASK64 for w in range(WORDS_PER_BLOCK)]
        mixed = _block_mix(combined, pass_, lane, block_index)
        if pass_ == 0:
            _set_block(memory, lane, block_index, blocks_per_lane, mixed)
        else:
            cur = _block_view(memory, lane, block_index, blocks_per_lane)
            _set_block(
                memory,
                lane,
                block_index,
                blocks_per_lane,
                [(cur[w] ^ mixed[w]) & _MASK64 for w in range(WORDS_PER_BLOCK)],
            )


def derive_hash(password: bytes, salt: bytes, params: Params) -> bytes:
    params.validate()
    _validate_salt(salt)

    seed = ForgeX.hash(TAG_SEED, _build_material(password, salt, params))
    parallelism = params.parallelism
    blocks_per_lane = params.blocks_per_lane
    slice_length = params.slice_length
    memory = [0] * (params.block_count * WORDS_PER_BLOCK)

    for lane in range(parallelism):
        for i in range(2):
            expand_input = seed + struct.pack("<II", lane, i)
            block_bytes = ForgeX.xof(TAG_EXPAND, expand_input, BLOCK_SIZE)
            _set_block(memory, lane, i, blocks_per_lane, _bytes_to_words(block_bytes))

    for pass_ in range(params.iterations):
        for slice_ in range(4):
            for lane in range(parallelism):
                _process_lane_slice(
                    memory, pass_, slice_, lane, parallelism, blocks_per_lane, slice_length
                )

    fold = [0] * WORDS_PER_BLOCK
    last = blocks_per_lane - 1
    q1 = blocks_per_lane // 4
    q2 = blocks_per_lane // 2
    q3 = (blocks_per_lane * 3) // 4
    for lane in range(parallelism):
        for index in (last, q1, q2, q3):
            blk = _block_view(memory, lane, index, blocks_per_lane)
            for w in range(WORDS_PER_BLOCK):
                fold[w] ^= blk[w]

    fold_bytes = _words_to_bytes(fold)
    final_input = (
        seed
        + fold_bytes
        + struct.pack(
            "<IIII",
            params.memory_kib,
            params.iterations,
            params.parallelism,
            params.output_length,
        )
    )
    root = ForgeX.hash(TAG_FINAL, final_input)
    return ForgeX.xof(TAG_OUTPUT, root + seed, params.output_length)
