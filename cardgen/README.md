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
node render.mjs all                # 전 카드 → out/cards/*.png (300x450)
node render.mjs card m08_gwang     # 1장만 재렌더
node render.mjs overlays           # frames/·badges/ → out/overlays/*.png
node render.mjs all --copy <dir>   # 렌더 후 out/cards→<dir>/Base, out/overlays→<dir>/Overlays 복사
node prep-illust.mjs <파일|폴더>    # 래스터 일러스트 전처리 (크롭 + 8색 양자화)
node prep-illust.mjs --selftest    # 전처리 자가 검증
```

라벨 폰트는 Google Fonts의 Nanum Brush Script를 렌더 시점에 로드한다(네트워크 필요; 오프라인이면 시스템 폰트 폴백). 추후 woff2를 이 폴더에 셀프호스팅하고 `template.html`의 `<link>`를 `@font-face`로 바꾸면 오프라인 렌더가 된다.

## 일러스트 입력 규격 (사람/AI 워크플로우용)

- 파일명 = 카드 id (`cards.json`의 id, 예: `m08_gwang.png`). 규칙: `m{두자리월}_{타입}{선택접미사}`
- 원본 권장 크기: 짧은 변 1024px 이상, 종횡비 무관 (artZone 268:418 ≈ 0.64:1 세로형에 가까울수록 크롭 손실이 적다)
- 원본은 `illustrations_raw/`에 넣고 `node prep-illust.mjs illustrations_raw`를 실행하면 중앙 cover 크롭(268x418) + 팔레트 8색 양자화를 거쳐 `illustrations/<id>.png`로 저장된다
- 완성본이 png이면 `cards.json`의 해당 카드 `illust`를 `<id>.png`로 바꾼다 (svg 일러스트는 전처리 없이 `illustrations/`에 직접, 색은 반드시 `var(--팔레트명)`만 사용)
