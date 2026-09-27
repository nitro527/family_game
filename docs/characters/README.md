# 우리 가족 파티 — 캐릭터 에셋 가이드

가족 4명을 베이스로 한 **드래곤 퀘스트 풍 판타지 캐릭터** 에셋 세트.
용도: 추후 RPG 게임 · 카드 게임 (그 외 굿즈/스티커 등 확장 가능)

| 캐릭터 | 직업 | 역할 | 설정서 |
|---|---|---|---|
| 막내 (아들, 3세) | 용사 | 파티 리더 · 밸런스형 | [boy.md](boy.md) |
| 아빠 | 전사 | 탱커 · 물리 딜러 | [dad.md](dad.md) |
| 엄마 | 승려 | 힐러 · 서포터 | [mom.md](mom.md) |
| 첫째 (딸, 5세) | 마법사 | 마법 딜러 | [girl.md](girl.md) |

레퍼런스 사진: [`reference/`](reference/) — 각자 정면 얼굴이 잘 나온 컷 (원본: `사진/` 020, 057번)

---

## 공통 아트 디렉션

- **화풍**: 90년대 일본 판타지 JRPG 공식 캐릭터 일러스트 (드래곤 퀘스트 풍)
  - 굵고 깔끔한 잉크 선, 밝은 단색 + 1단 셀 음영
  - 표정이 풍부하고 살짝 과장된 비율 (어른 약 5등신, 아이 약 3등신)
  - 머리카락은 덩어리감 있게, 눈은 단순하지만 인물 특징은 확실하게
- **가족 공통 색**: 사진 속 가족이 맞춰 입은 **라벤더 블루**를 파티의 테마색으로 모든 의상에 넣는다
- **닮음 우선순위**: 헤어스타일 > 안경·액세서리 > 얼굴형 > 눈매 > 표정
  (게임 캐릭터로 단순화해도 가족이 보고 "누군지" 바로 알아야 함)

## 공통 스타일 문구 (모든 프롬프트 끝에 붙어 있음)

```
Art style: official character illustration of a classic 1990s Japanese fantasy JRPG (Dragon Quest-like),
bold clean black ink linework, flat bright colors with simple cel shading, slightly stylized proportions,
friendly and heroic mood. Plain white background, full color, no text, no watermark-like logos.
```

> "Dragon Quest-like" 문구를 넣었을 때 Gemini가 거절하거나 결과가 이상하면
> `(Dragon Quest-like)` 부분만 지우고 다시 시도하세요. 나머지 문구만으로도 같은 방향이 나옵니다.

---

## Gemini 작업 순서 (무료/Basic: 하루 약 20장 기준)

**0. 준비**
- 캐릭터마다 **Gemini 대화창을 따로** 연다 (얼굴 일관성 유지에 가장 중요)
- 이미지 모델이 켜져 있는지 확인 (도구 → 이미지 만들기 / Nano Banana)

**1단계 · 메인 일러스트 확정 (1~2일차)**
1. `reference/xxx_ref.jpg` 업로드 + 설정서의 **① 메인 일러스트** 프롬프트
2. 마음에 들 때까지 "다시 생성" 또는 수정 요청 (설정서의 수정 문구 활용)
3. 가장 닮은 1장을 골라 `assets/characters/gen/<id>/<id>_main.png` 로 저장

**2단계 · 파생 에셋 (3일차~)** — 반드시 **확정된 메인 일러스트를 업로드**해서 생성
- ② 턴어라운드 (정면/측면/뒷면)
- ③ 표정 시트
- ④ 카드 일러스트
- ⑤ 얼굴 아이콘

**3단계 · 후처리 (Claude)**
뽑은 이미지를 `assets/characters/gen/<id>/` 에 넣어주면:
배경 제거 → 크기 규격화 → 카드/RPG 규격 에셋 정리 → 도트 스프라이트 재제작

## 저장 규칙 (2026-09-27 정리)

```
assets/characters/
  gen/<who>/            who = girl · boy · mom · dad
    <who>_main.png         확정 메인 그림
    <who>_expressions.png  표정 시트
    <who>_battle.png       전투 포즈 시트
    <who>_walk.png         걷기 시트
    <who>_portrait.png     초상화
    <who>_card.png         카드 그림
    <who>_turnaround.png   앞·옆·뒤 (Gemini)
    <who>_main_transparent.png, <who>_walk_side.gif, icons/, poses/  — 시트를 잘라 자동 생성 (tools/build_character_set.py)
    cards/<who>_card_unit.png + 필살기 카드  — tools/card_mockup.py (숫자는 카드 규칙 정할 때까지 임시)
    (girl 만) girl_card_ultimate.png — 필살기 카드용 별도 그림
  gen/family/            family_title.png(타이틀 키아트), family_party.png(파티 단체)
  overviews/<who>.jpg, family.jpg   한 장 요약 이미지 — python tools/build_character_set.py (자르기+요약 한 번에)
```
- 파일 이름은 `<who>_<종류>.png` 로만. **확정본만 남기고** 버린 시안(_v1, _v2 등)은 지워요 (필요하면 git 기록에 있음).
- 다시 뽑으면 같은 이름으로 덮어써요.

## 하루 20장 예산 예시

| 일차 | 작업 | 장수 |
|---|---|---|
| 1일차 | 메인 일러스트 · 아빠 5 / 엄마 5 / 첫째 5 / 막내 5 | 20 |
| 2일차 | 마음에 안 드는 캐릭터 재시도 + 턴어라운드 | ~20 |
| 3일차 | 표정 시트 4 + 카드 일러스트 8 + 아이콘 4 | ~16 |
