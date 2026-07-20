"""ForgeHash-B3 v1 core engine (SPECIFICATION.md §§9–17)."""

from __future__ import annotations

import struct
from typing import List

from blake3 import blake3

from .forgemix import (
    BLOCK_SIZE,
    MASK64,
    WORDS,
    WordBlock,
    bytes_to_words,
    mix,
    rotl,
    words_to_bytes,
    xor_words,
)
from .params import MAX_PASSWORD, Params

SEED_CTX = "ForgeHash/v1/seed"
EXPAND_PREFIX = b"ForgeHash/v1/expand"
GROUP_PREFIX = b"ForgeHash/v1/group"
GROUP_ROOT_PREFIX = b"ForgeHash/v1/group-root"
FINAL_PREFIX = b"ForgeHash/v1/final"
OUTPUT_PREFIX = b"ForgeHash/v1/output"


def _le32(v: int) -> bytes:
    return struct.pack("<I", v & 0xFFFFFFFF)


def _le64(v: int) -> bytes:
    return struct.pack("<Q", v & MASK64)


def _build_encoded_input(password: bytes, salt: bytes, params: Params) -> bytes:
    return b"".join(
        (
            _le32(1),
            _le32(params.memory_kib),
            _le32(params.iterations),
            _le32(params.parallelism),
            _le32(params.output_length),
            _le64(len(password)),
            password,
            _le32(len(salt)),
            salt,
            _le32(0),
        )
    )


def _derive_seed_bytes(material: bytes) -> bytes:
    return blake3(material, derive_key_context=SEED_CTX).digest()


def _expand(input_bytes: bytes, out_len: int) -> bytes:
    return blake3(EXPAND_PREFIX + input_bytes).digest(length=out_len)


def _blake3_hash(input_bytes: bytes) -> bytes:
    return blake3(input_bytes).digest()


def _blake3_xof(input_bytes: bytes, out_len: int) -> bytes:
    return blake3(input_bytes).digest(length=out_len)


def _fast_range(x: int, n: int) -> int:
    return ((x & MASK64) * (n & MASK64)) >> 64


class _Memory:
    __slots__ = ("parallelism", "blocks_per_lane", "slice_length", "words")

    def __init__(self, block_count: int, parallelism: int) -> None:
        self.parallelism = parallelism
        self.blocks_per_lane = block_count // parallelism
        self.slice_length = self.blocks_per_lane // 4
        self.words: List[int] = [0] * (block_count * WORDS)

    def flat_offset(self, lane: int, index: int) -> int:
        return (lane * self.blocks_per_lane + index) * WORDS

    def get_block(self, lane: int, index: int, out: WordBlock | None = None) -> WordBlock:
        start = self.flat_offset(lane, index)
        dest = out if out is not None else [0] * WORDS
        dest[:] = self.words[start : start + WORDS]
        return dest

    def set_block(self, lane: int, index: int, block: WordBlock) -> None:
        start = self.flat_offset(lane, index)
        self.words[start : start + WORDS] = block


def derive_seed(password: bytes, salt: bytes, params: Params) -> bytes:
    if len(password) > MAX_PASSWORD:
        raise ValueError("ForgeHash: password too long")
    params.validate()
    if not (16 <= len(salt) <= 64):
        raise ValueError("ForgeHash: salt length out of range")
    return _derive_seed_bytes(_build_encoded_input(password, salt, params))


def derive_hash(password: bytes, salt: bytes, params: Params) -> bytes:
    if len(password) > MAX_PASSWORD:
        raise ValueError("ForgeHash: password too long")
    params.validate()
    if not (16 <= len(salt) <= 64):
        raise ValueError("ForgeHash: salt length out of range")

    material = _build_encoded_input(password, salt, params)
    seed = _derive_seed_bytes(material)
    memory = _Memory(params.memory_kib, params.parallelism)
    _initialize_memory(memory, seed)
    _fill_memory(memory, params.iterations)
    return _finalize(memory, seed, params)


def _initialize_memory(memory: _Memory, seed: bytes) -> None:
    for lane in range(memory.parallelism):
        for block_index in range(2):
            block_bytes = _expand(seed + _le32(lane) + _le32(block_index), BLOCK_SIZE)
            memory.set_block(lane, block_index, bytes_to_words(block_bytes))


def _fill_memory(memory: _Memory, iterations: int) -> None:
    for pass_ in range(iterations):
        for slice_ in range(4):
            for lane in range(memory.parallelism):
                _process_slice(memory, pass_, slice_, lane)


_scratch_previous: WordBlock = [0] * WORDS
_scratch_reference: WordBlock = [0] * WORDS
_scratch_combined: WordBlock = [0] * WORDS
_scratch_mixed: WordBlock = [0] * WORDS
_scratch_old: WordBlock = [0] * WORDS
_scratch_output: WordBlock = [0] * WORDS


