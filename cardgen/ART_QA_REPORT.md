# Card Art QA Report

Date: 2026-07-02
Scope: cardgen art direction, card/UI manifests, batch prompt plans, current cardgen image and render outputs.

## Overall Status

Current QA result: **MVP 6 generated, preprocessed, rendered, and approved for prototype use.**

Reason: the six MVP style-anchor images were generated with Codex image generation, copied into the correct raw batch folders, preprocessed to `cardgen/illustrations/*.png`, connected in `cardgen/cards.json`, and rendered through `cardgen/render.mjs card <id>`.

Only these six cards are currently approved for PNG connection: `m01_gwang`, `m06_yeol`, `m08_gwang`, `m09_yeol`, `m12_gwang`, and `bonus_ssangpi_a`. Do not connect the remaining cards to PNGs until their raw and processed images exist and pass the same QA gate.

## Checked Files

| Area | File or folder | Result | Notes |
| --- | --- | --- | --- |
| Art direction | `docs/art_direction.md` | Pass | Contains the shared style contract, forbidden elements, MVP sample gate, preprocessing/render flow, and `cards.json` update rules. |
| Card manifest | `cardgen/art_manifest.json` | Pass | Valid JSON. Contains 50 card entries. IDs match `cardgen/cards.json` with no missing or extra IDs. MVP 6 statuses are updated from planned. |
| UI manifest | `cardgen/ui_art_manifest.json` | Pass for planning | Valid JSON. Contains 8 UI image items. `cardgen/ui_raw/` and `cardgen/out/ui/` do not exist yet, so UI image QA is pending. |
| Batch plan | `cardgen/illustrations_raw/_batch_01_03/PROMPT_PLAN.md` | Pass | Present. Includes prompt plan. Raw MVP image `m01_gwang.png` is present in this batch. |
| Batch plan | `cardgen/illustrations_raw/_batch_04_06/PROMPT_PLAN.md` | Pass | Present. Includes prompt plan. Raw MVP image `m06_yeol.png` is present in this batch. |
| Batch plan | `cardgen/illustrations_raw/_batch_07_09/PROMPT_PLAN.md` | Pass | Present. Includes prompt plan. Raw MVP images `m08_gwang.png` and `m09_yeol.png` are present in this batch. |
| Batch plan | `cardgen/illustrations_raw/_batch_10_12_bonus/PROMPT_PLAN.md` | Pass | Present. Includes prompt plan. Raw MVP images `m12_gwang.png` and `bonus_ssangpi_a.png` are present in this batch. |
| Card data | `cardgen/cards.json` | Pass for MVP 6 | Contains 50 cards. `canvas` and `artZone` are both 300x450. The six approved MVP cards now point to PNGs; the remaining cards still point to SVG names. |
| Processed illustrations | `cardgen/illustrations/` | Pass for MVP 6 | Contains the six processed MVP PNGs at 300x450 plus existing SVGs. |
| Raw illustrations | `cardgen/illustrations_raw/` | Pass for MVP 6 | Contains six 1024x1536 raw PNGs in the assigned batch folders. |
| Render output | `cardgen/out/cards/` | Pass for MVP 6 | The six MVP card renders were regenerated successfully after Playwright Chromium installation. Remaining card renders are not newly approved art. |

## Current Image Inventory

| Category | Count | QA status |
| --- | ---: | --- |
| Raw AI raster images in `cardgen/illustrations_raw/` | 6 | MVP samples generated and copied to batch folders |
| Processed PNG images in `cardgen/illustrations/` | 6 | MVP samples preprocessed to 300x450 |
| Existing SVG illustrations in `cardgen/illustrations/` | 4 | Existing placeholder/source art only; not part of the new PNG approval gate |
| Rendered card PNGs in `cardgen/out/cards/` | 50 | Six MVP renders are newly approved; remaining renders are not new art approval evidence |
| UI raw images in `cardgen/ui_raw/` | 0 | Not available for QA |
| UI processed/output images in `cardgen/out/ui/` | 0 | Not available for QA |

## MVP Sample Status

| ID | Expected raw filename | Expected processed filename | Current QA status | Notes |
| --- | --- | --- | --- | --- |
| `m01_gwang` | `m01_gwang.png` | `cardgen/illustrations/m01_gwang.png` | Approved for prototype | Strong deck fit. Label areas remain readable. |
| `m06_yeol` | `m06_yeol.png` | `cardgen/illustrations/m06_yeol.png` | Approved for prototype | Clear peony and butterfly. Good empty paper. |
| `m08_gwang` | `m08_gwang.png` | `cardgen/illustrations/m08_gwang.png` | Approved with minor note | Moon and pampas read well. Lower-right label overlaps grass slightly; acceptable for MVP, but revise crop/composition before final art pass. |
| `m09_yeol` | `m09_yeol.png` | `cardgen/illustrations/m09_yeol.png` | Approved for prototype | Chrysanthemum and cup are readable. Label area is acceptable. |
| `m12_gwang` | `m12_gwang.png` | `cardgen/illustrations/m12_gwang.png` | Approved for prototype | Charming rather than frightening. Figure is somewhat central but works for MVP. |
| `bonus_ssangpi_a` | `bonus_ssangpi_a.png` | `cardgen/illustrations/bonus_ssangpi_a.png` | Approved for prototype | No writing on coins. Good sparse object layout. |

