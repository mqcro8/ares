import { copyFile, mkdir, rm } from "node:fs/promises";
import { dirname, join } from "node:path";

const root = process.cwd();
const output = join(root, "public");

const assets = [
  "art/fonts/Aerospace.ttf",
  "art/fonts/PressStart2P-Regular.ttf",
  "art/menu_placeholder/1123556.png",
  "art/rover/mars_ground.png",
  "art/player/inventory/hotbar-single-segment.png",
  "art/player/inventory/selected-hotbar-single-segment.png",
  "art/rover/rover_moving.png",
];

await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });
await copyFile(join(root, "controlers/index.html"), join(output, "index.html"));

for (const asset of assets) {
  const destination = join(output, asset);
  await mkdir(dirname(destination), { recursive: true });
  await copyFile(join(root, asset), destination);
}
