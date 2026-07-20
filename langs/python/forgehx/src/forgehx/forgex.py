"""ForgeX sponge (SPECIFICATION_X §4)."""

from __future__ import annotations

import struct

from . import forgeperm

STATE_WORDS = 16
RATE_WORDS = 8
RATE_BYTES = RATE_WORDS * 8


class ForgeX:
    def __init__(self) -> None:
        self._state = [0] * STATE_WORDS
        self._offset = 0
        self._squeezing = False

    @staticmethod
    def hash(domain_tag: str, data: bytes) -> bytes:
        x = ForgeX()
        x.absorb_domain(domain_tag, data)
        return x.squeeze(32)

    @staticmethod
    def xof(domain_tag: str, data: bytes, length: int) -> bytes:
        if length < 0:
            raise ValueError("ForgeX: negative length")
        x = ForgeX()
        x.absorb_domain(domain_tag, data)
        return x.squeeze(length)

    def absorb_domain(self, domain_tag: str, data: bytes) -> None:
        tag = domain_tag.encode("ascii")
        self.absorb(struct.pack("<I", len(tag)))
        self.absorb(tag)
        self.absorb(data)

    def absorb(self, data: bytes) -> None:
        if self._squeezing:
            raise RuntimeError("ForgeX: cannot absorb after squeezing")
        rate = self._rate_bytes()
        for b in data:
            rate[self._offset] ^= b
            self._offset += 1
            if self._offset == RATE_BYTES:
                self._write_rate(rate)
                forgeperm.permute(self._state)
                self._offset = 0
                rate = self._rate_bytes()
        self._write_rate(rate)

    def squeeze(self, length: int) -> bytes:
        if not self._squeezing:
            self._pad_and_switch()
            self._squeezing = True
        out = bytearray(length)
        written = 0
        rate = self._rate_bytes()
        while written < length:
            if self._offset == RATE_BYTES:
                forgeperm.permute(self._state)
                self._offset = 0
                rate = self._rate_bytes()
            n = min(RATE_BYTES - self._offset, length - written)
            out[written : written + n] = rate[self._offset : self._offset + n]
            self._offset += n
            written += n
        return bytes(out)

    def _pad_and_switch(self) -> None:
        rate = self._rate_bytes()
        rate[self._offset] ^= 0x01
        rate[RATE_BYTES - 1] ^= 0x80
        self._write_rate(rate)
        forgeperm.permute(self._state)
        self._offset = 0

    def _rate_bytes(self) -> bytearray:
        return bytearray(struct.pack("<" + "Q" * RATE_WORDS, *self._state[:RATE_WORDS]))

    def _write_rate(self, rate: bytearray) -> None:
        words = struct.unpack("<" + "Q" * RATE_WORDS, bytes(rate))
        self._state[:RATE_WORDS] = list(words)