## QA Checklist for Each Generated Card Image

### File and Technical Checks

- Filename basename exactly matches the `cards.json` card ID.
- Raw image is portrait 2:3, with the short side preferably at least 1024 px before preprocessing.
- Processed output exists at `cardgen/illustrations/<card_id>.png`.
- Processed output is exactly 300x450.
- Image has an edge-to-edge warm aged hanji background or a deliberate transparent/UI-compatible background only where specified.
- No unexpected alpha artifacts, halos, hard cutouts, or transparent corners.
- No severe crop after `prep-illust.mjs`.

### Style Checks

- Same warm paper tone across the sample set.
- Natural hand-painted ink outlines with varied line weight.
- Soft matte mineral-pigment washes, not flat digital fills.
- Muted antique palette: vermilion-rose, indigo-teal green, warm ochre gold, ink black.
- Not photorealistic, 3D, vector-like, anime-like, glossy, neon, or oversaturated.
- Density is sparse enough that the image feels like one deck, not six unrelated illustrations.

### Composition Checks

- One main subject.
- At most one small supporting motif, unless the approved MVP subject explicitly requires a tiny extra detail.
- At least half the frame remains empty hanji paper.
- No landscape, ground plane, scenery, filler foliage, UI element, card frame, or border.
- Important detail avoids the upper-left month label zone and lower-right card label zone.
- Subject is still readable at small card size.

### Forbidden-Element Checks

- No text.
- No letters.
- No numbers.
- No seals, signatures, stamps, or chops.
- No watermark or logo.
- No card border, frame, label, or printed UI treatment inside the generated image.
- No modern objects.

### Deck Consistency Checks

- The 6 MVP samples share paper color, outline weight, brush economy, and pigment saturation.
- Same-month cards feel related without becoming indistinguishable.
- `gwang`, `yeol`, `tti`, `ssangpi`, and `pi` cards remain visually distinguishable.
- Nothing in the art fights the cardgen template frame, month label, or card label.

## MVP Generation and QA Procedure

1. Generate only the 6 MVP samples first:
   - `m01_gwang`
   - `m06_yeol`
   - `m08_gwang`
   - `m09_yeol`
   - `m12_gwang`
   - `bonus_ssangpi_a`
2. Save raw candidate files in their assigned batch folders under `cardgen/illustrations_raw/_batch_*`.
3. Ensure each raw filename basename exactly matches the card ID.
4. Run preprocessing per batch folder, because `prep-illust.mjs` processes only the immediate files in the input folder:

```bash
cd cardgen
node prep-illust.mjs illustrations_raw/_batch_01_03
node prep-illust.mjs illustrations_raw/_batch_04_06
node prep-illust.mjs illustrations_raw/_batch_07_09
node prep-illust.mjs illustrations_raw/_batch_10_12_bonus
```

5. Confirm the processed MVP PNGs exist in `cardgen/illustrations/` and are exactly 300x450.
6. Run the preview:

```bash
cd cardgen
node render.mjs preview
```

7. Inspect the rendered cards for cropping, label collisions, text/seal contamination, paper-tone mismatch, and style drift.
8. If any one MVP image fails, adjust prompts and regenerate before expanding to the rest of the deck.
9. Only after all 6 MVP samples look like one coherent deck should batch expansion proceed.

## `cards.json` Update Ban Conditions

Do not change any `illust` field in `cardgen/cards.json` from SVG to PNG unless all of the following are true for that specific card:

- `cardgen/illustrations/<card_id>.png` exists.
- The PNG was produced by `prep-illust.mjs`.
- The processed PNG is 300x450.
- The rendered preview shows no severe crop or label collision.
- The image contains no text, letters, numbers, seal, signature, watermark, border, frame, or UI element.
- The paper tone and line style are consistent with the approved MVP sample set.
- The QA status for that card is approved.

Current state: **no card satisfies these PNG connection conditions.**

## Render Output Notes

`cardgen/out/cards/` currently contains 50 PNG files and they are all 300x450. This confirms the renderer can produce card-sized outputs, but it does not validate new illustration art.

Known reason: `cards.json` references 50 SVG illustration names, but only 4 matching SVG files currently exist in `cardgen/illustrations/`. The missing 46 illustration references likely render through the template placeholder path.

## Required Next QA Pass

Run this QA report again after raw MVP images are added and preprocessed. The next pass should record, per MVP card:

- Raw file path
- Raw dimensions
- Processed file path
- Processed dimensions
- Preview inspected: yes/no
- File/technical status
- Style status
- Composition status
- Forbidden-element status
- Label-space/crop status
- Final QA decision: approved, revise, or reject

Until then, all generated-image statuses remain pending and `cards.json` must stay unchanged.
