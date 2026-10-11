using System.Collections;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 필드 장면의 공통 흐름 (2026-10-11 코드 정리 — 퀘스트·이벤트·보물상자·항아리·나무 선물마다 반복되던 것을 하나로):
    /// 다른 장면 중이면 건너뛰기 → busy 켜기 → 본문(대사·연출·보상) → 대화창 닫기 → (저장) → busy 끄기.
    /// 보상 연출은 RewardSparkle (금빛 별 열 개가 퐁).
    /// </summary>
    public partial class GameRoot
    {
        /// <summary>장면 하나를 감싼다. skipIfBusy = 다른 장면 중이면 아무것도 안 함, save = 끝나고 자동 저장.</summary>
        IEnumerator Scene(IEnumerator body, bool save = false, bool skipIfBusy = false)
        {
            if (skipIfBusy && busy) yield break;
            busy = true;
            yield return body;
            HideMessage();
            if (save) AutoSave();
            busy = false;
        }

        /// <summary>보상 반짝: at 위로 금빛 별 열 개가 퐁 (보물상자·구멍 사탕 등).</summary>
        void RewardSparkle(Vector3 at)
        {
            for (int i = 0; i < 10; i++)
                SpawnField(sparkSprite, at, new Vector3(Random.Range(-1f, 1f), Random.Range(.8f, 2f)), .9f, Random.Range(.2f, .35f), UIKit.Gold, Random.Range(-120, 120));
        }

        /// <summary>칸 (x, y) 위의 보상 반짝.</summary>
        void RewardSparkle(int x, int y) => RewardSparkle(FeetAt(x, y) + new Vector3(0, .6f, 0));
    }
}
