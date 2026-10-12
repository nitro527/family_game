using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GirlAdventure.View
{
    /// <summary>
    /// 애니메이션 영상 (2026-10-13 사용자 "게임 시작 화면에 넣자, 큰 이벤트에 짧은 애니메이션"):
    /// StreamingAssets/Movies/&lt;이름&gt;.mp4 — intro(게임을 켜면), join_bro·join_dad·join_mom(가족 합류), clear_ch1~7(챕터 완료).
    /// 영상은 tools/opening_gen.py·tools/event_movies.py 가 만든다 (Qwen 원화 → Wan 애니메이션 → 자막·음악 편집).
    /// 엔터·스페이스·Esc·클릭으로 건너뛴다. 파일이 없거나 배치 모드(테스트)면 그냥 지나간다.
    /// </summary>
    public partial class GameRoot
    {
        GameObject moviePanel;
        RawImage movieImg;
        VideoPlayer movie;
        RenderTexture movieRT;

        /// <summary>지금 트는 영상 이름 (프로브용, 없으면 null).</summary>
        public string MoviePlaying { get; private set; }

        public static string MoviePath(string name) => Path.Combine(Application.streamingAssetsPath, "Movies", name + ".mp4");
        public static bool HasMovie(string name) => File.Exists(MoviePath(name));
        /// <summary>이 영상을 실제로 틀 수 있나 (배치 모드 테스트에선 영상 대신 예전 컷인).</summary>
        static bool CanPlayMovie(string name) => !Application.isBatchMode && HasMovie(name);

        void BuildMovie()
        {
            moviePanel = UIKit.Fill(ui, "Movie", Color.black).gameObject;
            movieImg = UIKit.Stretch(moviePanel.transform, "Video").gameObject.AddComponent<RawImage>();
            movieImg.color = Color.white;
            var hint = UIKit.Label(moviePanel.transform, "Skip", "엔터: 건너뛰기", 780, 506, 170, 26, 16, TextAnchor.MiddleRight, new Color(1, 1, 1, .55f), false);
            hint.raycastTarget = false;
            moviePanel.SetActive(false);
        }

        /// <summary>영상 하나를 끝까지(또는 건너뛸 때까지) 튼다. 트는 동안 배경음악은 멈춘다.</summary>
        IEnumerator MovieRoutine(string name)
        {
            if (!CanPlayMovie(name)) yield break;
            if (movie == null)
            {
                movie = gameObject.AddComponent<VideoPlayer>();
                movie.playOnAwake = false;
                movie.isLooping = false;
                movie.renderMode = VideoRenderMode.RenderTexture;
                movie.audioOutputMode = VideoAudioOutputMode.Direct;
                movie.skipOnDrop = true;
                movieRT = new RenderTexture(1280, 720, 0);
                movie.targetTexture = movieRT;
                movieImg.texture = movieRT;
            }
            string keepSong = bgm.Current;
            bgm.Stop();
            MoviePlaying = name;
            movie.url = MoviePath(name);
            movie.SetDirectAudioVolume(0, bgm.LevelPercent / 100f);
            moviePanel.transform.SetAsLastSibling();
            moviePanel.SetActive(true);
            movieImg.color = new Color(1, 1, 1, 0);                // 준비될 때까지 검은 화면
            movie.Prepare();
            for (float t = 0; !movie.isPrepared && t < 6; t += Time.unscaledDeltaTime) yield return null;
            if (movie.isPrepared)
            {
                movie.Play();
                movieImg.color = Color.white;
                float len = (float)movie.length, t = 0;
                while (t < len + 1f)
                {
                    t += Time.unscaledDeltaTime;
                    if (t > .4f && (Keys.Down(KeyCode.Return) || Keys.Down(KeyCode.Space) || Keys.Down(KeyCode.Escape) || Keys.Down(KeyCode.Z) || Input.GetMouseButtonDown(0))) break;
                    if (t > 1f && !movie.isPlaying) break;
                    yield return null;
                }
                movie.Stop();
            }
            moviePanel.SetActive(false);
            MoviePlaying = null;
            if (!string.IsNullOrEmpty(keepSong)) bgm.Play(keepSong);
            confirmFrame = Time.frameCount;                       // 건너뛴 엔터가 다음 메뉴를 누르지 않게
        }

        /// <summary>게임을 켜면: 오프닝 영상 → 타이틀.</summary>
        void Boot()
        {
            if (CanPlayMovie("intro")) StartCoroutine(BootRoutine());
            else ShowTitle();                                       // 영상이 없거나 테스트면 바로 타이틀 (같은 프레임에)
        }

        IEnumerator BootRoutine()
        {
            Current = Mode.Title;
            titlePanel.SetActive(false);
            cam.transform.position = new Vector3(-100, 0, -10);
            yield return MovieRoutine("intro");
            ShowTitle();
        }
    }
}
