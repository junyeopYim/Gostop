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

// --- 잉크순서 마스크 파라미터 (검증된 명세) ---------------------------------
// 요소가 화면에 "그려지는 순서"를 단채널 그레이로 굽는다. 값 0 = 가장 먼저 그려지는
// 획(먹의 시작점), 값 1 = 가장 나중(먼 담채 면/떨어진 조각). 셰이더의 _Threshold가
// 0→1로 오르며 mask <= threshold 인 픽셀을 순서대로 드러낸다.
const INK_ALPHA_MIN = 64;      // 불투명 판정: alpha > 64
const INK_LUMA_MAX = 115;      // 먹선 판정: 불투명 AND 휘도 < 115
const INK_STEP_COST = 1;       // 먹선 위를 흐르는 걸음 비용 (붓이 선을 따라 흐름)
const NONINK_STEP_COST = 3;    // 담채/빈틈을 건너는 걸음 비용 (면과 색은 선 뒤에)
const INK_PERCENTILE = 0.995;  // 정규화 기준: 불투명 픽셀 거리의 99.5 백분위수
const INK_INF = 0x3fffffff;
const INK_MASK_SUFFIX = "_inkmask";
const NEIGHBORS8 = [
  [-1, -1], [0, -1], [1, -1],
  [-1, 0], [1, 0],
  [-1, 1], [0, 1], [1, 1],
];

function luminance(r, g, b) {
  return 0.299 * r + 0.587 * g + 0.114 * b;
}

