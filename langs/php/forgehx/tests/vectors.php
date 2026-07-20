<?php
declare(strict_types=1);

require dirname(__DIR__) . '/src/ForgeHashX.php';

$vectorDir = dirname(__DIR__, 4) . '/implementers/x0/vectors';
$files = glob($vectorDir . '/*.json');
if (!$files) {
    fwrite(STDERR, "no vectors in $vectorDir\n");
    exit(1);
}

foreach ($files as $file) {
    $data = json_decode(file_get_contents($file), true, 512, JSON_THROW_ON_ERROR);
    $password = hex2bin($data['passwordHex']);
    $salt = hex2bin($data['saltHex']);
    $seed = ForgeHashX::deriveSeed(
        $password,
        $salt,
        $data['memoryKiB'],
        $data['iterations'],
        $data['parallelism'],
        $data['outputLength']
    );
    $hash = ForgeHashX::deriveHash(
        $password,
        $salt,
        $data['memoryKiB'],
        $data['iterations'],
        $data['parallelism'],
        $data['outputLength']
    );
    if (bin2hex($seed) !== $data['seedHex'] || bin2hex($hash) !== $data['hashHex']) {
        fwrite(STDERR, 'mismatch ' . basename($file) . "\n");
        exit(2);
    }
    echo 'ok ' . basename($file) . PHP_EOL;
}
