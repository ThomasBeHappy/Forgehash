"""Sandbox parameters for ForgeHash-X v0."""

from __future__ import annotations

from dataclasses import dataclass

BLOCK_SIZE = 512
WORDS_PER_BLOCK = BLOCK_SIZE // 8
MIN_MEMORY_KIB = 256
MAX_MEMORY_KIB = 65_536
MIN_ITERATIONS = 1
MAX_ITERATIONS = 8
MIN_PARALLELISM = 1
MAX_PARALLELISM = 16
MIN_OUTPUT_LENGTH = 16
MAX_OUTPUT_LENGTH = 64
MIN_SALT_LENGTH = 16
MAX_SALT_LENGTH = 64
DEFAULT_OUTPUT_LENGTH = 32


@dataclass(slots=True)
class Params:
    memory_kib: int = 1024
    iterations: int = 1
    parallelism: int = 1
    output_length: int = DEFAULT_OUTPUT_LENGTH
    salt_length: int = 16

    @classmethod
    def toy(cls) -> Params:
        return cls(
            memory_kib=1024,
            iterations=1,
            parallelism=1,
            output_length=DEFAULT_OUTPUT_LENGTH,
            salt_length=16,
        )

    @property
    def block_count(self) -> int:
        return self.memory_kib * 1024 // BLOCK_SIZE

    @property
    def blocks_per_lane(self) -> int:
        return self.block_count // self.parallelism

    @property
    def slice_length(self) -> int:
        return self.blocks_per_lane // 4

    def validate(self) -> None:
        if not (MIN_MEMORY_KIB <= self.memory_kib <= MAX_MEMORY_KIB):
            raise ValueError("ForgeHash-X: memoryKiB out of sandbox range")
        if (self.memory_kib * 1024) % BLOCK_SIZE != 0:
            raise ValueError("ForgeHash-X: memory must yield whole 512-byte blocks")
        if not (MIN_ITERATIONS <= self.iterations <= MAX_ITERATIONS):
            raise ValueError("ForgeHash-X: iterations out of range")
        if not (MIN_PARALLELISM <= self.parallelism <= MAX_PARALLELISM):
            raise ValueError("ForgeHash-X: parallelism out of range")
        if self.block_count % self.parallelism != 0:
            raise ValueError("ForgeHash-X: blockCount must be divisible by parallelism")
        if self.blocks_per_lane % 4 != 0:
            raise ValueError("ForgeHash-X: blocksPerLane must be divisible by 4")
        if self.blocks_per_lane < 8:
            raise ValueError("ForgeHash-X: blocksPerLane must be at least 8")
        if not (MIN_OUTPUT_LENGTH <= self.output_length <= MAX_OUTPUT_LENGTH):
            raise ValueError("ForgeHash-X: outputLength out of range")
        if not (MIN_SALT_LENGTH <= self.salt_length <= MAX_SALT_LENGTH):
            raise ValueError("ForgeHash-X: saltLength out of range")