// 후처리된 요소 RGBA에서 그려짐 순서 마스크(단채널 0..255)를 계산한다.
// seedNorm: 정규화 좌표 [x, y] (트림 프레임 기준, y는 위→아래). 가장 가까운 먹선
// 픽셀을 시드로 삼아 ① 먹선 그래프 위 균일비용 BFS로 획 순서를 매기고
// ② 그 도달 픽셀들을 시작점으로 나머지 불투명 픽셀(담채·조각)까지 가중 확산한다.
function computeInkMask(rgba, width, height, seedNorm) {
  const n = width * height;
  const opaque = new Uint8Array(n);
  const ink = new Uint8Array(n);
  let inkCount = 0;
  let opaqueCount = 0;

  for (let i = 0, p = 0; i < n; i++, p += 4) {
    if (rgba[p + 3] > INK_ALPHA_MIN) {
      opaque[i] = 1;
      opaqueCount++;
      if (luminance(rgba[p], rgba[p + 1], rgba[p + 2]) < INK_LUMA_MAX) {
        ink[i] = 1;
        inkCount++;
      }
    }
  }

  const out = new Uint8Array(n); // 기본 0; 투명은 아래에서 255로 채운다
  if (opaqueCount === 0) {
    out.fill(255); // 그릴 것이 없으면 전부 "가장 나중" (투명 규칙)
    return out;
  }

  // --- 시드: seed 지점에서 가장 가까운 먹선 픽셀 (없으면 불투명 픽셀) ---
  const sx = Math.min(width - 1, Math.max(0, Math.round(seedNorm[0] * (width - 1))));
  const sy = Math.min(height - 1, Math.max(0, Math.round(seedNorm[1] * (height - 1))));
  const seedField = inkCount > 0 ? ink : opaque;
  let seedIdx = -1;
  let bestD2 = Infinity;
  for (let y = 0, i = 0; y < height; y++) {
    const dy = y - sy;
    for (let x = 0; x < width; x++, i++) {
      if (!seedField[i]) continue;
      const dx = x - sx;
      const d2 = dx * dx + dy * dy;
      if (d2 < bestD2) {
        bestD2 = d2;
        seedIdx = i;
      }
    }
  }

  const dist = new Int32Array(n).fill(INK_INF);
  const settledOnInk = new Uint8Array(n); // 1차에서 확정된 먹선 픽셀 (2차가 덮지 않음)

  // --- 1차: 먹선 그래프 위 균일비용(1) BFS → 측지 거리 = 획 순서 ---
  dist[seedIdx] = 0;
  if (inkCount > 0) {
    const queue = new Int32Array(inkCount);
    let head = 0;
    let tail = 0;
    settledOnInk[seedIdx] = 1;
    queue[tail++] = seedIdx;
    while (head < tail) {
      const cur = queue[head++];
      const cx = cur % width;
      const cy = (cur - cx) / width;
      const nd = dist[cur] + 1;
      for (let k = 0; k < 8; k++) {
        const nx = cx + NEIGHBORS8[k][0];
        const ny = cy + NEIGHBORS8[k][1];
        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
        const ni = ny * width + nx;
        if (!ink[ni] || dist[ni] <= nd) continue;
        dist[ni] = nd;
        settledOnInk[ni] = 1;
        queue[tail++] = ni;
      }
    }
  }

  // --- 2차: 1차 도달 픽셀 전체를 시작점으로 한 다원 Dial 확산 ---
  // 걸음 비용은 목적지 종류로: 먹선 1, 그 외(담채·투명 빈틈) 3. 투명을 건널 수
  // 있어 떨어진 조각도 큰 거리로 도달한다. 1차 확정 먹선은 고정(순서 보존).
  // 상한은 1차 측지 최대값(굽이치는 획은 캔버스 폭보다 길 수 있다) + 2차 확산 여유.
  // 이렇게 해야 먼 먹선 꼬리도 확산 시작점으로 쓰이고, 정규화 시 클램프로 값이
  // 잘리지 않는다. (짧은/곧은 획에서는 값이 이전과 동일하게 나온다.)
  let maxSeed = 0;
  for (let i = 0; i < n; i++) {
    const d = dist[i];
    if (d < INK_INF && d > maxSeed) maxSeed = d;
  }
  const maxDist = maxSeed + 3 * (width + height) + 8;
  const buckets = new Array(maxDist + 1);
  for (let i = 0; i < n; i++) {
    const d = dist[i];
    if (d < INK_INF) (buckets[d] || (buckets[d] = [])).push(i);
  }
  for (let d = 0; d <= maxDist; d++) {
    const bucket = buckets[d];
    if (bucket === undefined) continue;
    for (let bi = 0; bi < bucket.length; bi++) {
      const cur = bucket[bi];
      if (dist[cur] !== d) continue; // 더 작은 값으로 갱신된 낡은 항목
      const cx = cur % width;
      const cy = (cur - cx) / width;
      for (let k = 0; k < 8; k++) {
        const nx = cx + NEIGHBORS8[k][0];
        const ny = cy + NEIGHBORS8[k][1];
        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
        const ni = ny * width + nx;
        if (settledOnInk[ni]) continue; // 1차 먹선 순서는 덮지 않는다
        const nd = d + (ink[ni] ? INK_STEP_COST : NONINK_STEP_COST);
        if (nd < dist[ni]) {
          dist[ni] = nd;
          if (nd <= maxDist) (buckets[nd] || (buckets[nd] = [])).push(ni);
        }
      }
    }
    buckets[d] = undefined; // 처리 완료 버킷 해제
  }

  // --- 정규화: 불투명 픽셀 거리의 99.5 백분위수로 나눠 0..1, 투명은 1.0 ---
  const hist = new Int32Array(maxDist + 1);
  for (let i = 0; i < n; i++) {
    if (!opaque[i]) continue;
    let d = dist[i];
    if (d > maxDist) d = maxDist; // 도달 실패 방지용 상한
    hist[d]++;
  }
  const targetCount = Math.max(1, Math.ceil(INK_PERCENTILE * opaqueCount));
  let cumulative = 0;
  let percentile = 1;
  for (let d = 0; d <= maxDist; d++) {
    cumulative += hist[d];
    if (cumulative >= targetCount) {
      percentile = Math.max(1, d);
      break;
    }
  }

  for (let i = 0; i < n; i++) {
    if (!opaque[i]) {
      out[i] = 255; // 투명 = 1.0
      continue;
    }
    const norm = Math.min(1, dist[i] / percentile);
    out[i] = Math.max(0, Math.min(255, Math.round(norm * 255)));
  }
  return out;
}

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

  // 트림된 요소 RGBA를 실체화해 요소 PNG와 잉크마스크를 같은 픽셀에서 얻는다.
  const trimmedRaw = await sharp(data, {
    raw: { width: info.width, height: info.height, channels: 4 },
  }).extract(trim).raw().toBuffer();
  const pngBuffer = await sharp(trimmedRaw, {
    raw: { width: trim.width, height: trim.height, channels: 4 },
  }).png().toBuffer();

  const sourcePivot = Array.isArray(item?.pivot) && item.pivot.length >= 2
    ? item.pivot
    : [0.5, 0.5];
  const pivotSourceX = sourcePivot[0] * info.width;
  const pivotSourceY = sourcePivot[1] * info.height;
  const pivotTrimmed = [
    clamp01((pivotSourceX - trim.left) / trim.width),
    clamp01((pivotSourceY - trim.top) / trim.height),
  ];

  // 시드: 매니페스트 inkSeed(정규화 좌표, 트림 프레임 기준)가 있으면 그 지점,
  // 없으면 트림된 피벗 좌표에서 가장 가까운 먹선 픽셀로 성장 순서를 잡는다.
  const inkSeed = Array.isArray(item?.inkSeed) && item.inkSeed.length >= 2
    ? [clamp01(item.inkSeed[0]), clamp01(item.inkSeed[1])]
    : pivotTrimmed;
  const inkMaskData = computeInkMask(trimmedRaw, trim.width, trim.height, inkSeed);

  return {
    pngBuffer,
    inkMask: { data: inkMaskData, width: trim.width, height: trim.height },
    meta: {
      id: item?.id || null,
      originalSize: { width: info.width, height: info.height },
      size: { width: trim.width, height: trim.height },
      trim,
      pivot: pivotTrimmed,
      unityPivot: [pivotTrimmed[0], 1 - pivotTrimmed[1]],
      inkSeed,
      cutoutApplied,
    },
  };
}

