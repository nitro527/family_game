# 첫째 (딸) — 마법사 (Mage)

> 리본을 휘날리며 주문을 외우는 꼬마 마법사. 작지만 파티 최강의 화력.

레퍼런스: [`reference/girl_ref.jpg`](reference/girl_ref.jpg)

## 닮게 만들 핵심 특징
| 부위 | 특징 (반드시 유지) |
|---|---|
| 헤어 | **일자 앞머리**, **낮게 묶은 양갈래**, 검은 머리 |
| 액세서리 | **파란 데님 리본 머리띠** (윗부분에 토끼귀 같은 매듭) — 가장 중요한 식별 포인트 |
| 얼굴 | 동그란 얼굴, 발그레한 볼 |
| 눈·입 | 동그랗고 까만 눈, 작고 붉은 입술, 살짝 새침한 미소 |
| 나이감 | 5살 정도의 아이 (약 3등신) |

## 의상 & 소품
- 라벤더 원피스 + **큰 흰색 레이스 카라** (사진 속 원피스 그대로)
- 짧은 진보라 마법사 망토 (별 모양 금색 브로치)
- 핑크 구두
- 무기: 끝에 분홍 크리스탈/별이 달린 작은 마법 지팡이
- 모자는 쓰지 않음 → 리본 머리띠가 보여야 함

## 컬러 팔레트
| 용도 | 색 |
|---|---|
| 머리 | `#1E1A24` |
| 리본 | `#5B8EE0` |
| 원피스 | `#B8C2F0` |
| 카라 | `#FFFFFF` |
| 망토 | `#4A3F8F` |
| 지팡이 크리스탈 | `#FF7A8A` |

## 게임 설정 (초안)
- **RPG**: HP 낮음 · MP 최고 · 마법 공격 최고
- **스킬 콘셉트**: `리본 파이어`(단일 화염) · `별빛 폭풍`(전체 공격) · `새침한 눈빛`(적 공격력 감소)
- **카드 게임**: 스펠 카드 — 광역 데미지

## ✅ 확보된 에셋 (`assets/characters/gen/girl/`)
| 파일 | 내용 | 비고 |
|---|---|---|
| `girl_main.png` | 메인 일러스트 (흰 배경) | 딸 본인 선택 |
| `girl_main_transparent.png` | 메인 일러스트 투명 PNG | `tools/postprocess.py cutbg` |
| `girl_expressions.png` | 표정 시트 6종 | 미소·웃음·삐짐·놀람·울음·주문 |
| `icons/girl_01~06_*.png` | 얼굴 아이콘 512px ×6 | 표정 시트에서 자동 분할 |
| `girl_card.png` | 카드 일러스트 (밤하늘·성·별 마법) | 우하단 Gemini ✦ 워터마크 → 카드 프레임으로 가릴 것 |
| `girl_turnaround.png` | 정면·측면·뒷면 | 확정본보다 약간 길쭉함, 참고용 |
| `girl_battle.png` → `poses/girl_battle_{ready,cast,damage,victory}.png` | 전투 포즈 4종 (투명 PNG) | 대기·불꽃 주문·피격·승리 |
| `girl_walk.png` → `poses/girl_walk_{side1-4,front,back}.png` | 걷기 6프레임 (투명 PNG, 발 기준선 정렬) | 측면 4프레임 사이클 + 정면/뒷면 |
| `girl_walk_side.gif` | 측면 걷기 애니메이션 미리보기 | 160ms × 4프레임 |
| `girl_portrait.png` | 대화창용 상반신 초상화 (1024²) | 손 흔들며 미소 |
| `girl_card_ultimate.png` | 필살기 카드 일러스트 "별빛 폭풍" | 보름달·마법진·별 폭풍 |
| `cards/girl_card_unit.png`, `cards/girl_card_starlight.png` | 카드 게임 목업 (750×1050, 투명 모서리) | `tools/card_mockup.py` — 스탯/문구는 임시 |
| `girl_assets_overview.jpg` | 전체 에셋 한 장 요약 | |

> SD 재시도 아이디어: 확정본을 올리지 말고 표정 시트의 얼굴 아이콘을 올려 "chibi 2-head-tall mascot"로 요청하거나, 먼저 텍스트로만 2등신을 만들고 두 번째 턴에 확정본을 참고시키기.
> 파생 에셋 요령: **새 대화 + 확정본 1장만 업로드** + "copy the original exactly: (특징 나열)" 로 요청해야 그림체가 안 흔들림.

---

## Gemini 프롬프트

### ① 메인 일러스트 (레퍼런스 사진 업로드 + 아래 입력) — ✅ 확정: `assets/characters/gen/girl/girl_main.png` (딸 본인 선택)
```
Turn the little girl in this photo into a cute fantasy RPG character: a young MAGE (about 5 years old).
Art style: Dragon Quest official character art / classic Japanese manga style, anime-style face with big simple
expressive eyes, large head and compact child proportions (about 3 heads tall), smooth confident black ink lines,
clean flat cel shading, bright colors, no western comic-book hatching.
IMPORTANT: keep her real facial features so the family can recognize her: very round face with full soft cheeks
and rosy blush, straight blunt bangs cut just above the eyebrows, black hair in two low pigtails near the ears,
a blue denim fabric bow headband with the knot on top of her head, round dark eyes, small red lips with a slightly
shy confident smile.
Outfit: lavender dress with a large white eyelet-lace collar, short deep-purple mage cape with a gold star brooch,
pink shoes. She holds a small magic wand topped with a pink star crystal. No hat, so the bow headband stays visible.
Full body, cheerful spell-casting pose, three-quarter view. Plain white background, full color, no text.
```
> 참고: 눈매를 실사에 가깝게(아몬드형·애교살) 바꾼 시안도 뽑아봤지만(삭제됨), 만화 눈이 큰 버전이 선택됨.
> → 아이 캐릭터는 **크고 동그란 만화 눈** 방향이 기준.

### ② 턴어라운드 (확정된 메인 일러스트 업로드)
```
Create a character turnaround sheet of this exact same character: front view, side view, and back view,
standing side by side in a neutral pose, same outfit and colors, same art style.
Plain white background, no text.
```

### ③ 표정 시트 (확정된 메인 일러스트 업로드)
```
Create an expression sheet of this exact same character: 6 head-and-shoulders portraits in a 3x2 grid —
shy smile, big happy grin, pouting, surprised, crying, determined spell-casting face.
Same face, pigtails, bow headband and art style. Plain white background, no text.
```

### ④ 카드 일러스트 (확정된 메인 일러스트 업로드)
```
Draw this exact same character casting a powerful magic spell with her wand,
swirling stars and pink-purple magic energy, pigtails and cape blowing in the wind.
Magical night sky background. Vertical 2:3 aspect ratio card illustration, same art style, no text.
```

### ⑤ 얼굴 아이콘 (확정된 메인 일러스트 업로드)
```
Draw a bust portrait of this exact same character, facing forward with a shy smile,
centered for a square RPG status icon. Same art style, plain white background, no text.
```

### 수정 요청 문구 (닮지 않았을 때)
- `Make the bangs straight and blunt, cut just above the eyebrows.`
- `Tie the pigtails lower, near the ears, like in the photo.`
- `Make the headband a blue denim fabric bow with the knot on top of the head.`

> SD 2등신 버전은 아직 없음 (첫 시도 실패, 재시도 필요).
