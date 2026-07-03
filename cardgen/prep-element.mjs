// Usage: node prep-element.mjs <file|folder>  /  node prep-element.mjs --selftest
// Element PNG preprocessing:
// 1) cut out uniform white fallback backgrounds with alpha extraction and white unmatting
// 2) trim transparent margins
// 3) recalculate manifest pivot from the original frame into the trimmed frame
// 4) write PNG and <id>.meta.json sidecar into manifest.outputDir
import path from "node:path";
import { fileURLToPath } from "node:url";
import { mkdir, readFile, readdir, rename, stat, writeFile } from "node:fs/promises";
import assert from "node:assert/strict";
import sharp from "sharp";

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = path.dirname(ROOT);
const MANIFEST_PATH = path.join(ROOT, "elements", "manifest.json");
const RASTER_EXTS = new Set([".png", ".jpg", ".jpeg", ".webp"]);
const ALPHA_TRIM_THRESHOLD = 8;
const WHITE_CUTOUT_LOW = 34;
const WHITE_CUTOUT_HIGH = 118;

function clamp01(v) {
  return Math.min(1, Math.max(0, v));
}

function smoothstep(edge0, edge1, x) {
  const t = clamp01((x - edge0) / (edge1 - edge0));
  return t * t * (3 - 2 * t);
}

function parseSize(size) {
  const [w, h] = String(size || "").split("x").map((v) => Number(v));
  return Number.isFinite(w) && Number.isFinite(h) ? { width: w, height: h } : null;
}

async function loadManifest() {
  const manifest = JSON.parse(await readFile(MANIFEST_PATH, "utf8"));
  const byId = new Map();
  for (const item of manifest.elements || []) byId.set(item.id, item);
  const outputDir = path.resolve(PROJECT_ROOT, manifest.outputDir || "Assets/Art/Elements");
  return { manifest, byId, outputDir };
}

function hasUsefulAlpha(data) {
  for (let i = 3; i < data.length; i += 4) {
    if (data[i] < 250) return true;
  }
  return false;
}

function applyWhiteCutout(data) {
  let changed = false;
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const distanceFromWhite = Math.max(255 - r, 255 - g, 255 - b);
    const alpha = Math.round(smoothstep(WHITE_CUTOUT_LOW, WHITE_CUTOUT_HIGH, distanceFromWhite) * 255);
    data[i + 3] = alpha;
    changed = true;

    if (alpha <= 0) {
      data[i] = 0;
      data[i + 1] = 0;
      data[i + 2] = 0;
      continue;
    }

    if (alpha < 255) {
      const a = alpha / 255;
      data[i] = Math.max(0, Math.min(255, Math.round((r - 255 * (1 - a)) / a)));
      data[i + 1] = Math.max(0, Math.min(255, Math.round((g - 255 * (1 - a)) / a)));
      data[i + 2] = Math.max(0, Math.min(255, Math.round((b - 255 * (1 - a)) / a)));
    }
  }
  return changed;
}

function findAlphaBounds(data, width, height) {
  let left = width;
  let right = -1;
  let top = height;
  let bottom = -1;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const alpha = data[(y * width + x) * 4 + 3];
      if (alpha <= ALPHA_TRIM_THRESHOLD) continue;
      if (x < left) left = x;
      if (x > right) right = x;
      if (y < top) top = y;
      if (y > bottom) bottom = y;
    }
  }

  if (right < left || bottom < top) {
    return { left: 0, top: 0, width, height };
  }

  return {
    left,
    top,
    width: right - left + 1,
    height: bottom - top + 1,
  };
}

async function prepBuffer(input, item) {
  const targetSize = parseSize(item?.size);
  let image = sharp(input).rotate().ensureAlpha();
  if (targetSize) {
    image = image.resize(targetSize.width, targetSize.height, {
      fit: "contain",
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    });
  }

  const { data, info } = await image.raw().toBuffer({ resolveWithObject: true });
  const cutoutApplied = !hasUsefulAlpha(data) && applyWhiteCutout(data);
  const trim = findAlphaBounds(data, info.width, info.height);

  const rawImage = sharp(data, {
    raw: { width: info.width, height: info.height, channels: 4 },
  });
  const pngBuffer = await rawImage.extract(trim).png().toBuffer();

  const sourcePivot = Array.isArray(item?.pivot) && item.pivot.length >= 2
    ? item.pivot
    : [0.5, 0.5];
  const pivotSourceX = sourcePivot[0] * info.width;
  const pivotSourceY = sourcePivot[1] * info.height;
  const pivotTrimmed = [
    clamp01((pivotSourceX - trim.left) / trim.width),
    clamp01((pivotSourceY - trim.top) / trim.height),
  ];

  return {
    pngBuffer,
    meta: {
      id: item?.id || null,
      originalSize: { width: info.width, height: info.height },
      size: { width: trim.width, height: trim.height },
      trim,
      pivot: pivotTrimmed,
      unityPivot: [pivotTrimmed[0], 1 - pivotTrimmed[1]],
      cutoutApplied,
    },
  };
}

