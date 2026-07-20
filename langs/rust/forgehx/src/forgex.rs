//! ForgeX sponge (SPECIFICATION_X §4).

use crate::forgeperm::{self, WORDS};

pub const RATE_WORDS: usize = 8;
pub const RATE_BYTES: usize = RATE_WORDS * 8;

pub struct ForgeX {
    state: [u64; WORDS],
    offset: usize,
    squeezing: bool,
}

impl ForgeX {
    pub fn new() -> Self {
        Self {
            state: [0u64; WORDS],
            offset: 0,
            squeezing: false,
        }
    }

    /// AbsorbDomain + pad + squeeze 32 bytes.
    pub fn hash(domain_tag: &str, data: &[u8]) -> [u8; 32] {
        let mut x = Self::new();
        x.absorb_domain(domain_tag, data);
        let mut out = [0u8; 32];
        x.squeeze_into(&mut out);
        out
    }

    /// AbsorbDomain + pad + squeeze `length` bytes.
    pub fn xof(domain_tag: &str, data: &[u8], length: usize) -> Vec<u8> {
        let mut x = Self::new();
        x.absorb_domain(domain_tag, data);
        let mut out = vec![0u8; length];
        x.squeeze_into(&mut out);
        out
    }

    pub fn absorb_domain(&mut self, domain_tag: &str, data: &[u8]) {
        let tag = domain_tag.as_bytes();
        self.absorb(&(tag.len() as u32).to_le_bytes());
        self.absorb(tag);
        self.absorb(data);
    }

    pub fn absorb(&mut self, data: &[u8]) {
        assert!(!self.squeezing, "ForgeX: cannot absorb after squeezing");
        let mut rate = self.rate_bytes();
        for &b in data {
            rate[self.offset] ^= b;
            self.offset += 1;
            if self.offset == RATE_BYTES {
                self.write_rate(&rate);
                forgeperm::permute(&mut self.state);
                self.offset = 0;
                rate = self.rate_bytes();
            }
        }
        self.write_rate(&rate);
    }

    pub fn squeeze_into(&mut self, out: &mut [u8]) {
        if !self.squeezing {
            self.pad_and_switch();
            self.squeezing = true;
        }
        let mut written = 0;
        let mut rate = self.rate_bytes();
        while written < out.len() {
            if self.offset == RATE_BYTES {
                forgeperm::permute(&mut self.state);
                self.offset = 0;
                rate = self.rate_bytes();
            }
            let n = (RATE_BYTES - self.offset).min(out.len() - written);
            out[written..written + n].copy_from_slice(&rate[self.offset..self.offset + n]);
            self.offset += n;
            written += n;
        }
    }

    fn pad_and_switch(&mut self) {
        let mut rate = self.rate_bytes();
        rate[self.offset] ^= 0x01;
        rate[RATE_BYTES - 1] ^= 0x80;
        self.write_rate(&rate);
        forgeperm::permute(&mut self.state);
        self.offset = 0;
    }

    fn rate_bytes(&self) -> [u8; RATE_BYTES] {
        let mut rate = [0u8; RATE_BYTES];
        for i in 0..RATE_WORDS {
            rate[i * 8..(i + 1) * 8].copy_from_slice(&self.state[i].to_le_bytes());
        }
        rate
    }

    fn write_rate(&mut self, rate: &[u8; RATE_BYTES]) {
        for i in 0..RATE_WORDS {
            let mut le = [0u8; 8];
            le.copy_from_slice(&rate[i * 8..(i + 1) * 8]);
            self.state[i] = u64::from_le_bytes(le);
        }
    }
}

impl Default for ForgeX {
    fn default() -> Self {
        Self::new()
    }
}
