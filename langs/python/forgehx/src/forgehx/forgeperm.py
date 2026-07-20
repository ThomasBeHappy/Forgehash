"""ForgePerm — 16-word ARX permutation (SPECIFICATION_X §4.3)."""

from __future__ import annotations

WORDS = 16
ROUNDS = 8

_MASK64 = (1 << 64) - 1


def rotl(x: int, n: int) -> int:
    n &= 63
    x &= _MASK64
    if n == 0:
        return x
    return ((x << n) | (x >> (64 - n))) & _MASK64


def rotr(x: int, n: int) -> int:
    n &= 63
    x &= _MASK64
    if n == 0:
        return x
    return ((x >> n) | (x << (64 - n))) & _MASK64


def low32(x: int) -> int:
    return x & 0xFFFFFFFF


def round_constant(round_: int, index: int) -> int:
    x = (
        0x9E3779B97F4A7C15
        ^ ((round_ & 0xFFFFFFFF) * 0xD1B54A32D192ED03)
        ^ ((index & 0xFFFFFFFF) * 0xA24BAED4963EE407)
    ) & _MASK64
    return rotl(x, (round_ + index * 3) & 63)


def _quarter_round(s: list[int], ia: int, ib: int, ic: int, id_: int) -> None:
    a, b, c, d = s[ia], s[ib], s[ic], s[id_]
    a = (a + b + 2 * low32(a) * low32(b)) & _MASK64
    d = rotr(d ^ a, 17)
    c = (c + d + 2 * low32(c) * low32(d)) & _MASK64
    b = rotr(b ^ c, 11)
    a = (a + b + 2 * low32(a) * low32(b)) & _MASK64
    d = rotr(d ^ a, 23)
    c = (c + d + 2 * low32(c) * low32(d)) & _MASK64
    b = rotr(b ^ c, 41)
    s[ia], s[ib], s[ic], s[id_] = a, b, c, d


def permute(state: list[int]) -> None:
    if len(state) != WORDS:
        raise ValueError("ForgePerm: state must be 16 words")
    temp = [0] * WORDS
    for r in range(ROUNDS):
        for i in range(WORDS):
            state[i] = (state[i] ^ round_constant(r, i)) & _MASK64
        _quarter_round(state, 0, 4, 8, 12)
        _quarter_round(state, 1, 5, 9, 13)
        _quarter_round(state, 2, 6, 10, 14)
        _quarter_round(state, 3, 7, 11, 15)
        _quarter_round(state, 0, 5, 10, 15)
        _quarter_round(state, 1, 6, 11, 12)
        _quarter_round(state, 2, 7, 8, 13)
        _quarter_round(state, 3, 4, 9, 14)
        for i in range(WORDS):
            temp[(i * 7 + 3) & 15] = state[i]
        state[:] = temp
