"""Experimental ForgeHash-B3 v1 — Python reference implementation.

**Not for production password storage.**
"""

from __future__ import annotations

import secrets
from typing import Union

from .encoding import ParsedHash, encode, parse
from .engine import derive_hash, derive_seed
from .params import Params

__all__ = [
    "Params",
    "ParsedHash",
    "derive_hash",
    "derive_seed",
    "encode",
    "parse",
    "hash_password",
    "verify_password",
    "needs_rehash",
]

BytesLike = Union[bytes, bytearray, memoryview, str]


def _to_bytes(value: BytesLike, label: str) -> bytes:
    if isinstance(value, (bytes, bytearray, memoryview)):
        return bytes(value)
    if isinstance(value, str):
        return value.encode("utf-8")
    raise TypeError(f"ForgeHash: {label} must be bytes-like or str")


def hash_password(password: BytesLike, params: Params | None = None) -> str:
    """Hash a password with a fresh random salt; return the canonical encoded string."""
    if params is None:
        params = Params.interactive()
    params.validate()
    password_b = _to_bytes(password, "password")
    salt = secrets.token_bytes(params.salt_length)
    hash_ = derive_hash(password_b, salt, params)
    return encode(1, params, salt, hash_)


def verify_password(password: BytesLike, encoded_hash: str) -> bool:
    """Constant-time verify; returns False for any malformed / mismatched input."""
    try:
        parsed = parse(encoded_hash)
    except Exception:
        return False

    try:
        password_b = _to_bytes(password, "password")
        actual = derive_hash(password_b, parsed.salt, parsed.params)
    except Exception:
        return False

    if len(actual) != len(parsed.hash):
        return False

    diff = 0
    for a, b in zip(actual, parsed.hash):
        diff |= a ^ b
    return diff == 0


def needs_rehash(encoded_hash: str, desired_params: Params) -> bool:
    """True if the stored hash should be regenerated under desired_params."""
    try:
        parsed = parse(encoded_hash)
    except Exception:
        return True
    if parsed.version != 1:
        return True
    if parsed.memory_kib < desired_params.memory_kib:
        return True
    if parsed.iterations < desired_params.iterations:
        return True
    if parsed.parallelism != desired_params.parallelism:
        return True
    if len(parsed.hash) != desired_params.output_length:
        return True
    if len(parsed.salt) < desired_params.salt_length:
        return True
    return False
