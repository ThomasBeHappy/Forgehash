"""Experimental ForgeHash-X v0 — Python reference (custom ForgeX sponge).

**Not for production password storage. Not compatible with ForgeHash-B3.**
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
]

BytesLike = Union[bytes, bytearray, memoryview, str]


def _to_bytes(value: BytesLike, label: str) -> bytes:
    if isinstance(value, (bytes, bytearray, memoryview)):
        return bytes(value)
    if isinstance(value, str):
        return value.encode("utf-8")
    raise TypeError(f"ForgeHash-X: {label} must be bytes-like or str")


def hash_password(password: BytesLike, params: Params | None = None) -> str:
    if params is None:
        params = Params.toy()
    params.validate()
    password_b = _to_bytes(password, "password")
    salt = secrets.token_bytes(params.salt_length)
    hash_ = derive_hash(password_b, salt, params)
    return encode(params, salt, hash_)


def verify_password(password: BytesLike, encoded_hash: str) -> bool:
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
