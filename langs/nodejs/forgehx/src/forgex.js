// ForgeX sponge (SPECIFICATION_X §4).

import { permute } from "./forgeperm.js";

export const STATE_WORDS = 16;
export const RATE_WORDS = 8;
export const RATE_BYTES = RATE_WORDS * 8;

const MASK64 = 0xffffffffffffffffn;

function le32(v) {
  const b = Buffer.alloc(4);
  b.writeUInt32LE(v >>> 0, 0);
  return b;
}

export class ForgeX {
  constructor() {
    this._state = new Array(STATE_WORDS).fill(0n);
    this._offset = 0;
    this._squeezing = false;
  }

  static hash(domainTag, data) {
    const x = new ForgeX();
    x.absorbDomain(domainTag, data);
    return x.squeeze(32);
  }

  static xof(domainTag, data, length) {
    if (length < 0) {
      throw new RangeError("ForgeX: negative length");
    }
    const x = new ForgeX();
    x.absorbDomain(domainTag, data);
    return x.squeeze(length);
  }

  absorbDomain(domainTag, data) {
    const tag = Buffer.from(domainTag, "ascii");
    this.absorb(le32(tag.length));
    this.absorb(tag);
    this.absorb(data);
  }

  absorb(data) {
    if (this._squeezing) {
      throw new Error("ForgeX: cannot absorb after squeezing");
    }
    const buf = Buffer.isBuffer(data) ? data : Buffer.from(data);
    let rate = this._rateBytes();
    for (let i = 0; i < buf.length; i++) {
      rate[this._offset] ^= buf[i];
      this._offset += 1;
      if (this._offset === RATE_BYTES) {
        this._writeRate(rate);
        permute(this._state);
        this._offset = 0;
        rate = this._rateBytes();
      }
    }
    this._writeRate(rate);
  }

  squeeze(length) {
    if (!this._squeezing) {
      this._padAndSwitch();
      this._squeezing = true;
    }
    const out = Buffer.alloc(length);
    let written = 0;
    let rate = this._rateBytes();
    while (written < length) {
      if (this._offset === RATE_BYTES) {
        permute(this._state);
        this._offset = 0;
        rate = this._rateBytes();
      }
      const n = Math.min(RATE_BYTES - this._offset, length - written);
      rate.copy(out, written, this._offset, this._offset + n);
      this._offset += n;
      written += n;
    }
    return out;
  }

  _padAndSwitch() {
    const rate = this._rateBytes();
    rate[this._offset] ^= 0x01;
    rate[RATE_BYTES - 1] ^= 0x80;
    this._writeRate(rate);
    permute(this._state);
    this._offset = 0;
  }

  _rateBytes() {
    const rate = Buffer.alloc(RATE_BYTES);
    for (let i = 0; i < RATE_WORDS; i++) {
      rate.writeBigUInt64LE(this._state[i] & MASK64, i * 8);
    }
    return rate;
  }

  _writeRate(rate) {
    for (let i = 0; i < RATE_WORDS; i++) {
      this._state[i] = rate.readBigUInt64LE(i * 8);
    }
  }
}
