"""Canonical `$forgeh$...` encoding / parsing (SPECIFICATION.md §6)."""

from __future__ import annotations

import base64
import re
from dataclasses import dataclass

from .params import DEFAULT_OUTPUT_LENGTH, Params

_B64_RE = re.compile(r"^[A-Za-z0-9+/]+$")
_WS_RE = re.compile(r"\s")


def b64_encode(data: bytes) -> str:
    return base64.b64encode(data).decode("ascii").rstrip("=")


def b64_decode(text: str) -> bytes:
    if not text:
        raise ValueError("ForgeHash: empty base64 field")
    if not _B64_RE.fullmatch(text):
        raise ValueError("ForgeHash: malformed base64")
    rem = len(text) % 4
    if rem == 1:
        raise ValueError("ForgeHash: malformed base64 length")
    padded = text + ("=" * ((4 - rem) % 4))
    try:
        decoded = base64.b64decode(padded, validate=True)
    except Exception as exc:
        raise ValueError("ForgeHash: malformed base64") from exc
    if b64_encode(decoded) != text:
        raise ValueError("ForgeHash: non-canonical base64")
    return decoded


def encode(version: int, params: Params, salt: bytes, hash_: bytes) -> str:
    return (
        f"$forgeh$v={version}$m={params.memory_kib},t={params.iterations},"
        f"p={params.parallelism}${b64_encode(salt)}${b64_encode(hash_)}"
    )


def _parse_strict_positive_int(text: str) -> int:
    if not text or text[0] in "+-" or not text.isdigit():
        raise ValueError("ForgeHash: invalid integer field")
    if len(text) > 1 and text.startswith("0"):
        raise ValueError("ForgeHash: leading zero not allowed")
    value = int(text)
    if value == 0:
        raise ValueError("ForgeHash: integer field out of range")
    return value


def _parse_cost_field(segment: str, name: str) -> int:
    eq = segment.find("=")
    if eq == -1:
        raise ValueError("ForgeHash: malformed cost field")
    key = segment[:eq]
    value = segment[eq + 1 :]
    if key != name:
        raise ValueError(f"ForgeHash: expected field '{name}'")
    return _parse_strict_positive_int(value)


@dataclass(slots=True)
class ParsedHash:
    version: int
    memory_kib: int
    iterations: int
    parallelism: int
    salt: bytes
    hash: bytes
    params: Params
    encoded: str


def parse(encoded: str) -> ParsedHash:
    if not isinstance(encoded, str):
        raise ValueError("ForgeHash: encoded hash must be a string")
    if "\0" in encoded or _WS_RE.search(encoded):
        raise ValueError("ForgeHash: whitespace or null byte in encoded hash")

    parts = encoded.split("$")
    if len(parts) != 6 or parts[0] != "":
        raise ValueError("ForgeHash: malformed encoded hash")

    _, algorithm, version_field, costs_field, salt_field, hash_field = parts
    if algorithm != "forgeh":
        raise ValueError("ForgeHash: unsupported algorithm identifier")
    if not version_field.startswith("v="):
        raise ValueError("ForgeHash: malformed version field")
    version = _parse_strict_positive_int(version_field[2:])
    if version != 1:
        raise ValueError("ForgeHash: unsupported algorithm version")

    cost_segments = costs_field.split(",")
    if len(cost_segments) != 3:
        raise ValueError("ForgeHash: malformed cost parameters")
    memory_kib = _parse_cost_field(cost_segments[0], "m")
    iterations = _parse_cost_field(cost_segments[1], "t")
    parallelism = _parse_cost_field(cost_segments[2], "p")

    salt = b64_decode(salt_field)
    hash_ = b64_decode(hash_field)
    if len(hash_) != DEFAULT_OUTPUT_LENGTH:
        raise ValueError("ForgeHash: invalid hash length")

    params = Params(
        memory_kib=memory_kib,
        iterations=iterations,
        parallelism=parallelism,
        output_length=len(hash_),
        salt_length=len(salt),
    )
    params.validate()

    canonical = encode(version, params, salt, hash_)
    if canonical != encoded:
        raise ValueError("ForgeHash: encoded hash is not canonical")

    return ParsedHash(
        version=version,
        memory_kib=memory_kib,
        iterations=iterations,
        parallelism=parallelism,
        salt=salt,
        hash=hash_,
        params=params,
        encoded=canonical,
    )
