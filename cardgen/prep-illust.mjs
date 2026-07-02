// AI 생성 원본 등 래스터 일러스트 전처리.
// 사용법: node prep-illust.mjs <파일|폴더>   /   node prep-illust.mjs --selftest
// ① artZone 종횡비로 중앙 cover 크롭 + artZone 픽셀 크기로 리사이즈
// ② RGB를 palette 8색으로 최근접 매핑(유클리드, 디더링 없음), 알파는 보존
// ③ illustrations/<입력파일명>.png 저장, 고유 RGB 색 수 출력
// svg는 전처리 대상이 아니다(이미 팔레트 변수 기반).
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFile, readdir, mkdir, stat } from "node:fs/promises";
import assert from "node:assert/strict";
import sharp from "sharp";

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const RASTER_EXTS = new Set([".png", ".jpg", ".jpeg", ".webp"]);

const config = JSON.parse(await readFile(path.join(ROOT, "cards.json"), "utf8"));
const ART_W = config.artZone.width;
const ART_H = config.artZone.height;
const PALETTE = Object.values(config.palette).map((hex) => {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
});

function nearestPaletteColor(r, g, b) {
  let best = PALETTE[0];
  let bestDist = Infinity;
  for (const [pr, pg, pb] of PALETTE) {
    const d = (r - pr) ** 2 + (g - pg) ** 2 + (b - pb) ** 2;
    if (d < bestDist) {
      bestDist = d;
      best = [pr, pg, pb];
    }
  }
  return best;
}

// 입력 버퍼 → { png(sharp 인스턴스), data, info, uniqueColors }
async function prepBuffer(input) {
  const { data, info } = await sharp(input)
    .resize(ART_W, ART_H, { fit: "cover", position: "centre" })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  const unique = new Set();
  for (let i = 0; i < data.length; i += 4) {
    const [r, g, b] = nearestPaletteColor(data[i], data[i + 1], data[i + 2]);
    data[i] = r;
    data[i + 1] = g;
    data[i + 2] = b; // data[i+3](알파)는 그대로 보존
    unique.add((r << 16) | (g << 8) | b);
  }

  const png = sharp(data, {
    raw: { width: info.width, height: info.height, channels: 4 },
  }).png();
  return { png, data, info, uniqueColors: unique.size };
}

async function prepFile(inputPath) {
  const base = path.basename(inputPath, path.extname(inputPath));
  const outPath = path.join(ROOT, "illustrations", `${base}.png`);
  const { png, uniqueColors } = await prepBuffer(await readFile(inputPath));
  await mkdir(path.dirname(outPath), { recursive: true });
  await png.toFile(outPath);
  console.log(`${base}.png: ${ART_W}x${ART_H}, 고유 RGB ${uniqueColors}색`);
}

// 내부 생성한 다색 그라디언트로 전 과정을 검증한다
async function selftest() {
  const W = 512;
  const H = 384;
  const raw = Buffer.alloc(W * H * 4);
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      const i = (y * W + x) * 4;
      raw[i] = Math.round((x / (W - 1)) * 255);
      raw[i + 1] = Math.round((y / (H - 1)) * 255);
      raw[i + 2] = Math.round(((x + y) / (W + H - 2)) * 255);
      raw[i + 3] = y < H / 8 ? 0 : 255; // 상단 투명 띠: 알파 보존 검증용
    }
  }
  const inputPng = await sharp(raw, { raw: { width: W, height: H, channels: 4 } })
    .png()
    .toBuffer();

  const { data, info, uniqueColors } = await prepBuffer(inputPng);

  assert.equal(info.width, ART_W, "출력 폭이 artZone과 다르다");
  assert.equal(info.height, ART_H, "출력 높이가 artZone과 다르다");
  assert.ok(uniqueColors <= 8, `고유 RGB ${uniqueColors}색 > 8`);

  const paletteSet = new Set(PALETTE.map(([r, g, b]) => (r << 16) | (g << 8) | b));
  let hasTransparent = false;
  let hasOpaque = false;
  for (let i = 0; i < data.length; i += 4) {
    const key = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
    assert.ok(paletteSet.has(key), "팔레트에 없는 색이 남았다");
    if (data[i + 3] === 0) hasTransparent = true;
    if (data[i + 3] === 255) hasOpaque = true;
  }
  assert.ok(hasTransparent && hasOpaque, "알파 채널이 보존되지 않았다");

  console.log(`selftest PASS: ${ART_W}x${ART_H}, 고유 RGB ${uniqueColors}색 (≤ 8), 알파 보존 확인`);
}

const arg = process.argv[2];
if (!arg) {
  console.error("사용법: node prep-illust.mjs <파일|폴더>  /  node prep-illust.mjs --selftest");
  process.exit(1);
}

if (arg === "--selftest") {
  await selftest();
} else {
  const target = path.resolve(process.cwd(), arg);
  const info = await stat(target);
  if (info.isDirectory()) {
    const files = (await readdir(target)).filter((f) =>
      RASTER_EXTS.has(path.extname(f).toLowerCase())
    );
    if (files.length === 0) {
      console.error(`전처리할 이미지(png/jpg/webp)가 없다: ${target}`);
      process.exit(1);
    }
    for (const f of files) await prepFile(path.join(target, f));
  } else {
    if (!RASTER_EXTS.has(path.extname(target).toLowerCase())) {
      console.error(`지원하지 않는 확장자: ${target} (png/jpg/webp만)`);
      process.exit(1);
    }
    await prepFile(target);
  }
}
