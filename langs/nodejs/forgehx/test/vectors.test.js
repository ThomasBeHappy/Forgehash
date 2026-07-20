// Official ForgeHash-X v0 toy vectors — implementers/x0/vectors/*.json

import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

import { Params, deriveSeed, deriveHash, encode } from "../src/index.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const VECTORS_DIR = path.resolve(
  __dirname,
  "../../../../implementers/x0/vectors"
);

function hexToBuffer(hex) {
  return Buffer.from(hex, "hex");
}

function loadVectors() {
  const files = readdirSync(VECTORS_DIR)
    .filter((f) => f.endsWith(".json"))
    .sort();
  return files.map((file) => {
    const contents = JSON.parse(
      readFileSync(path.join(VECTORS_DIR, file), "utf8")
    );
    return { file, vector: contents };
  });
}

for (const { file, vector } of loadVectors()) {
  test(`x0 vector: ${file}`, { timeout: 600_000 }, () => {
    const password = hexToBuffer(vector.passwordHex);
    const salt = hexToBuffer(vector.saltHex);
    const params = new Params({
      memoryKiB: vector.memoryKiB,
      iterations: vector.iterations,
      parallelism: vector.parallelism,
      outputLength: vector.outputLength,
      saltLength: salt.length,
    });

    const seed = deriveSeed(password, salt, params);
    assert.equal(seed.toString("hex"), vector.seedHex, `seed mismatch for ${file}`);

    const hash = deriveHash(password, salt, params);
    assert.equal(hash.toString("hex"), vector.hashHex, `hash mismatch for ${file}`);

    const encoded = encode(params, salt, hash);
    assert.equal(encoded, vector.encoded, `encoded mismatch for ${file}`);
  });
}
