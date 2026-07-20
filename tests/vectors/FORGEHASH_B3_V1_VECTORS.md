# ForgeHash-B3 Version 1 Official Test Vectors

These vectors freeze the ForgeHash-B3 v1 algorithm. Incompatible changes require `v=2`.

> Experimental cryptography. Not for production password storage.

## Vector 1: `vector1_empty_password_zero_salt`

| Field | Value |
|-------|-------|
| password_hex | `` |
| salt_hex | `00000000000000000000000000000000` |
| memory_kib | `8192` |
| iterations | `1` |
| parallelism | `1` |
| seed_hex | `0b7db6e3161a3bfd38b6316d5ad782790df16668db6279be16c5151e1c17b6bc` |
| group_root_hex | `c9aa3c72291c5db25d98ecd25ef52e298deb1074641ed95c9d77fb5e087e8a99` |
| hash_hex | `50aa2141813479be95d66a46efa0e076191addb64d1cf0a3cc832c6c9b54be4e` |
| encoded | `$forgeh$v=1$m=8192,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$UKohQYE0eb6V1mpG76Dgdhka3bZNHPCjzIMsbJtUvk4` |

### Initialized block prefixes (32 bytes)

- lane 0 block 0: `c7d2de85cb51df8317a8b8d3414d4aaff11562445d4acf1fb70b65d13deb3695`
- lane 0 block 1: `ac711e39b71108d4a5049f3beddb5464c0ebd5a474f453e3fccb17617f8f6754`

### Sample references

| pass | lane | block | prev | refLane | refBlock | cross |
|-----:|-----:|------:|-----:|--------:|---------:|:-----:|
| 0 | 0 | 2 | 1 | 0 | 0 | no |
| 0 | 0 | 32 | 31 | 0 | 21 | no |
| 0 | 0 | 4096 | 4095 | 0 | 1851 | no |
| 0 | 0 | 8191 | 8190 | 0 | 1446 | no |

### ForgeMix sample prefixes (32 bytes)

- pass 0 lane 0 block 2: `5dbe94e9f5e6f7695ac6c437f520d871dae5f772766d61a4ea9f794ba9bd6184`
- pass 0 lane 0 block 2048: `15848bbbdca49b9fc89a90ffa4b115596ded14b7656160fefaff4fa770d104ec`
- pass 0 lane 0 block 8191: `148aa2bf4afc9954e4822f893cc357d9bf6996de2c12a4f4cc6e25eee33d53b1`

## Vector 2: `vector2_password_incrementing_salt`

| Field | Value |
|-------|-------|
| password_hex | `70617373776f7264` |
| salt_hex | `000102030405060708090a0b0c0d0e0f` |
| memory_kib | `8192` |
| iterations | `1` |
| parallelism | `1` |
| seed_hex | `ddd972ef2d252014300be716c6019dc992b34e3def366b17fb6c31a169d790f5` |
| group_root_hex | `0c9ce5dac651519ed636354573d3e16895f5dc1c07d7df89a3a86c7da1dcd5b1` |
| hash_hex | `02acdfa7faa0f149fe700b2f46b792fda8eaecd5f14844142c67709c561a6a98` |
| encoded | `$forgeh$v=1$m=8192,t=1,p=1$AAECAwQFBgcICQoLDA0ODw$Aqzfp/qg8Un+cAsvRreS/ajq7NXxSEQULGdwnFYaapg` |

### Initialized block prefixes (32 bytes)

- lane 0 block 0: `515962f7ab5b77f70c71d39dbbca30e8b1ebf46a49a12b8d35fd7fd147d12ac0`
- lane 0 block 1: `dbf5a6a8576bda8defe1a581e40f10a627211715637aa296a5ace4310d3139c6`

### Sample references

| pass | lane | block | prev | refLane | refBlock | cross |
|-----:|-----:|------:|-----:|--------:|---------:|:-----:|
| 0 | 0 | 2 | 1 | 0 | 0 | no |
| 0 | 0 | 32 | 31 | 0 | 21 | no |
| 0 | 0 | 4096 | 4095 | 0 | 2498 | no |
| 0 | 0 | 8191 | 8190 | 0 | 506 | no |

### ForgeMix sample prefixes (32 bytes)

- pass 0 lane 0 block 2: `73f6812ca426eb4fd3952020d3e2d37ed56b63cdf98d7221cbefa07712b3d036`
- pass 0 lane 0 block 2048: `8ed67f0cdd05674ce103861f700c2fbb6bd38cf9bdcd86b6c4d62f006c119b46`
- pass 0 lane 0 block 8191: `b1c9d3f6225ac400b322ba0f98369c9ffd6dfc464b9ca2e7b18d6be0d03ee081`

## Vector 3: `vector3_utf8_two_lanes`

