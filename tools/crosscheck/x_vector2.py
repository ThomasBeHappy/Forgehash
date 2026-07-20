"""Print ForgeHash-X vector2 digest (hex). Used by cross-implementation tests."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "langs" / "python" / "forgehx" / "src"))

from forgehx import Params, derive_hash  # noqa: E402

password = bytes.fromhex("70617373776f7264")
salt = bytes.fromhex("000102030405060708090a0b0c0d0e0f")
params = Params(memory_kib=1024, iterations=1, parallelism=1)
print(derive_hash(password, salt, params).hex())
