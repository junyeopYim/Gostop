# Gostop Card Art Direction

이 문서는 `cardgen` 일러스트 생성 작업의 스타일 계약과 검수 기준이다. 목표는 개별적으로 예쁜 이미지를 모으는 것이 아니라, 프로토타입/데모에서 한 벌의 덱처럼 보이는 일관된 화투패 일러스트 세트를 만드는 것이다.

카드 프레임, 월 표시, 라벨은 `cardgen/template.html`과 렌더러가 얹는다. 생성 이미지는 카드 안쪽에 깔리는 순수 일러스트 원본이어야 하며, 이미지 자체에는 텍스트나 프레임을 넣지 않는다.

## 핵심 스타일 계약

- 방향: 낡은 한지 위에 그린 수묵담채/민화풍 화투패 일러스트.
- 시대감: 조선 민화 앨범 잎처럼 소박하고, 약간 순진하며, 손맛이 살아 있는 그림.
- 선: 굵기가 자연스럽게 흔들리는 손그림 먹선. 균일한 벡터 외곽선이나 굵고 딱딱한 디지털 라인은 실패로 본다.
- 채색: 부드러운 광물 안료 담채. 꽃잎, 깃털, 털 안쪽에 은은한 층과 번짐이 있어야 하며, 평평한 디지털 단색 채우기는 피한다.
- 분위기: 조용하고 오래된 카드 묶음처럼 보여야 한다. 한 장만 튀는 밝기, 채도, 종이색, 선 두께는 실패다.
- 밀도: 주 피사체 하나와 최대 한 개의 작은 보조 모티프만 둔다. 화면의 절반 이상은 빈 한지 여백이어야 한다.
- 렌더 계약: 원본은 portrait 2:3 비율, 가능하면 짧은 변 1024px 이상으로 만든다. 전처리 후 `artZone` 300x450에 자연스럽게 들어가야 한다.

## 기준 프롬프트

모든 카드 일러스트 생성은 아래 프롬프트를 기본 스타일 계약으로 사용한다. 카드별로 `{Subject}`만 교체하고, 스타일 문장은 임의로 약화하거나 제거하지 않는다.

```text
Traditional Korean minhwa folk painting in the manner of a sparse, naive
Joseon album leaf — simple, understated and charming.
Hand-painted ink outlines of naturally varying weight; a few confident,
economical brush strokes rather than dense rendering.
Soft matte mineral-pigment washes with gentle layered gradation inside
petals, feathers and fur — never flat digital fills.
Color mood: softly faded antique tones — muted vermilion-rose, deep
indigo-teal greens, warm ochre gold, ink black — quiet, harmonious,
slightly sun-faded, never saturated or bright.
Composition: ONE main subject, moderately sized, plus at most one small
supporting motif; at least half of the frame is empty paper.
No background scenery, no landscape, no ground plane, no filler foliage.
Background: warm aged cream hanji paper with subtle grain, edge to edge —
the same warm paper tone in every image. Portrait 2:3.
Slightly naive, endearing proportions with a hint of humor.
No text, letters, numbers, seals, borders or watermarks.
Avoid: busy compositions, dark backgrounds, night scenes, flat vector
graphics, uniform bold outlines, oversaturated colors.

{Subject}
```

## 색감

- 기본 종이색은 `cards.json`의 `palette.paper`에 가까운 따뜻한 크림 한지 톤으로 맞춘다.
- 주요 색은 muted vermilion-rose, deep indigo-teal green, warm ochre gold, ink black 범위에서 운용한다.
- 같은 batch 안에서 한지 배경의 색온도와 밝기가 흔들리면 실패다.
- 빨강은 낙관이나 UI 강조색처럼 강하게 튀지 않게, 오래된 주홍/장미색으로 낮춘다.
- 파랑/초록은 청단, 초단, 잎사귀 구분에 쓰되 채도를 낮춰 카드 전체를 압도하지 않게 한다.
- 금색은 광 카드나 작은 장식감에만 절제해서 쓴다. 금박처럼 번쩍이거나 현대적인 메탈 질감은 피한다.

## 구성

- 출력 비율은 반드시 portrait 2:3이다.
- 피사체는 중간 크기로 두고, 카드 안에서 숨 쉴 여백을 남긴다.
- 좌상단 월 라벨과 우하단 카드 라벨이 올라갈 수 있으므로, 이 영역에 중요한 얼굴, 눈, 달, 태양, 동전 같은 핵심 디테일을 두지 않는다.
- 주 피사체는 하나만 둔다. 보조 모티프는 있어도 하나 이하여야 한다.
- 배경 풍경, 지평선, 땅, 산, 물가, 건물, 과도한 풀숲을 만들지 않는다.
- 한지는 이미지 전체에 edge-to-edge로 깔려야 하며, 별도 카드 테두리나 프레임을 이미지 안에 넣지 않는다.
- 광/열끗/띠/피는 서로 식별되어야 하지만, 한 작가가 같은 재료로 그린 것처럼 보여야 한다.