async function encodeInkMaskPng(inkMask) {
  // 단채널(그레이) 8비트 PNG — 요소와 동일 해상도. 잉크순서 값이 곧 휘도.
  // b-w 컬러스페이스를 강제해 sharp가 RGB로 확장하지 않게 한다.
  return sharp(Buffer.from(inkMask.data), {
    raw: { width: inkMask.width, height: inkMask.height, channels: 1 },
  }).toColourspace("b-w").png({ compressionLevel: 9 }).toBuffer();
}

async function writeOutput(outputDir, id, pngBuffer, inkMask, meta) {
  await mkdir(outputDir, { recursive: true });
  const outPath = path.join(outputDir, `${id}.png`);
  const tempPath = path.join(outputDir, `${id}.tmp.png`);
  await writeFile(tempPath, pngBuffer);
  await rename(tempPath, outPath);

  if (inkMask) {
    const maskPng = await encodeInkMaskPng(inkMask);
    const maskPath = path.join(outputDir, `${id}${INK_MASK_SUFFIX}.png`);
    const maskTemp = path.join(outputDir, `${id}${INK_MASK_SUFFIX}.tmp.png`);
    await writeFile(maskTemp, maskPng);
    await rename(maskTemp, maskPath);
  }

  await writeFile(path.join(outputDir, `${id}.meta.json`), JSON.stringify(meta, null, 2) + "\n", "utf8");
  return outPath;
}

async function prepFile(inputPath, manifestInfo) {
  const id = path.basename(inputPath, path.extname(inputPath));
  const item = manifestInfo.byId.get(id);
  if (!item) throw new Error(`Element id not found in manifest: ${id}`);
  const { pngBuffer, inkMask, meta } = await prepBuffer(await readFile(inputPath), item);
  meta.id = id;
  meta.source = path.relative(PROJECT_ROOT, inputPath).replaceAll("\\", "/");
  meta.inkMask = inkMask ? `${id}${INK_MASK_SUFFIX}.png` : null;
  const outPath = await writeOutput(manifestInfo.outputDir, id, pngBuffer, inkMask, meta);
  console.log(`${id}.png: ${meta.size.width}x${meta.size.height}, pivot ${meta.pivot.map((v) => v.toFixed(4)).join(", ")}, cutout ${meta.cutoutApplied ? "yes" : "no"}, inkmask ${meta.inkMask ?? "none"} -> ${path.relative(PROJECT_ROOT, outPath)}`);
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

  await inkMaskSelftest();

  console.log("selftest PASS: cutout, trim, pivot recalculation, and ink-order mask verified");
}

