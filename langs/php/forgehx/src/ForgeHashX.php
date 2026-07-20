<?php
declare(strict_types=1);

/**
 * Experimental ForgeHash-X v0 via Rust FFI.
 * Not for production. Not B3-compatible.
 */
final class ForgeHashX
{
    private static ?FFI $ffi = null;

    private static function ffi(): FFI
    {
        if (self::$ffi !== null) {
            return self::$ffi;
        }

        $cdef = <<<'CDEF'
int forgehx_derive_seed(const uint8_t *password, size_t password_len,
                        const uint8_t *salt, size_t salt_len,
                        uint32_t memory_kib, uint32_t iterations,
                        uint32_t parallelism, uint32_t output_length,
                        uint8_t *out);
int forgehx_derive_hash(const uint8_t *password, size_t password_len,
                        const uint8_t *salt, size_t salt_len,
                        uint32_t memory_kib, uint32_t iterations,
                        uint32_t parallelism, uint32_t output_length,
                        uint8_t *out);
int forgehx_verify_password(const uint8_t *password, size_t password_len,
                            const char *encoded);
CDEF;

        $root = dirname(__DIR__, 4);
        $candidates = [
            $root . '/langs/rust/forgehx/target/release/forgehx.dll',
            $root . '/langs/rust/forgehx/target/release/libforgehx.so',
            $root . '/langs/rust/forgehx/target/release/libforgehx.dylib',
        ];
        foreach ($candidates as $lib) {
            if (is_file($lib)) {
                self::$ffi = FFI::cdef($cdef, $lib);
                return self::$ffi;
            }
        }
        throw new RuntimeException('forgehx shared library not found; build langs/rust/forgehx --release');
    }

    public static function deriveHash(
        string $password,
        string $salt,
        int $memoryKiB = 1024,
        int $iterations = 1,
        int $parallelism = 1,
        int $outputLength = 32
    ): string {
        $ffi = self::ffi();
        $out = $ffi->new('uint8_t[' . $outputLength . ']');
        $rc = $ffi->forgehx_derive_hash(
            $password,
            strlen($password),
            $salt,
            strlen($salt),
            $memoryKiB,
            $iterations,
            $parallelism,
            $outputLength,
            $out
        );
        if ($rc !== 0) {
            throw new RuntimeException('forgehx_derive_hash failed');
        }
        return FFI::string($out, $outputLength);
    }

    public static function deriveSeed(
        string $password,
        string $salt,
        int $memoryKiB = 1024,
        int $iterations = 1,
        int $parallelism = 1,
        int $outputLength = 32
    ): string {
        $ffi = self::ffi();
        $out = $ffi->new('uint8_t[32]');
        $rc = $ffi->forgehx_derive_seed(
            $password,
            strlen($password),
            $salt,
            strlen($salt),
            $memoryKiB,
            $iterations,
            $parallelism,
            $outputLength,
            $out
        );
        if ($rc !== 0) {
            throw new RuntimeException('forgehx_derive_seed failed');
        }
        return FFI::string($out, 32);
    }

    public static function verifyPassword(string $password, string $encoded): bool
    {
        $ffi = self::ffi();
        return $ffi->forgehx_verify_password($password, strlen($password), $encoded) !== 0;
    }
}