| Field | Value |
|-------|-------|
| password_hex | `70c3a1737377c3b872642df09f9490` |
| salt_hex | `5a912c7e11d48803f64b19aec0573d8f` |
| memory_kib | `16384` |
| iterations | `2` |
| parallelism | `2` |
| seed_hex | `640b1c084530c5c75cd1b023906021b3c7043255100346e7dd68b1d38bbd7075` |
| group_root_hex | `3428cb7dee307d3820e762aab5dd89cb07394e7b26ddcc1ec7927709779cd1a3` |
| hash_hex | `fc2f2e6bbcda6f7ca4a927d8a827b7224b30fcce829c8418005d7dacf6f061ba` |
| encoded | `$forgeh$v=1$m=16384,t=2,p=2$WpEsfhHUiAP2SxmuwFc9jw$/C8ua7zab3ykqSfYqCe3Iksw/M6CnIQYAF19rPbwYbo` |

### Initialized block prefixes (32 bytes)

- lane 0 block 0: `0bcb726da0149873adf26791f768809f1ace968f32edbaef055c73b379a13853`
- lane 0 block 1: `259709591fee8e08928a83f1b4ceecc23640ee8180bc7cff3673727e96e3ad65`
- lane 1 block 0: `a38e8d52d8210c60281f7b4128d12272d79059a82693539e29d5ddcfe7ee7bad`
- lane 1 block 1: `7759df6832792693f4cdcc4fe330c4fafc95db5baee85bb127f7d58936d4a0f7`

### Sample references

| pass | lane | block | prev | refLane | refBlock | cross |
|-----:|-----:|------:|-----:|--------:|---------:|:-----:|
| 0 | 0 | 2 | 1 | 0 | 0 | no |
| 0 | 0 | 32 | 31 | 0 | 3 | no |
| 0 | 0 | 4096 | 4095 | 0 | 1945 | no |
| 0 | 0 | 8191 | 8190 | 0 | 3812 | no |
| 0 | 1 | 2 | 1 | 1 | 1 | no |
| 0 | 1 | 32 | 31 | 1 | 8 | no |
| 0 | 1 | 4096 | 4095 | 0 | 770 | yes |
| 0 | 1 | 8191 | 8190 | 1 | 7580 | no |
| 1 | 0 | 0 | 8191 | 0 | 6528 | no |
| 1 | 0 | 32 | 31 | 0 | 4049 | no |
| 1 | 0 | 4096 | 4095 | 1 | 3745 | yes |
| 1 | 0 | 8191 | 8190 | 0 | 727 | no |
| 1 | 1 | 0 | 8191 | 1 | 8013 | no |
| 1 | 1 | 32 | 31 | 1 | 4207 | no |
| 1 | 1 | 4096 | 4095 | 0 | 2619 | yes |
| 1 | 1 | 8191 | 8190 | 1 | 519 | no |

### ForgeMix sample prefixes (32 bytes)

- pass 0 lane 0 block 2: `bed0af054768e25ece94c1ac987b35bac7244c6290c8ce416cc175300001b8e0`
- pass 0 lane 0 block 2048: `1522e7c86a6460a9b3bcd2a4a6f367d50762f743169c3b449aeaf5bdeeb06673`
- pass 0 lane 1 block 2: `e029e624817460e07bb4db7d842fffaf11bd67d30177d7ad3aae65b4e1b57c4a`
- pass 0 lane 1 block 2048: `7e96498fe377598f29055fda07e77977d0bfe52bb819c7672de9014d29b72e76`
- pass 1 lane 0 block 8191: `1da78361440a944c875f126c18303e659ca03e3e08ac462d68dfbeb91fc943e7`
- pass 1 lane 1 block 8191: `5e81f6ff471f160c9ebb7ee19b3e434ac4db6fc4bd24e2fc6cae798296d97f1a`

## Vector 4: `vector4_null_bytes_four_lanes`

| Field | Value |
|-------|-------|
| password_hex | `000100ff00` |
| salt_hex | `42424242424242424242424242424242` |
| memory_kib | `32768` |
| iterations | `3` |
| parallelism | `4` |
| seed_hex | `f24b61e55a61587b704c5e5d98f3e0f9b17e0891708a8bee48c348643d71c40d` |
| group_root_hex | `f5c6c5eed903a7086337fa3b6f19272c99b7feb8805b5b892678193e58287d6d` |
| hash_hex | `158230bd7d23be110989b6e9c26a408a0109c2f8ab95d5294e7558a7c9b40b3d` |
| encoded | `$forgeh$v=1$m=32768,t=3,p=4$QkJCQkJCQkJCQkJCQkJCQg$FYIwvX0jvhEJibbpwmpAigEJwvirldUpTnVYp8m0Cz0` |

### Initialized block prefixes (32 bytes)

- lane 0 block 0: `16ea3cd93db64195760ac6d3912e86521473d4b7c236193be4ce198340febbb9`
- lane 0 block 1: `b4a351be22474870908c1f0a08ea42c9af0b18e0c7bc53299e6ebac4f600ba09`
- lane 1 block 0: `e8bbc5419b91d00072d9fa800ec5c6a4e923bec8dfb9bb06766a7bb57aa62ca6`
- lane 1 block 1: `3012aa37c3f80535789264448e551c91eb1c8c2795d6d03d1acd4ed9e3355fee`
- lane 2 block 0: `c154d688a9873d353385be1dc6e493ac7b593725477f481af8849ee9a120bb46`
- lane 2 block 1: `1be852975e4dfd5c9e620952014ada19507612771a14d8f976d851f4f974bbc2`
- lane 3 block 0: `09bc3c913066b76bac6d22587d621c2f155e4e792ec37a9fac69c4c145403300`
- lane 3 block 1: `cc9bae3a30a7a42c17ae1cf6f98b381ee86c4aec7ce6951cccc2c8a6b9dec900`

