// AI 생성 원본 등 래스터 일러스트 전처리.
// 사용법: node prep-illust.mjs <파일|폴더> [--no-normalize]   /   node prep-illust.mjs --selftest
// ① artZone 종횡비로 중앙 cover 크롭 + artZone 픽셀 크기로 리사이즈
// ② 종이톤 정규화: 네 모서리 24x24 픽셀의 채널별 중앙값을 원본 종이색으로 추정,
//    palette.paper로 맞추는 채널별 게인(0.7~1.4 클램프)을 전체에 곱한다. 알파는 보존.
// ③ illustrations/<입력파일명>.png 저장
// svg는 전처리 대상이 아니다(이미 팔레트 변수 기반).
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFile, readdir, mkdir, stat } from "node:fs/promises";
import assert from "node:assert/strict";
import sharp from "sharp";

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const RASTER_EXTS = new Set([".png", ".jpg", ".jpeg", ".webp"]);
const CORNER = 24; // 종이색 추정에 쓰는 모서리 샘플 한 변(px)
const GAIN_MIN = 0.7; // 과보정 방지 클램프
const GAIN_MAX = 1.4;

const config = JSON.parse(await readFile(path.join(ROOT, "cards.json"), "utf8"));
const ART_W = config.artZone.width;
const ART_H = config.artZone.height;
const TARGET_PAPER = hexToRgb(config.palette.paper);

