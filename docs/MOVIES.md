# 애니메이션 영상 (2026-10-13)

사용자 요청: "서진이를 주인공으로 가족과 함께라면 뭐든지 이겨낼 수 있다는 1분 애니메이션" → "게임 시작 화면에 넣자,
큰 이벤트에 3~10초 애니메이션, 이미지는 이미지 모델로" → "에셋을 그대로 넣지 말고 에셋을 참고로 한 진짜 애니메이션, 게임 오프닝처럼".
지훈이는 동생이라 "지켜 준다"가 아니라 "함께 힘을 모은다" (사용자).

## 게임 안
- `StreamingAssets/Movies/<이름>.mp4` — `GameRoot.Movies.cs` 의 `MovieRoutine` 이 튼다 (엔터·스페이스·Esc·클릭으로 건너뛰기, 배치 모드·파일 없으면 지나감).
- `intro` = 게임을 켜면 (BootRoutine → 타이틀), `join_bro`·`join_dad`·`join_mom` = 가족 합류 (없으면 예전 컷인), `clear_ch1~7` = 챕터 완료 대사 뒤.
- SmokeTest `MovieTests` 가 파일이 다 있는지 본다.

## 만드는 법 (ComfyUI 127.0.0.1:8188 를 띄우고)
1. 오프닝 원화 13컷: `python tools/opening_gen.py keys` (Qwen, 가족 그림 hero.png 를 흰 16:9 참고 판으로) → `assets/movie/opening/keys`
2. 움직임: `python tools/opening_gen.py video` (Wan 2.2 14B, 832×480, 16fps, 컷당 약 2분 15초) → `assets/movie/opening/clips/<컷>/`
3. 편집: `C:/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/opening_gen.py edit` (자막·컷 겹치기·제목·음악) → `assets/movie/opening.mp4` + 게임 intro.mp4
   - 미리보기: `... python.exe tools/opening_edit.py --preview 3,10,20`
4. 이벤트 10개: `python tools/event_movies.py keys` → `video` → (`python_embeded`) `edit`
- 컷 하나만 다시: 이름을 뒤에 (예: `python tools/opening_gen.py keys s07_jelly`). s07 킹 젤리는 참고 판 없이 그린 것이 사용자 마음에 듦.
- 처음 만든 모션 그래픽 버전(게임 그림을 그대로 붙인 것): `tools/family_movie.py` → `assets/movie/seojin_family.mp4` (배경 그림 `tools/event_art_gen.py`). 편집 도구들이 이 파일의 글자·음악 함수를 같이 쓴다.