## 금지 요소

생성 이미지에는 다음 요소가 절대 들어가면 안 된다.

- 텍스트
- 글자
- 숫자
- 도장/낙관
- 카드 테두리
- 카드 프레임
- UI 요소
- 워터마크
- 로고
- 과도한 배경 풍경
- 복잡한 장면
- 현대적인 물건
- 포토리얼/3D/애니풍/벡터풍

## 파일 및 폴더 규칙

- `cardgen/cards.json`
  - 카드 id, canvas 크기, palette, `illust` 파일명의 기준이다.
  - 생성 파일 basename은 카드 id와 정확히 일치해야 한다.
  - 예: `m08_gwang` 카드의 최종 png는 `m08_gwang.png`.
- `cardgen/illustrations_raw/`
  - AI가 생성한 원본 후보 이미지를 보관한다.
  - batch별 하위 폴더를 사용한다.
  - 예: `cardgen/illustrations_raw/_batch_01_03/`, `cardgen/illustrations_raw/_batch_04_06/`, `cardgen/illustrations_raw/_batch_07_09/`, `cardgen/illustrations_raw/_batch_10_12_bonus/`.
  - 원본 후보는 지우지 말고, 실패 후보도 검수 메모와 함께 남길 수 있다.
- `cardgen/illustrations/`
  - `prep-illust.mjs`가 만든 전처리 결과만 둔다.
  - 사람이 임의로 직접 편집하지 않는다.
- `cardgen/out/cards/`
  - `node render.mjs all` 결과물이 저장되는 위치다.
- `cardgen/out/overlays/`
  - overlay 렌더 결과물이 저장되는 위치다.

`prep-illust.mjs`가 `illustrations_raw` 하위 batch 폴더를 재귀 처리하지 못하는 경우, batch별 폴더를 직접 지정하거나 raw 파일을 일시적으로 루트로 모아 처리한다. basename 충돌은 반드시 피한다.

## MVP 6장 스타일 앵커

처음부터 전체 덱을 확장하지 않고, 아래 6장을 먼저 생성해 스타일 앵커로 검수한다.

| id | Subject | 검수 포인트 |
| --- | --- | --- |
| `m01_gwang` | A white crane standing gracefully under a sparse pine branch, with a small muted red sun in the upper empty space. | 학, 소나무, 붉은 해가 한 화면에 들어가되 과밀하지 않아야 한다. |
| `m06_yeol` | A single red peony blossom with a tiny butterfly hovering above it, delicate leaves, large empty hanji paper around it. | 모란의 붉은색이 과포화되지 않고, 나비는 보조 모티프로 작아야 한다. |
| `m08_gwang` | A round full moon above a quiet sweep of pampas grass, very sparse and poetic, mostly empty warm paper. | 달과 억새가 고요하게 보이고 배경 풍경으로 번지지 않아야 한다. |
| `m09_yeol` | A yellow chrysanthemum stem leaning beside a small traditional cup, simple and understated, generous empty paper. | 국화와 잔이 구분되지만 정물화처럼 과하게 묘사되지 않아야 한다. |
| `m12_gwang` | A skeletal Joseon scholar figure holding a red paper umbrella in light rain, with one small frog near the feet, charming rather than frightening. | 해골 선비가 무섭거나 복잡하지 않고 민화적으로 귀엽게 보여야 한다. |
| `bonus_ssangpi_a` | A small lucky pouch and two simple old coins arranged sparsely on aged hanji paper, playful and handmade, no text. | 동전에 글자, 문양, 숫자가 들어가면 실패다. |

MVP 6장은 한 화면에 펼쳤을 때 종이색, 먹선 두께, 채도, 여백 비율이 같은 덱으로 보여야 한다. 한 장이라도 튀면 나머지 batch를 생성하지 말고 프롬프트를 먼저 조정한다.

## MVP 승인 기준

- 6장 모두 warm aged cream hanji paper가 edge-to-edge로 깔려 있다.
- 모든 이미지가 portrait 2:3이며, 전처리 후 300x450에서 큰 크롭 없이 보인다.
- 화면 절반 이상이 빈 한지 여백이다.
- 좌상단과 우하단 라벨 영역이 핵심 피사체를 심하게 가리지 않는다.
- 텍스트, 숫자, 도장, 낙관, 워터마크, 카드 테두리, 프레임이 없다.
- 포토리얼, 3D, 애니풍, 벡터풍으로 보이지 않는다.
- 작은 카드 크기에서도 월/카드 타입의 핵심 모티프를 알아볼 수 있다.
- 여섯 장을 동시에 봤을 때 한 작가가 같은 재료로 그린 것처럼 보인다.

