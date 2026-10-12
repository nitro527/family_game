using System.IO;
using GirlAdventure.Sim;
using GirlAdventure.View;

namespace GirlAdventure.EditorTools
{
    /// <summary>애니메이션 영상 (2026-10-13): 오프닝·가족 합류·챕터 완료 영상이 StreamingAssets/Movies 에 있는지 (만들기: tools/opening_gen.py·tools/event_movies.py).</summary>
    public static partial class SmokeTest
    {
        static void MovieTests()
        {
            string missing = "";
            var names = new System.Collections.Generic.List<string> { "intro", "join_bro", "join_dad", "join_mom" };
            foreach (var c in Chapters.All) names.Add($"clear_ch{c.No}");
            foreach (var n in names)
                if (!GameRoot.HasMovie(n) || new FileInfo(GameRoot.MoviePath(n)).Length < 50000) missing += n + " ";
            Check($"movies ({names.Count}): intro, family joins, chapter clears", missing == "", missing);
        }
    }
}