### Sample references

| pass | lane | block | prev | refLane | refBlock | cross |
|-----:|-----:|------:|-----:|--------:|---------:|:-----:|
| 0 | 0 | 2 | 1 | 0 | 0 | no |
| 0 | 0 | 32 | 31 | 0 | 9 | no |
| 0 | 0 | 4096 | 4095 | 1 | 1143 | yes |
| 0 | 0 | 8191 | 8190 | 0 | 6312 | no |
| 0 | 1 | 2 | 1 | 1 | 0 | no |
| 0 | 1 | 32 | 31 | 1 | 6 | no |
| 0 | 1 | 4096 | 4095 | 2 | 3276 | yes |
| 0 | 1 | 8191 | 8190 | 1 | 3071 | no |
| 0 | 2 | 2 | 1 | 2 | 0 | no |
| 0 | 2 | 32 | 31 | 2 | 20 | no |
| 0 | 2 | 4096 | 4095 | 2 | 1362 | no |
| 0 | 2 | 8191 | 8190 | 2 | 5475 | no |
| 0 | 3 | 2 | 1 | 3 | 0 | no |
| 0 | 3 | 32 | 31 | 3 | 28 | no |
| 0 | 3 | 4096 | 4095 | 2 | 2800 | yes |
| 0 | 3 | 8191 | 8190 | 3 | 2558 | no |
| 1 | 0 | 0 | 8191 | 0 | 2569 | no |
| 1 | 0 | 32 | 31 | 0 | 6965 | no |
| 1 | 0 | 4096 | 4095 | 2 | 3927 | yes |
| 1 | 0 | 8191 | 8190 | 0 | 3149 | no |
| 1 | 1 | 0 | 8191 | 1 | 3596 | no |
| 1 | 1 | 32 | 31 | 1 | 7164 | no |
| 1 | 1 | 4096 | 4095 | 0 | 1088 | yes |
| 1 | 1 | 8191 | 8190 | 1 | 4884 | no |
| 1 | 2 | 0 | 8191 | 2 | 2101 | no |
| 1 | 2 | 32 | 31 | 2 | 4742 | no |
| 1 | 2 | 4096 | 4095 | 3 | 787 | yes |
| 1 | 2 | 8191 | 8190 | 2 | 6171 | no |
| 1 | 3 | 0 | 8191 | 3 | 1225 | no |
| 1 | 3 | 32 | 31 | 3 | 4357 | no |
| 1 | 3 | 4096 | 4095 | 0 | 2767 | yes |
| 1 | 3 | 8191 | 8190 | 3 | 1595 | no |
| 2 | 0 | 0 | 8191 | 0 | 515 | no |
| 2 | 0 | 32 | 31 | 0 | 1780 | no |
| 2 | 0 | 4096 | 4095 | 3 | 1760 | yes |
| 2 | 0 | 8191 | 8190 | 0 | 5785 | no |
| 2 | 1 | 0 | 8191 | 1 | 263 | no |
| 2 | 1 | 32 | 31 | 1 | 3770 | no |
| 2 | 1 | 4096 | 4095 | 0 | 2555 | yes |
| 2 | 1 | 8191 | 8190 | 1 | 5692 | no |
| 2 | 2 | 0 | 8191 | 2 | 291 | no |
| 2 | 2 | 32 | 31 | 2 | 3876 | no |
| 2 | 2 | 4096 | 4095 | 3 | 243 | yes |
| 2 | 2 | 8191 | 8190 | 2 | 5183 | no |
| 2 | 3 | 0 | 8191 | 3 | 6472 | no |
| 2 | 3 | 32 | 31 | 3 | 1630 | no |
| 2 | 3 | 4096 | 4095 | 0 | 1367 | yes |
| 2 | 3 | 8191 | 8190 | 3 | 2949 | no |

### ForgeMix sample prefixes (32 bytes)

- pass 0 lane 0 block 2: `705febda46676e9f37de96788d5eddf435edc7e671abf55afff8ac07e54450a7`
- pass 0 lane 0 block 2048: `05d850c6fc58e88b39ddbaafb2395fd0d44eff9fac048c3f4f4eb1ade95ed6bd`
- pass 0 lane 1 block 2: `f2490cadcb90bb96db4d0e1cc19c29e874d754041a0c2205f9e3882134c0093d`
- pass 0 lane 1 block 2048: `fe731128ee3df205264dc92266f53cf2ce7b7d9a8282d526377545331106e352`
- pass 2 lane 0 block 8191: `24753e8bb5bf11b2d3d830ffb7a83e85642bdd85bc4fd76a89af0388046c345f`
- pass 2 lane 1 block 8191: `cec02c53d72bf151bb12f18a54bc1cf75ea3d06140e478efc21ab7b59c24451d`

