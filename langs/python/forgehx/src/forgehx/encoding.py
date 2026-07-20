"""Canonical `$forgehx$v=0$...` encoding / parsing."""

from __future__ import annotations

import base64
import re
from dataclasses import dataclass

from .params import MIN_OUTPUT_LENGTH, MAX_OUTPUT_LENGTH, Params

_B64_RE = re.compile(r"^[A-Za-z0-9+/]+$")
_WS_RE = re.compile(r"\s")

ALGORITHM_ID = "forgehx"
VERSION = 0


def b64_encode(data: bytes) -> str:
    return base64.b64encode(data).decode("ascii").rstrip("=")


def b64_decode(text: str) -> bytes:
    if not text:
        raise ValueError("ForgeHash-X: empty base64 field")
    if not _B64_RE.fullmatch(text):
        raise ValueError("ForgeHash-X: malformed base64")
    rem = len(text) % 4
    if rem == 1:
        raise ValueError("ForgeHash-X: malformed base64 length")
    padded = text + ("=" * ((4 - rem) % 4))
    try:
        decoded = base64.b64decode(padded, validate=True)
    except Exception as exc:
        raise ValueError("ForgeHash-X: malformed base64") from exc
    if b64_encode(decoded) != text:
        raise ValueError("ForgeHash-X: non-canonical base64")
    return decoded


def encode(params: Params, salt: bytes, hash_: bytes) -> str:
    return (
        f"${ALGORITHM_ID}$v={VERSION}$m={params.memory_kib},t={params.iterations},"
        f"p={params.parallelism}${b64_encode(salt)}${b64_encode(hash_)}"
    )


def _parse_strict_non_negative_int(text: str) -> int:
    if not text or text[0] in "+-" or not text.isdigit():
        raise ValueError("ForgeHash-X: invalid integer field")
    if len(text) > 1 and text.startswith("0"):
        raise ValueError("ForgeHash-X: leading zero not allowed")
    return int(text)


def _parse_strict_positive_int(text: str) -> int:
    value = _parse_strict_non_negative_int(text)
    if value == 0:
        raise ValueError("ForgeHash-X: integer field out of range")
    return value


def _parse_cost_field(segment: str, name: str) -> int:
    eq = segment.find("=")
    if eq == -1:
        raise ValueError("ForgeHash-X: malformed cost field")
    if segment[:eq] != name:
        raise ValueError(f"ForgeHash-X: expected field '{name}'")
    return _parse_strict_positive_int(segment[eq + 1 :])


@dataclass(slots=True)
class ParsedHash:
    version: int
    params: Params
    salt: bytes
    hash: bytes
    encoded: str


def parse(encoded: str) -> ParsedHash:
    if not isinstance(encoded, str):
        raise ValueError("ForgeHash-X: encoded hash must be a string")
    if "\0" in encoded or _WS_RE.search(encoded):
        raise ValueError("ForgeHash-X: whitespace or null byte in encoded hash")

    parts = encoded.split("$")
    if len(parts) != 6 or parts[0] != "":
        raise ValueError("ForgeHash-X: malformed encoded hash")

    _, algorithm, version_field, costs_field, salt_field, hash_field = parts
    if algorithm != ALGORITHM_ID:
        raise ValueError("ForgeHash-X: unsupported algorithm identifier")
    if not version_field.startswith("v="):
        raise ValueError("ForgeHash-X: malformed version field")
    version = _parse_strict_non_negative_int(version_field[2:])
    if version != VERSION:
        raise ValueError("ForgeHash-X: unsupported algorithm version")

    cost_segments = costs_field.split(",")
    if len(cost_segments) != 3:
        raise ValueError("ForgeHash-X: malformed cost parameters")
    memory_kib = _parse_cost_field(cost_segments[0], "m")
    iterations = _parse_cost_field(cost_segments[1], "t")
    parallelism = _parse_cost_field(cost_segments[2], "p")

    salt = b64_decode(salt_field)
    hash_ = b64_decode(hash_field)
    if not (MIN_OUTPUT_LENGTH <= len(hash_) <= MAX_OUTPUT_LENGTH):
        raise ValueError("ForgeHash-X: invalid hash length")

    params = Params(
        memory_kib=memory_kib,
        iterations=iterations,
        parallelism=parallelism,
        output_length=len(hash_),
        salt_length=len(salt),
    )
    params.validate()

    canonical = encode(params, salt, hash_)
    if canonical != encoded:
        raise ValueError("ForgeHash-X: encoded hash is not canonical")

    return ParsedHash(version=version, params=params, salt=salt, hash=hash_, encoded=canonical)
