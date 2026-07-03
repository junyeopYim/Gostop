# cardgen — 화투 카드 아트 파이프라인

카드 데이터의 유일한 원천은 `cards.json`. 팔레트/테두리 변경은 파일 하나 수정 → 재렌더로 끝난다.

## 설치

```
npm install
npx playwright install chromium
```

## 명령

```
node render.mjs preview            # 브라우저 프리뷰 (50장 격자)
node render.mjs all                # 전 카드+뒷면 → out/cards/*.png (300x450)
node render.mjs card m08_gwang     # 1장만 재렌더
node render.mjs back               # 뒷면만 → out/cards/card_back.png
node render.mjs overlays           # frames/·badges/ → out/overlays/*.png
node render.mjs ui                 # ui/*.svg → out/ui/*.png
node render.mjs all --copy <dir>   # 렌더 후 카드/오버레이/낙관 복사
node render.mjs ui --copy ../Assets/Art/Cards  # UI는 형제 폴더 ../Assets/Art/UI로 복사
node prep-illust.mjs <파일|폴더>    # 래스터 일러스트 전처리 (중앙 cover 크롭 + 종이톤 정규화)
node prep-illust.mjs <파일|폴더> --no-normalize  # 정규화 없이 크롭만 (전후 비교용)
node prep-illust.mjs --selftest    # 전처리 자가 검증
```

라벨 폰트는 Google Fonts의 Nanum Brush Script를 렌더 시점에 로드한다(네트워크 필요; 오프라인이면 시스템 폰트 폴백). 추후 woff2를 이 폴더에 셀프호스팅하고 `template.html`의 `<link>`를 `@font-face`로 바꾸면 오프라인 렌더가 된다.

## 일러스트 입력 규격 (사람/AI 워크플로우용)

- 화풍: **담채 민화(수묵담채)** — 낡은 한지 배경을 그림 안에 포함해 full-bleed로 생성한다. 그림 속 한지가 곧 카드 바탕이고, 그 위에 프레임과 라벨이 얹힌다 (광 5장은 금테 `frames/gold.svg`, 나머지는 기본 `frame.svg`)
- 파일명 = 카드 id (`cards.json`의 id, 예: `m08_gwang.png`). 규칙: `m{두자리월}_{타입}{선택접미사}`
- 종횡비 **2:3** (artZone 300x450과 정확히 일치 — 크롭 손실 없음), 짧은 변 1024px 이상 권장
- 워크플로우: 생성물 → `illustrations_raw/` → `node prep-illust.mjs illustrations_raw` → `node render.mjs all --copy <UnityArt경로>`
- 전처리는 중앙 cover 크롭(300x450) 후 **종이톤 정규화**(네 모서리 24x24 중앙값으로 원본 종이색을 추정해 palette `paper`로 채널별 게인 보정, 0.7~1.4 클램프)를 거쳐 `illustrations/<id>.png`로 저장한다
- 같은 월 피 2장은 `cards.json`의 `illust` 필드에 같은 파일을 지정해 일러스트를 공유할 수 있다 (그림 수 48→39 절감)
- 완성본이 png이면 `cards.json`의 해당 카드 `illust`를 `<id>.png`로 바꾼다 (svg 일러스트는 전처리 없이 `illustrations/`에 직접, 색은 반드시 `var(--팔레트명)`만 사용)
- 현재 `illustrations/`의 데모 svg 4장(m02_yeol·m03_hongdan·m08_gwang·m11_ssangpi)은 플랫 벡터 구세대 화풍으로, 담채 일러스트로 교체 대상이다
