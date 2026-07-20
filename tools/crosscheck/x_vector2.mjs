/** Print ForgeHash-X vector2 digest (hex). Used by cross-implementation tests. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..");
const modUrl = pathToFileURL(join(root, "langs", "nodejs", "forgehx", "src", "index.js")).href;
const { deriveHash, Params } = await import(modUrl);

const password = Buffer.from("70617373776f7264", "hex");
const salt = Buffer.from("000102030405060708090a0b0c0d0e0f", "hex");
const hash = deriveHash(
  password,
  salt,
  new Params({ memoryKiB: 1024, iterations: 1, parallelism: 1 }),
);
console.log(Buffer.from(hash).toString("hex"));