async function writeOutput(outputDir, id, pngBuffer, meta) {
  await mkdir(outputDir, { recursive: true });
  const outPath = path.join(outputDir, `${id}.png`);
  const tempPath = path.join(outputDir, `${id}.tmp.png`);
  await writeFile(tempPath, pngBuffer);
  await rename(tempPath, outPath);
  await writeFile(path.join(outputDir, `${id}.meta.json`), JSON.stringify(meta, null, 2) + "\n", "utf8");
  return outPath;
}

async function prepFile(inputPath, manifestInfo) {
  const id = path.basename(inputPath, path.extname(inputPath));
  const item = manifestInfo.byId.get(id);
  if (!item) throw new Error(`Element id not found in manifest: ${id}`);
  const { pngBuffer, meta } = await prepBuffer(await readFile(inputPath), item);
  meta.id = id;
  meta.source = path.relative(PROJECT_ROOT, inputPath).replaceAll("\\", "/");
  const outPath = await writeOutput(manifestInfo.outputDir, id, pngBuffer, meta);
  console.log(`${id}.png: ${meta.size.width}x${meta.size.height}, pivot ${meta.pivot.map((v) => v.toFixed(4)).join(", ")}, cutout ${meta.cutoutApplied ? "yes" : "no"} -> ${path.relative(PROJECT_ROOT, outPath)}`);
}

async function selftest() {
  const W = 240;
  const H = 180;
  const raw = Buffer.alloc(W * H * 4);
  for (let i = 0; i < raw.length; i += 4) {
    raw[i] = 255;
    raw[i + 1] = 255;
    raw[i + 2] = 255;
    raw[i + 3] = 255;
  }

  for (let y = 50; y < 130; y++) {
    for (let x = 70; x < 190; x++) {
      const i = (y * W + x) * 4;
      raw[i] = 90;
      raw[i + 1] = 45;
      raw[i + 2] = 35;
      raw[i + 3] = 255;
    }
  }

  const input = await sharp(raw, { raw: { width: W, height: H, channels: 4 } }).png().toBuffer();
  const item = { id: "selftest", size: "240x180", pivot: [0.5, 0.5] };
  const { pngBuffer, meta } = await prepBuffer(input, item);
  const { data, info } = await sharp(pngBuffer).ensureAlpha().raw().toBuffer({ resolveWithObject: true });

  assert.equal(info.width, 120);
  assert.equal(info.height, 80);
  assert.ok(meta.cutoutApplied, "white fallback cutout should be applied");
  assert.ok(Math.abs(meta.pivot[0] - 0.4166667) < 0.01, `pivot x ${meta.pivot[0]}`);
  assert.ok(Math.abs(meta.pivot[1] - 0.5) < 0.01, `pivot y ${meta.pivot[1]}`);

  let transparent = 0;
  let opaque = 0;
  for (let i = 3; i < data.length; i += 4) {
    if (data[i] === 0) transparent++;
    if (data[i] === 255) opaque++;
  }
  assert.ok(opaque > 0, "trimmed result should contain opaque subject pixels");
  assert.equal(transparent, 0, "trimmed rectangle should not retain white transparent padding in this fixture");
  console.log("selftest PASS: cutout, trim, and pivot recalculation verified");
}

const argv = process.argv.slice(2);
if (argv.length === 0) {
  console.error("Usage: node prep-element.mjs <file|folder>  /  node prep-element.mjs --selftest");
  process.exit(1);
}

if (argv[0] === "--selftest") {
  await selftest();
} else {
  const manifestInfo = await loadManifest();
  const target = path.resolve(process.cwd(), argv[0]);
  const targetStat = await stat(target);
  if (targetStat.isDirectory()) {
    const files = (await readdir(target))
      .filter((f) => RASTER_EXTS.has(path.extname(f).toLowerCase()))
      .sort((a, b) => a.localeCompare(b));
    if (files.length === 0) throw new Error(`No raster files found: ${target}`);
    for (const file of files) await prepFile(path.join(target, file), manifestInfo);
  } else {
    if (!RASTER_EXTS.has(path.extname(target).toLowerCase())) {
      throw new Error(`Unsupported extension: ${target}`);
    }
    await prepFile(target, manifestInfo);
  }
}