// 합성 요소(가로 먹선 1개 + 선에서 떨어진 먹 원 1개)로 그려짐 순서를 검증한다:
// ① 선을 따라 값이 단조 증가 ② 떨어진 원의 값 > 선 끝 값 ③ 출력이 단채널.
async function inkMaskSelftest() {
  const W = 320;
  const H = 160;
  const raw = Buffer.alloc(W * H * 4); // 전부 투명(alpha 0)에서 시작

  const paintDark = (x, y) => {
    const i = (y * W + x) * 4;
    raw[i] = 30;
    raw[i + 1] = 25;
    raw[i + 2] = 22;
    raw[i + 3] = 255;
  };

  // 가로 먹선: x 20..179, y 78..82
  for (let y = 78; y <= 82; y++) {
    for (let x = 20; x <= 179; x++) paintDark(x, y);
  }
  // 선에서 떨어진 먹 원: 중심 (272,40), 반지름 14 (투명 간극으로 분리)
  const cx = 272;
  const cy = 40;
  const r = 14;
  for (let y = cy - r; y <= cy + r; y++) {
    for (let x = cx - r; x <= cx + r; x++) {
      const dx = x - cx;
      const dy = y - cy;
      if (dx * dx + dy * dy <= r * r) paintDark(x, y);
    }
  }

  const input = await sharp(raw, { raw: { width: W, height: H, channels: 4 } }).png().toBuffer();
  // 시드 = 선의 왼쪽 끝 (트림 프레임 기준 좌하단 근처) → 왼→오 성장
  const item = { id: "inkselftest", pivot: [0.5, 0.5], inkSeed: [0.0, 0.964] };
  const { inkMask } = await prepBuffer(input, item);
  assert.ok(inkMask, "ink mask should be produced");

  const at = (tx, ty) => inkMask.data[ty * inkMask.width + tx];

  // 트림 프레임: 불투명 경계 = x[20..286], y[26..82] → 원점 (20,26)
  const lineRow = 80 - 26;            // 선 중심행 (트림 좌표)
  const samples = [];
  for (let tx = 0; tx <= 152; tx += 8) samples.push(at(tx, lineRow));
  for (let k = 1; k < samples.length; k++) {
    assert.ok(samples[k] >= samples[k - 1],
      `ink order should be monotonic along the stroke (idx ${k}: ${samples[k - 1]} -> ${samples[k]})`);
  }
  assert.ok(samples[samples.length - 1] > samples[0] + 8,
    `stroke should grow from seeded end to far end (${samples[0]} -> ${samples[samples.length - 1]})`);

  const lineEndValue = at(159, lineRow);        // 선 오른쪽 끝
  const circleValue = at(272 - 20, 40 - 26);    // 떨어진 원 중심 (트림 좌표)
  assert.ok(circleValue > lineEndValue,
    `detached piece should be drawn after the stroke (circle ${circleValue} > stroke end ${lineEndValue})`);

  const maskPng = await encodeInkMaskPng(inkMask);
  const maskMeta = await sharp(maskPng).metadata();
  assert.equal(maskMeta.channels, 1, "ink mask PNG must be single-channel");

  await windingStrokeSelftest();
}

// 굽이치는 긴 획(측지 거리가 캔버스 폭을 훌쩍 넘는다)에서도 순서가 무너지지 않는지:
// 획을 따라 값이 단조 증가하고 정규화가 붕괴(전부 동일값)하지 않아야 한다.
async function windingStrokeSelftest() {
  const W = 100, H = 100;
  const raw = Buffer.alloc(W * H * 4);
  const path = [];
  const x0 = 6, x1 = 93, y0 = 6, dy = 6, rows = 16;
  for (let r = 0; r < rows; r++) {
    const y = y0 + r * dy;
    if (r % 2 === 0) for (let x = x0; x <= x1; x++) path.push([x, y]);
    else for (let x = x1; x >= x0; x--) path.push([x, y]);
    if (r < rows - 1) {
      const cx = r % 2 === 0 ? x1 : x0;
      for (let yy = y + 1; yy < y + dy; yy++) path.push([cx, yy]);
    }
  }
  for (const [x, y] of path) {
    const i = (y * W + x) * 4;
    raw[i] = 28; raw[i + 1] = 24; raw[i + 2] = 20; raw[i + 3] = 255;
  }
  // 측지 길이가 옛 상한 4*(W+H)=800 을 넘는지 확인 (이 국면을 실제로 밟는다)
  assert.ok(path.length > 4 * (W + H), `winding geodesic ${path.length} should exceed the old span bound`);

  const input = await sharp(raw, { raw: { width: W, height: H, channels: 4 } }).png().toBuffer();
  const item = { id: "windtest", pivot: [0.5, 0.5], inkSeed: [0.0, 0.0] }; // 시드 = 획 시작점
  const { inkMask } = await prepBuffer(input, item);
  const at = (tx, ty) => inkMask.data[ty * inkMask.width + tx];

  let prev = -1;
  for (let k = 0; k < path.length; k += 40) {
    const tx = path[k][0] - x0, ty = path[k][1] - y0; // 트림 원점 = (x0,y0)
    const v = at(tx, ty);
    assert.ok(v >= prev, `winding order must be monotonic along the stroke (step ${k}: ${prev} -> ${v})`);
    prev = v;
  }

  // 시작·중반·후반·끝 근처에서 값이 '엄격히' 증가해야 한다. 옛 상한(span-only)에서는
  // 먼 획 픽셀 거리가 잘려 백분위수가 붕괴 → 후반이 255로 뭉개져 이 검사가 실패한다 (회귀 가드).
  const frac = (f) => {
    const k = Math.min(path.length - 1, Math.floor(f * path.length));
    return at(path[k][0] - x0, path[k][1] - y0);
  };
  const v10 = frac(0.10), v35 = frac(0.35), v60 = frac(0.60), v85 = frac(0.85);
  assert.ok(v10 < v35 && v35 < v60 && v60 < v85,
    `winding stroke order must stay strictly increasing end-to-end (got ${v10} < ${v35} < ${v60} < ${v85})`);
  assert.ok(v85 < 255, `mid-late stroke must not collapse to 'drawn last' (${v85})`);
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