def _process_slice(memory: _Memory, pass_: int, slice_: int, lane: int) -> None:
    start = slice_ * memory.slice_length
    end = start + memory.slice_length
    if pass_ == 0 and slice_ == 0:
        start = 2

    for block_index in range(start, end):
        previous_index = block_index - 1 if block_index > 0 else memory.blocks_per_lane - 1
        memory.get_block(lane, previous_index, _scratch_previous)

        reference_lane, reference_index = _select_reference(
            memory, pass_, slice_, lane, block_index, _scratch_previous
        )
        memory.get_block(reference_lane, reference_index, _scratch_reference)
        xor_words(_scratch_previous, _scratch_reference, _scratch_combined)

        if pass_ == 0:
            mix(_scratch_combined, pass_, lane, block_index, _scratch_output)
            memory.set_block(lane, block_index, _scratch_output)
        else:
            mix(_scratch_combined, pass_, lane, block_index, _scratch_mixed)
            memory.get_block(lane, block_index, _scratch_old)
            xor_words(_scratch_old, _scratch_mixed, _scratch_output)
            memory.set_block(lane, block_index, _scratch_output)


def _select_reference(
    memory: _Memory,
    pass_: int,
    slice_: int,
    current_lane: int,
    block_index: int,
    previous: WordBlock,
) -> tuple[int, int]:
    address_word = (
        previous[0]
        ^ rotl(previous[17], 13)
        ^ previous[73]
        ^ (pass_ & MASK64)
        ^ rotl(block_index & MASK64, 29)
    ) & MASK64

    if memory.parallelism == 1:
        reference_lane = 0
    elif block_index % 32 == 0:
        reference_lane = _fast_range(previous[1], memory.parallelism)
    else:
        reference_lane = current_lane

    allowed = _allowed_block_count(
        memory, pass_, slice_, current_lane, reference_lane, block_index
    )
    if allowed == 0:
        reference_lane = current_lane
        allowed = _allowed_block_count(
            memory, pass_, slice_, current_lane, reference_lane, block_index
        )

    reference_index = _fast_range(address_word, allowed)
    return reference_lane, reference_index


def _allowed_block_count(
    memory: _Memory,
    pass_: int,
    slice_: int,
    current_lane: int,
    reference_lane: int,
    block_index: int,
) -> int:
    if reference_lane == current_lane:
        return block_index if pass_ == 0 else memory.blocks_per_lane
    return slice_ * memory.slice_length


def _finalize(memory: _Memory, seed: bytes, params: Params) -> bytes:
    acc = [0] * WORDS
    last = memory.blocks_per_lane - 1
    quarter = memory.blocks_per_lane // 4
    half = memory.blocks_per_lane // 2
    three_quarter = (memory.blocks_per_lane * 3) // 4
    tmp = [0] * WORDS

    for lane in range(memory.parallelism):
        for index in (last, quarter, half, three_quarter):
            memory.get_block(lane, index, tmp)
            for i in range(WORDS):
                acc[i] ^= tmp[i]

    accumulator_bytes = words_to_bytes(acc)
    group_root = _compute_group_root(memory)

    root_input = b"".join(
        (
            FINAL_PREFIX,
            seed,
            accumulator_bytes,
            group_root,
            _le32(params.memory_kib),
            _le32(params.iterations),
            _le32(params.parallelism),
            _le32(params.output_length),
        )
    )
    root = _blake3_hash(root_input)
    return _blake3_xof(OUTPUT_PREFIX + root + seed, params.output_length)


def _compute_group_root(memory: _Memory) -> bytes:
    root_hasher = blake3()
    root_hasher.update(GROUP_ROOT_PREFIX)

    group_size = 64
    total = memory.parallelism * memory.blocks_per_lane
    group_count = (total + group_size - 1) // group_size
    group_buf = bytearray(group_size * BLOCK_SIZE)
    words = [0] * WORDS
    block_bytes = bytearray(BLOCK_SIZE)

    for group_index in range(group_count):
        start = group_index * group_size
        count = min(total - start, group_size)
        for i in range(count):
            flat = start + i
            lane = flat // memory.blocks_per_lane
            block_index = flat % memory.blocks_per_lane
            memory.get_block(lane, block_index, words)
            words_to_bytes(words, block_bytes)
            group_buf[i * BLOCK_SIZE : (i + 1) * BLOCK_SIZE] = block_bytes

        byte_len = count * BLOCK_SIZE
        group_hasher = blake3()
        group_hasher.update(GROUP_PREFIX)
        group_hasher.update(_le64(group_index))
        group_hasher.update(_le64(count))
        group_hasher.update(bytes(group_buf[:byte_len]))
        digest = group_hasher.digest()

        root_hasher.update(_le64(group_index))
        root_hasher.update(digest)

    return root_hasher.digest()