function hexToRgb(hex) {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function rgbToHex([r, g, b]) {
  return "#" + [r, g, b].map((v) => Math.round(v).toString(16).padStart(2, "0")).join("").toUpperCase();
}

function median(values) {
  values.sort((a, b) => a - b);
  const mid = values.length >> 1;
  return values.length % 2 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
}

// 네 모서리 24x24 영역 픽셀의 채널별 중앙값 = 종이색 추정.
// 모서리는 여백일 확률이 높고, 중앙값이라 피사체가 한 모서리를 침범해도 견딘다.
// 투명 픽셀은 리사이즈의 premultiply 때문에 RGB가 0으로 뭉개지므로 표본에서 제외한다.
function estimatePaper(data, width, height) {
  const size = Math.min(CORNER, width, height);
  const corners = [
    [0, 0],
    [width - size, 0],
    [0, height - size],
    [width - size, height - size],
  ];
  const channels = [[], [], []];
  for (const [cx, cy] of corners) {
    for (let y = cy; y < cy + size; y++) {
      for (let x = cx; x < cx + size; x++) {
        const i = (y * width + x) * 4;
        if (data[i + 3] < 128) continue; // 반투명 이하 모서리 픽셀은 종이가 아니다
        channels[0].push(data[i]);
        channels[1].push(data[i + 1]);
        channels[2].push(data[i + 2]);
      }
    }
  }
  if (channels[0].length === 0) return null; // 모서리가 전부 투명 → 추정 불가
  return channels.map(median);
}

// 종이톤 정규화: 채널별 게인 곱연산. data를 제자리 수정, 알파는 건드리지 않는다.
// 종이색 추정이 불가능하면(모서리 전부 투명) 원본을 그대로 둔다.
function normalizePaperTone(data, width, height) {
  const paperEstimate = estimatePaper(data, width, height);
  if (!paperEstimate) return { paperEstimate: null, gains: null };
  const gains = paperEstimate.map((est, c) =>
    Math.min(GAIN_MAX, Math.max(GAIN_MIN, TARGET_PAPER[c] / est))
  );
  for (let i = 0; i < data.length; i += 4) {
    data[i] = Math.min(255, Math.round(data[i] * gains[0]));
    data[i + 1] = Math.min(255, Math.round(data[i + 1] * gains[1]));
    data[i + 2] = Math.min(255, Math.round(data[i + 2] * gains[2])); // data[i+3](알파)는 그대로 보존
  }
  return { paperEstimate, gains };
}

// 입력 버퍼 → { png(sharp 인스턴스), data, info, paperEstimate, gains }
async function prepBuffer(input, { normalize = true } = {}) {
  const { data, info } = await sharp(input)
    .rotate() // EXIF Orientation 자동 적용 (스마트폰 촬영 jpg 대비)
    .resize(ART_W, ART_H, { fit: "cover", position: "centre" })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  let paperEstimate = null;
  let gains = null;
  if (normalize) {
    ({ paperEstimate, gains } = normalizePaperTone(data, info.width, info.height));
  }

  const png = sharp(data, {
    raw: { width: info.width, height: info.height, channels: 4 },
  }).png();
  return { png, data, info, paperEstimate, gains };
}

function toneLog(paperEstimate, gains) {
  if (!paperEstimate) return "정규화 생략 (--no-normalize 또는 모서리 전부 투명)";
  const g = gains.map((v) => v.toFixed(3)).join(", ");
  return `추정 종이색 ${rgbToHex(paperEstimate)}, 게인 [${g}]`;
}

async function prepFile(inputPath, options) {
  const base = path.basename(inputPath, path.extname(inputPath));
  const outPath = path.join(ROOT, "illustrations", `${base}.png`);
  const { png, paperEstimate, gains } = await prepBuffer(await readFile(inputPath), options);
  await mkdir(path.dirname(outPath), { recursive: true });
  await png.toFile(outPath);
  console.log(`${base}.png: ${ART_W}x${ART_H}, ${toneLog(paperEstimate, gains)}`);
}

// 내부 생성한 "톤이 틀어진 종이" 테스트 이미지로 전 과정을 검증한다
async function selftest() {
  const W = 600;
  const H = 900; // 2:3 — artZone과 같은 종횡비
  const BG = [226, 207, 166]; // #E2CFA6: 목표 paper에서 톤이 틀어진 종이색
  const raw = Buffer.alloc(W * H * 4);
  for (let i = 0; i < raw.length; i += 4) {
    raw[i] = BG[0];
    raw[i + 1] = BG[1];
    raw[i + 2] = BG[2];
    raw[i + 3] = 255;
  }
  // 중앙에 임의 도형 몇 개 (모서리 24x24 샘플 영역은 침범하지 않는다)
  const fillRect = (x0, y0, w, h, [r, g, b, a]) => {
    for (let y = y0; y < y0 + h; y++) {
      for (let x = x0; x < x0 + w; x++) {
        const i = (y * W + x) * 4;
        raw[i] = r;
        raw[i + 1] = g;
        raw[i + 2] = b;
        raw[i + 3] = a;
      }
    }
  };
  fillRect(180, 260, 240, 120, [140, 60, 40, 255]); // 적갈 사각
  fillRect(240, 460, 120, 220, [50, 80, 120, 255]); // 청 사각
  fillRect(270, 400, 60, 40, [0, 0, 0, 0]); // 투명 구멍: 알파 보존 검증용

  const inputPng = await sharp(raw, { raw: { width: W, height: H, channels: 4 } })
    .png()
    .toBuffer();

  const { data, info, paperEstimate, gains } = await prepBuffer(inputPng);
  console.log(`selftest 파이프라인: ${toneLog(paperEstimate, gains)}`);

  assert.equal(info.width, ART_W, "출력 폭이 artZone과 다르다");
  assert.equal(info.height, ART_H, "출력 높이가 artZone과 다르다");

  // 정규화 후 네 모서리 중앙값이 목표 paper와 채널당 ±6 이내
  const resultPaper = estimatePaper(data, info.width, info.height);
  for (let c = 0; c < 3; c++) {
    const diff = Math.abs(resultPaper[c] - TARGET_PAPER[c]);
    assert.ok(
      diff <= 6,
      `채널 ${"RGB"[c]} 종이색 오차 ${diff} > 6 (결과 ${rgbToHex(resultPaper)}, 목표 ${rgbToHex(TARGET_PAPER)})`
    );
  }

  let hasTransparent = false;
  let hasOpaque = false;
  for (let i = 3; i < data.length; i += 4) {
    if (data[i] === 0) hasTransparent = true;
    if (data[i] === 255) hasOpaque = true;
  }
  assert.ok(hasTransparent && hasOpaque, "알파 채널이 보존되지 않았다");

  console.log(
    `selftest PASS: ${ART_W}x${ART_H}, 결과 종이색 ${rgbToHex(resultPaper)} (목표 ${rgbToHex(TARGET_PAPER)} ±6), 알파 보존 확인`
  );
}

const argv = process.argv.slice(2);
const normalize = !argv.includes("--no-normalize");
const args = argv.filter((a) => a !== "--no-normalize");
const arg = args[0];
if (!arg) {
  console.error(
    "사용법: node prep-illust.mjs <파일|폴더> [--no-normalize]  /  node prep-illust.mjs --selftest"
  );
  process.exit(1);
}

if (arg === "--selftest") {
  await selftest();
} else {
  const options = { normalize };
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
    // 출력이 항상 <basename>.png라 basename이 겹치면 한쪽이 소리 없이 덮어써진다 → 사전 차단
    const byBase = new Map();
    for (const f of files) {
      const base = path.basename(f, path.extname(f));
      if (!byBase.has(base)) byBase.set(base, []);
      byBase.get(base).push(f);
    }
    const clashes = [...byBase.values()].filter((v) => v.length > 1);
    if (clashes.length > 0) {
      console.error(
        `출력 파일명이 충돌한다 (같은 basename): ${clashes.map((v) => v.join(" ↔ ")).join(", ")}`
      );
      process.exit(1);
    }
    for (const f of files) await prepFile(path.join(target, f), options);
  } else {
    if (!RASTER_EXTS.has(path.extname(target).toLowerCase())) {
      console.error(`지원하지 않는 확장자: ${target} (png/jpg/webp만)`);
      process.exit(1);
    }
    await prepFile(target, options);
  }
}