## 실패 조건

다음 중 하나라도 해당하면 QA 미승인으로 처리한다.

- 이미지 안에 글자, 숫자, 도장, 낙관, 서명, 워터마크가 보인다.
- 카드 테두리, 프레임, UI 버튼, 라벨이 생성물 안에 포함되어 있다.
- 종이 배경이 흰색, 회색, 어두운색, 강한 노랑 등으로 batch 내 다른 카드와 다르다.
- 피사체가 너무 커서 월 라벨 또는 카드 라벨 영역을 막는다.
- 피사체가 너무 작거나 흐릿해서 300x450 카드에서 식별되지 않는다.
- 배경 풍경, 지면, 건물, 복잡한 풀숲, 현대적인 사물이 생겼다.
- 선이 균일한 벡터 라인처럼 보이거나, 색이 납작한 디지털 fill처럼 보인다.
- 채도가 높아 다른 카드보다 밝고 현대적으로 튄다.
- 원본 비율이 portrait 2:3이 아니어서 전처리 과정에서 과도하게 잘린다.
- 같은 batch 안에서 먹선 두께, 종이 질감, 색온도가 크게 흔들린다.

## 전처리 및 렌더 확인

이미지 후보 생성 후 기본 흐름은 다음과 같다.

```bash
cd cardgen
npm install
npx playwright install chromium
node prep-illust.mjs illustrations_raw
node render.mjs preview
node render.mjs all
```

전후 비교가 필요하면 다음 명령으로 정규화 없는 결과도 확인한다.

```bash
node prep-illust.mjs illustrations_raw --no-normalize
```

Unity 아트 경로가 정해진 경우에만 복사를 수행한다.

```bash
node render.mjs all --copy <UnityArtPath>
```

`prep-illust.mjs`, `render.mjs`, `template.html`은 렌더링 계약의 일부이므로 이 작업에서 임의 수정하지 않는다. 필요한 변경점은 제안으로만 남긴다.

## cards.json 갱신 조건

`cards.json`의 `illust` 필드는 다음 조건을 모두 만족할 때만 `.png`로 바꾼다.

1. `cardgen/illustrations/<card_id>.png`가 실제로 존재한다.
2. 해당 이미지는 `prep-illust.mjs`를 통과했다.
3. `render.mjs preview`에서 잘림, 텍스트 유입, 과도한 디테일, 종이색 불일치가 없다.
4. 좌상단 월 라벨과 우하단 카드 라벨 뒤쪽에 충분한 여백이 있다.
5. QA report에서 승인 상태다.

예:

```json
{ "id": "m08_gwang", "illust": "m08_gwang.svg" }
```

승인 후:

```json
{ "id": "m08_gwang", "illust": "m08_gwang.png" }
```

존재하지 않는 png를 `cards.json`에 연결하지 않는다. 전체 batch가 끝나지 않아도 승인된 카드만 부분 갱신할 수 있다. 같은 월의 피 카드 2장이 같은 일러스트를 공유하는 것은 허용되지만, 파일명과 `cards.json` 연결은 명시적으로 확인해야 한다.

## 최종 QA 체크리스트

- 파일명이 `cards.json` id와 일치하는가?
- 원본 비율이 portrait 2:3인가?
- 전처리 후 `cardgen/illustrations/<id>.png`가 300x450으로 출력되는가?
- warm aged cream hanji paper가 edge-to-edge로 들어갔는가?
- 종이색이 다른 이미지들과 비슷한가?
- 먹선이 손그림처럼 자연스럽고 굵기 변화가 있는가?
- 채색이 부드러운 담채/광물 안료처럼 보이는가?
- 주 피사체 하나와 최대 한 개의 보조 모티프만 있는가?
- 화면 절반 이상이 빈 한지 여백인가?
- 좌상단 월 라벨과 우하단 카드 라벨 공간이 확보되어 있는가?
- 텍스트, 숫자, 도장, 낙관, 서명, 워터마크가 없는가?
- 카드 테두리나 프레임이 이미지 안에 없는가?
- 작은 카드 크기에서도 피사체를 알아볼 수 있는가?
- 같은 월 카드끼리 느슨하게 연결되어 보이는가?
- 전체 덱을 펼쳤을 때 한 작가가 그린 것처럼 보이는가?
