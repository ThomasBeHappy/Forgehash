<?php

declare(strict_types=1);

/**
 * Official vector smoke test for the PHP FFI binding.
 *
 * Usage:
 *   cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml
 *   php langs/php/forgeh/tests/vectors.php
 */

require_once dirname(__DIR__) . '/src/ForgeHash.php';

use ForgeH\ForgeHash;

function from_hex(string $hex): string
{
    if ($hex === '') {
        return '';
    }
    $bin = hex2bin($hex);
    if ($bin === false) {
        throw new RuntimeException('bad hex');
    }
    return $bin;
}

function check(string $path): void
{
    $json = json_decode(file_get_contents($path), true, 512, JSON_THROW_ON_ERROR);
    $password = from_hex($json['passwordHex']);
    $salt = from_hex($json['saltHex']);
    $params = [
        'memoryKib' => $json['memoryKiB'],
        'iterations' => $json['iterations'],
        'parallelism' => $json['parallelism'],
        'outputLength' => 32,
        'saltLength' => strlen($salt),
    ];

    $seed = ForgeHash::deriveSeed($password, $salt, $params);
    $hash = ForgeHash::deriveHash($password, $salt, $params);
    $encoded = ForgeHash::encode($params, $salt, $hash);

    if (bin2hex($seed) !== $json['seedHex']) {
        throw new RuntimeException($path . ': seed mismatch');
    }
    if (bin2hex($hash) !== $json['hashHex']) {
        throw new RuntimeException($path . ': hash mismatch');
    }
    if ($encoded !== $json['encoded']) {
        throw new RuntimeException($path . ': encoded mismatch');
    }
    echo "ok {$path}\n";
}

$dir = dirname(__DIR__, 3) . '/implementers/v1/vectors';
foreach ([
    'vector1_empty_password_zero_salt.json',
    'vector2_password_incrementing_salt.json',
    'vector3_utf8_two_lanes.json',
    'vector4_null_bytes_four_lanes.json',
] as $file) {
    check($dir . '/' . $file);
}

echo "all vectors passed\n";
