"""ForgeHash-B3 v1 parameter definitions and validation (SPECIFICATION.md §7)."""

from __future__ import annotations

from dataclasses import dataclass

DEFAULT_OUTPUT_LENGTH = 32
ABSOLUTE_MAX_PARALLELISM = 255
MIN_MEMORY_KIB = 8192
MAX_MEMORY_KIB = 1_048_576
MIN_ITERATIONS = 1
MAX_ITERATIONS = 20
MAX_PARALLELISM_POLICY = 64
MIN_SALT = 16
MAX_SALT = 64
MIN_OUTPUT = 16
MAX_OUTPUT = 64
MAX_PASSWORD = 1_048_576


@dataclass(slots=True)
class Params:
    """ForgeHash-B3 cost / output parameters."""

    memory_kib: int = 65_536
    iterations: int = 3
    parallelism: int = 1
    output_length: int = DEFAULT_OUTPUT_LENGTH
    salt_length: int = 16

    @classmethod
    def interactive(cls) -> Params:
        """Recommended interactive profile: 64 MiB, 3 iterations, 1 lane."""
        return cls(
            memory_kib=65_536,
            iterations=3,
            parallelism=1,
            output_length=DEFAULT_OUTPUT_LENGTH,
            salt_length=16,
        )

    @classmethod
    def development(cls) -> Params:
        """Development-only profile: fast, low cost. Never use in production."""
        return cls(
            memory_kib=MIN_MEMORY_KIB,
            iterations=1,
            parallelism=1,
            output_length=DEFAULT_OUTPUT_LENGTH,
            salt_length=16,
        )

    @classmethod
    def sensitive(cls) -> Params:
        """Recommended sensitive profile: 256 MiB, 4 iterations, 2 lanes."""
        return cls(
            memory_kib=262_144,
            iterations=4,
            parallelism=2,
            output_length=DEFAULT_OUTPUT_LENGTH,
            salt_length=16,
        )

    def validate(self) -> None:
        if not isinstance(self.memory_kib, int) or not (
            MIN_MEMORY_KIB <= self.memory_kib <= MAX_MEMORY_KIB
        ):
            raise ValueError("ForgeHash: memory out of range")
        if not isinstance(self.iterations, int) or not (
            MIN_ITERATIONS <= self.iterations <= MAX_ITERATIONS
        ):
            raise ValueError("ForgeHash: iterations out of range")
        if not isinstance(self.parallelism, int) or not (
            1 <= self.parallelism <= MAX_PARALLELISM_POLICY
            and self.parallelism <= ABSOLUTE_MAX_PARALLELISM
        ):
            raise ValueError("ForgeHash: parallelism out of range")
        if self.memory_kib < self.parallelism * 8:
            raise ValueError("ForgeHash: memory too small for lane count")
        if self.memory_kib % self.parallelism != 0:
            raise ValueError("ForgeHash: memory not divisible by parallelism")
        blocks_per_lane = self.memory_kib // self.parallelism
        if blocks_per_lane % 4 != 0:
            raise ValueError("ForgeHash: blocks per lane not divisible by 4")
        if not isinstance(self.output_length, int) or not (
            MIN_OUTPUT <= self.output_length <= MAX_OUTPUT
        ):
            raise ValueError("ForgeHash: output length out of range")
        if not isinstance(self.salt_length, int) or not (
            MIN_SALT <= self.salt_length <= MAX_SALT
        ):
            raise ValueError("ForgeHash: salt length out of range")
