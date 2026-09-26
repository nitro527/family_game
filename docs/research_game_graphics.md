# AI(Claude Code)로 만든 게임들은 그래픽을 어떻게 만드나 — 2026 조사

## 대표 사례: Vibe Jam 2026 우승작 "카피바라 음식 배달" (상금 $25,000)
- iOS 개발자 1명이 **15일** 동안 만든 Three.js 브라우저 3D 게임이에요. 코드 27,000줄, 커밋 188개를 전부 AI가 작성했어요.
- **그래픽과 사운드도 모두 AI 도구로 만들었어요.**
  | 역할 | 도구 |
  |---|---|
  | 코드 전체 | Claude Code |
  | 3D 렌더링 | Three.js (브라우저) |
  | **3D 모델** | **Tripo3D** (이미지·텍스트 → 3D 모델) |
  | 텍스처, 로고, 일러스트 | GPT Image 2, Grok |
  | 음악 | Suno |
  | 음성, 효과음 | ElevenLabs |
- 사람이 한 일: 각 도구에 넣을 프롬프트 작성, AI가 못 채운 빈틈 메우기, 미세 조정.
- **가장 손이 많이 간 곳은 맵**이었어요. AI가 만든 3D 도시는 가까이 가면 디테일이 부족하고 성능도 나빠서 직접 손봤다고 해요.

## 업계에서 자리 잡은 방식 (2026)
1. **Claude Code + 게임 엔진 연결**
   - **Unity가 Claude Code 공식 플러그인을 출시**했어요. Unity 기능(스킬) 29종이 들어 있고, 명령 한 줄로 설치돼요.
   - 커뮤니티 Unity MCP(CoplayDev, IvanMurzak 등)도 씬, 에셋, 테스트를 AI가 직접 조작하게 해 줘요.
2. **Blender MCP**: Claude가 Blender를 조종해서 3D 에셋을 만들고 GLTF나 FBX로 내보내 Unity에 넣는 파이프라인이에요.
3. **이미지 → 3D 생성** (Tripo3D, Meshy 등): 캐릭터 일러스트를 3D 모델로 바꿔요.
4. **코드로 만든 그래픽 + 무료 에셋 팩**: 도형, 셰이더, 절차적 생성에 Kenney나 Quaternius 같은 무료 에셋을 더하는 방식이에요.

## 우리 프로젝트와 비교
- 구조가 이미 같아요: 코드는 Claude, 그림은 이미지 AI(우리는 Gemini), 효과음은 코드로 합성.
- 바로 도입할 만한 것:
  - **Unity 공식 Claude Code 플러그인**: 지금은 배치 모드 스크립트로 검증하는데, 플러그인을 쓰면 에디터를 직접 조작할 수 있어요.
  - **Tripo3D**: 무료 크레딧으로 딸 캐릭터 일러스트를 3D로 바꿔 보는 실험
  - **Suno** 무료 플랜: 게임 배경음악(BGM). 지금 BGM이 없어요.

출처: [wccftech](https://wccftech.com/ios-developer-made-capybara-game-using-vibe-coding-and-won-25000/), [MacObserver](https://www.macobserver.com/news/ios-developer-wins-25000-after-building-entire-game-with-claude-code-in-two-weeks/), [explainx.ai — Claude Code + Unity MCP](https://www.explainx.ai/blog/claude-code-unity-mcp-guide-2026), [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), [MCP.Directory — Godot vs Unity vs Blender MCP](https://mcp.directory/blog/godot-vs-unity-vs-blender-mcp-skills-2026)
