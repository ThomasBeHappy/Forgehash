<?php

declare(strict_types=1);

namespace ForgeH;

use FFI;
use InvalidArgumentException;
use RuntimeException;

/**
 * Experimental ForgeHash-B3 v1 (PHP FFI binding to the Rust core).
 *
 * NOT for production password storage.
 */
final class ForgeHash
{
    private static ?FFI $ffi = null;

    /** @return array{memoryKib:int,iterations:int,parallelism:int,outputLength:int,saltLength:int} */
    public static function development(): array
    {
        return [
            'memoryKib' => 8192,
            'iterations' => 1,
            'parallelism' => 1,
            'outputLength' => 32,
            'saltLength' => 16,
        ];
    }

    /** @return array{memoryKib:int,iterations:int,parallelism:int,outputLength:int,saltLength:int} */
    public static function interactive(): array
    {
        return [
            'memoryKib' => 65536,
            'iterations' => 3,
            'parallelism' => 1,
            'outputLength' => 32,
            'saltLength' => 16,
        ];
    }

    private static function ffi(): FFI
    {
        if (self::$ffi !== null) {
            return self::$ffi;
        }

        $lib = self::libraryPath();
        $cdef = <<<'CDEF'
int forgeh_derive_hash(
    const uint8_t *password, size_t password_len,
    const uint8_t *salt, size_t salt_len,
    uint32_t memory_kib, uint32_t iterations,
    uint32_t parallelism, uint32_t output_length,
    uint8_t *out);
int forgeh_derive_seed(
    const uint8_t *password, size_t password_len,
    const uint8_t *salt, size_t salt_len,
    uint32_t memory_kib, uint32_t iterations,
    uint32_t parallelism, uint32_t output_length,
    uint8_t *out);
int forgeh_verify_password(const uint8_t *password, size_t password_len, const char *encoded);
int forgeh_encode(
    uint32_t memory_kib, uint32_t iterations, uint32_t parallelism,
    const uint8_t *salt, size_t salt_len,
    const uint8_t *hash, size_t hash_len,
    char *out, size_t out_len);
CDEF;

        self::$ffi = FFI::cdef($cdef, $lib);
        return self::$ffi;
    }

    private static function libraryPath(): string
    {
        $env = getenv('FORGEH_LIB');
        if (is_string($env) && $env !== '' && is_file($env)) {
            return $env;
        }

        $root = dirname(__DIR__, 3) . DIRECTORY_SEPARATOR . 'rust' . DIRECTORY_SEPARATOR . 'forgeh' . DIRECTORY_SEPARATOR . 'target' . DIRECTORY_SEPARATOR . 'release';
        $candidates = [
            $root . DIRECTORY_SEPARATOR . 'forgeh.dll',
            $root . DIRECTORY_SEPARATOR . 'libforgeh.so',
            $root . DIRECTORY_SEPARATOR . 'libforgeh.dylib',
        ];
        foreach ($candidates as $path) {
            if (is_file($path)) {
                return $path;
            }
        }

        throw new RuntimeException(
            'Rust forgeh library not found. Build with: cargo build --release --manifest-path langs/rust/forgeh/Cargo.toml (or set FORGEH_LIB)'
        );
    }

    /**
     * @param array{memoryKib:int,iterations:int,parallelism:int,outputLength:int,saltLength?:int} $params
     */
    public static function deriveHash(string $password, string $salt, array $params): string
    {
        $ffi = self::ffi();
        $outLen = (int) $params['outputLength'];
        $out = $ffi->new('uint8_t[' . $outLen . ']');
        $rc = $ffi->forgeh_derive_hash(
            $password,
            strlen($password),
            $salt,
            strlen($salt),
            (int) $params['memoryKib'],
            (int) $params['iterations'],
            (int) $params['parallelism'],
            $outLen,
            $out
        );
        if ($rc !== 0) {
            throw new RuntimeException('forgeh_derive_hash failed: ' . $rc);
        }
        return FFI::string($out, $outLen);
    }

    /**
     * @param array{memoryKib:int,iterations:int,parallelism:int,outputLength:int,saltLength?:int} $params
     */
    public static function deriveSeed(string $password, string $salt, array $params): string
    {
        $ffi = self::ffi();
        $out = $ffi->new('uint8_t[32]');
        $rc = $ffi->forgeh_derive_seed(
            $password,
            strlen($password),
            $salt,
            strlen($salt),
            (int) $params['memoryKib'],
            (int) $params['iterations'],
            (int) $params['parallelism'],
            (int) $params['outputLength'],
            $out
        );
        if ($rc !== 0) {
            throw new RuntimeException('forgeh_derive_seed failed: ' . $rc);
        }
        return FFI::string($out, 32);
    }

    /**
     * @param array{memoryKib:int,iterations:int,parallelism:int,outputLength:int,saltLength:int} $params
     */
    public static function hashPassword(string $password, array $params): string
    {
        $saltLen = (int) $params['saltLength'];
        $salt = random_bytes($saltLen);
        $hash = self::deriveHash($password, $salt, $params);
        return self::encode($params, $salt, $hash);
    }

    public static function verifyPassword(string $password, string $encoded): bool
    {
        $ffi = self::ffi();
        return $ffi->forgeh_verify_password($password, strlen($password), $encoded) === 1;
    }

    /**
     * @param array{memoryKib:int,iterations:int,parallelism:int,outputLength?:int,saltLength?:int} $params
     */
    public static function encode(array $params, string $salt, string $hash): string
    {
        $ffi = self::ffi();
        $out = $ffi->new('char[512]');
        $n = $ffi->forgeh_encode(
            (int) $params['memoryKib'],
            (int) $params['iterations'],
            (int) $params['parallelism'],
            $salt,
            strlen($salt),
            $hash,
            strlen($hash),
            $out,
            512
        );
        if ($n < 0) {
            throw new RuntimeException('forgeh_encode failed');
        }
        return FFI::string($out, $n);
    }
}
