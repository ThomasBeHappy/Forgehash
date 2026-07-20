"""ForgePerm / ForgeX known-answer tests from implementers/x0/kats."""

from __future__ import annotations

import json
from pathlib import Path

from forgehx.forgeperm import permute, round_constant
from forgehx.forgex import ForgeX

ROOT = Path(__file__).resolve().parents[4]
KAT = ROOT / "implementers" / "x0" / "kats" / "forgex_primitive.json"


def test_round_constants_and_zero_permute():
    data = json.loads(KAT.read_text(encoding="utf-8"))
    assert f"{round_constant(0, 0):016x}" == data["roundConstants"]["r0_i0"]
    assert f"{round_constant(7, 15):016x}" == data["roundConstants"]["r7_i15"]

    state = [0] * 16
    permute(state)
    expected = [int(w, 16) for w in data["zeroStatePermute"]]
    assert state == expected


def test_hash_and_xof_kats():
    data = json.loads(KAT.read_text(encoding="utf-8"))
    for item in data["hash"]:
        out = ForgeX.hash(item["domainTag"], bytes.fromhex(item["dataHex"]))
        assert out.hex() == item["outHex"]
    for item in data["xof"]:
        out = ForgeX.xof(item["domainTag"], bytes.fromhex(item["dataHex"]), item["length"])
        assert out.hex() == item["outHex"]
