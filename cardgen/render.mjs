// 카드/오버레이 렌더러.
// 사용법: node render.mjs <all | card <id> | overlays | preview> [--copy <dir>]
// Node 내장 http로 cardgen/을 서빙하고 playwright chromium으로 캡처한다.
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readFile, readdir, mkdir, cp } from "node:fs/promises";
import { chromium } from "playwright";

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const OUT_CARDS = path.join(ROOT, "out", "cards");
const OUT_OVERLAYS = path.join(ROOT, "out", "overlays");
const READY_TIMEOUT_MS = 30000;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".mjs": "text/javascript",
  ".js": "text/javascript",
  ".css": "text/css",
  ".woff2": "font/woff2",
};

function startServer() {
  const server = http.createServer(async (req, res) => {
    try {
      const pathname = decodeURIComponent(new URL(req.url, "http://127.0.0.1").pathname);
      const rel = pathname === "/" ? "template.html" : pathname.slice(1);
      const file = path.resolve(ROOT, rel);
      if (file !== ROOT && !file.startsWith(ROOT + path.sep)) {
        res.writeHead(403);
        res.end("forbidden");
        return;
      }
      const body = await readFile(file);
      const type = MIME[path.extname(file).toLowerCase()] ?? "application/octet-stream";
      res.writeHead(200, { "content-type": type });
      res.end(body);
    } catch {
      res.writeHead(404);
      res.end("not found");
    }
  });
  return new Promise((resolve) =>
    server.listen(0, "127.0.0.1", () =>
      resolve({ server, base: `http://127.0.0.1:${server.address().port}` })
    )
  );
}

async function loadCards() {
  return JSON.parse(await readFile(path.join(ROOT, "cards.json"), "utf8"));
}

async function waitReady(page) {
  await page.waitForSelector('body[data-ready="1"], body[data-error="1"]', {
    state: "attached",
    timeout: READY_TIMEOUT_MS,
  });
  if (await page.evaluate(() => document.body.dataset.error === "1")) {
    throw new Error(`template error: ${await page.locator("pre").first().innerText()}`);
  }
  await page.evaluate(() => document.fonts.ready);
}

async function captureCard(page, base, id) {
  await page.goto(`${base}/template.html?card=${encodeURIComponent(id)}`);
  await waitReady(page);
  const out = path.join(OUT_CARDS, `${id}.png`);
  await page.locator(`#card-${id}`).screenshot({ omitBackground: true, path: out });
  return out;
}

async function captureOverlay(page, base, name, prefix) {
  await page.goto(`${base}/template.html?overlay=${encodeURIComponent(name)}`);
  await waitReady(page);
  const out = path.join(OUT_OVERLAYS, `${prefix}_${name}.png`);
  await page.locator(`#overlay-${name}`).screenshot({ omitBackground: true, path: out });
  return out;
}

// frames/*.svg → frame_<이름>, badges/*.svg → badge_<이름>
async function listOverlays() {
  const found = [];
  for (const [dir, prefix] of [["frames", "frame"], ["badges", "badge"]]) {
    let files = [];
    try {
      files = await readdir(path.join(ROOT, dir));
    } catch { /* 폴더 없으면 건너뜀 */ }
    for (const f of files) {
      if (f.toLowerCase().endsWith(".svg")) found.push({ name: f.slice(0, -4), prefix });
    }
  }
  return found;
}

async function withBrowser(fn) {
  const { server, base } = await startServer();
  const browser = await chromium.launch();
  try {
    const context = await browser.newContext({
      viewport: { width: 1280, height: 960 },
      deviceScaleFactor: 1,
    });
    await fn(await context.newPage(), base);
  } finally {
    await browser.close();
    server.close();
  }
}

// out/cards → <dir>/Base, out/overlays → <dir>/Overlays
async function copyOut(dir) {
  const target = path.resolve(process.cwd(), dir);
  const jobs = [
    [OUT_CARDS, path.join(target, "Base")],
    [OUT_OVERLAYS, path.join(target, "Overlays")],
  ];
  for (const [src, dst] of jobs) {
    try {
      await readdir(src);
    } catch {
      continue; // 렌더된 적 없는 폴더는 건너뜀
    }
    await mkdir(dst, { recursive: true });
    await cp(src, dst, { recursive: true, force: true });
    console.log(`복사: ${src} → ${dst}`);
  }
}

function usage() {
  console.error("사용법: node render.mjs <all | card <id> | overlays | preview> [--copy <dir>]");
  process.exit(1);
}

const argv = process.argv.slice(2);
let copyDir = null;
const copyIdx = argv.indexOf("--copy");
if (copyIdx !== -1) {
  copyDir = argv[copyIdx + 1];
  if (!copyDir) usage();
  argv.splice(copyIdx, 2);
}
const [cmd, cmdArg] = argv;

switch (cmd) {
  case "preview": {
    const { server, base } = await startServer();
    console.log(`프리뷰 서버: ${base}/template.html  (Ctrl+C로 종료)`);
    process.on("SIGINT", () => {
      server.close();
      process.exit(0);
    });
    break;
  }

  case "all": {
    const { cards } = await loadCards();
    await mkdir(OUT_CARDS, { recursive: true });
    await withBrowser(async (page, base) => {
      let n = 0;
      for (const card of cards) {
        await captureCard(page, base, card.id);
        console.log(`[${++n}/${cards.length}] ${card.id}.png`);
      }
    });
    if (copyDir) await copyOut(copyDir);
    break;
  }

  case "card": {
    if (!cmdArg) usage();
    const { cards } = await loadCards();
    if (!cards.some((c) => c.id === cmdArg)) {
      console.error(`cards.json에 없는 id: ${cmdArg}`);
      process.exit(1);
    }
    await mkdir(OUT_CARDS, { recursive: true });
    await withBrowser(async (page, base) => {
      await captureCard(page, base, cmdArg);
      console.log(`${cmdArg}.png`);
    });
    if (copyDir) await copyOut(copyDir);
    break;
  }

  case "overlays": {
    const overlays = await listOverlays();
    if (overlays.length === 0) {
      console.error("frames/·badges/에 svg가 없다");
      process.exit(1);
    }
    await mkdir(OUT_OVERLAYS, { recursive: true });
    await withBrowser(async (page, base) => {
      for (const { name, prefix } of overlays) {
        await captureOverlay(page, base, name, prefix);
        console.log(`${prefix}_${name}.png`);
      }
    });
    if (copyDir) await copyOut(copyDir);
    break;
  }

  default:
    usage();
}
