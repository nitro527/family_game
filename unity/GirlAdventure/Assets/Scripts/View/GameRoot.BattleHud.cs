using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>전투: 적 HP 막대·무리 이름표 (2026-10-11 전투 연출 정리: GameRoot.Battle 에서 나눔).</summary>
    public partial class GameRoot
    {
        void BuildEnemyHp()
        {
            enemyHpBar = UIKit.Bar(enemyLabel.transform, "EnemyHP", 96, 42, 150, 10, new Color32(86, 212, 106, 255));
            var frame = enemyHpBar.transform.parent.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0, 0, 0, .9f);
            frame.effectDistance = new Vector2(2, -2);
            enemyHpText = UIKit.Label(enemyLabel.transform, "EnemyHPText", "", 254, 35, 120, 24, 16, TextAnchor.MiddleLeft, Color.white, true);
            // 밝은 배경(눈밭·사막)에서도 읽히게 이름·HP 글자 테두리를 두껍게
            foreach (var t in new[] { enemyLabel, enemyHpText }) t.GetComponent<TextRim>().effectDistance = new Vector2(2f, 2f);
            enemyTagGroup = enemyLabel.gameObject.AddComponent<CanvasGroup>();
            enemyTagGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 막대는 실제 HP 를 향해 천천히 줄어든다 (맞은 게 눈에 보이게). 절반 아래 노랑, 1/4 아래 빨강.
        /// 적이 공격 모션·등장 중일 때는 이름표째 흐려져서 몸과 겹치지 않는다.
        /// </summary>
        void TickEnemyHp()
        {
            bool on = Current == Mode.Battle && battle != null && enemyLabel.text != "";
            var bg = enemyHpBar.transform.parent.gameObject;
            if (bg.activeSelf != on) { bg.SetActive(on); enemyHpText.gameObject.SetActive(on); }
            if (!on) return;
            float target = Mathf.Clamp01(battle.EnemyHp / (float)Mathf.Max(1, battle.Def.Hp));
            enemyHpShown = Mathf.MoveTowards(enemyHpShown, target, Time.deltaTime * .9f);
            enemyHpBar.fillAmount = enemyHpShown;
            enemyHpBar.color = enemyHpShown > .5f ? new Color32(86, 212, 106, 255) : enemyHpShown > .25f ? new Color32(242, 196, 67, 255) : new Color32(255, 96, 96, 255);
            enemyHpText.text = $"HP {Mathf.Min(battle.EnemyHp, battle.Def.Hp)}/{battle.Def.Hp}";
            bool hide = moveP < 1 || enemyAppear < 1;
            enemyTagGroup.alpha = Mathf.MoveTowards(enemyTagGroup.alpha, hide ? 0 : 1, Time.deltaTime * 6);
        }

        // ---- 무리 전투: 적마다 작은 이름표 + 체력 막대 (머리 위를 따라다님), 대상 고를 때 ▼

        void BuildGroupTags()
        {
            for (int i = 0; i < groupTags.Length; i++)
            {
                groupTags[i] = UIKit.Label(ui, "FoeTag" + i, "", 0, 0, 120, 26, 18, TextAnchor.MiddleCenter, Color.white, true);
                groupTags[i].GetComponent<TextRim>().effectDistance = new Vector2(2.2f, 2.2f);       // 밝은 배경에서도 잘 보이게
                groupBars[i] = UIKit.Bar(groupTags[i].transform, "Hp", 22, 26, 76, 7, new Color32(86, 212, 106, 255));
                groupGroups[i] = groupTags[i].gameObject.AddComponent<CanvasGroup>();
                groupGroups[i].blocksRaycasts = false;
            }
            targetArrow = UIKit.Label(ui, "TargetArrow", "▼", 0, 0, 60, 40, 34, TextAnchor.MiddleCenter, UIKit.Gold, true);
            targetArrow.gameObject.SetActive(false);
        }

        /// <summary>적 i 의 머리 꼭대기 y (무대 px = 캔버스 기준 해상도 960×540 과 같은 좌표).</summary>
        float FoeHeadY(int i) => EnemyY - HeadUnits(battle.Foes[i].Def) * FoeScale(i) * 48;

        void TickGroupTags()
        {
            bool on = Current == Mode.Battle && GroupBattle && battleRoot.gameObject.activeSelf;
            for (int i = 0; i < groupTags.Length; i++)
            {
                bool show = on && i < battle.Foes.Count && groupTags[i].text != "";
                if (groupTags[i].gameObject.activeSelf != show) groupTags[i].gameObject.SetActive(show);
                if (!show) continue;
                var f = battle.Foes[i];
                groupTags[i].rectTransform.anchoredPosition = new Vector2(FoeX(i) - 60, -(FoeHeadY(i) - 44));     // 머리 위, 기준점 왼쪽 위
                groupShown[i] = Mathf.MoveTowards(groupShown[i], Mathf.Clamp01(f.Hp / (float)Mathf.Max(1, f.Def.Hp)), Time.deltaTime * .9f);
                groupBars[i].fillAmount = groupShown[i];
                groupBars[i].color = groupShown[i] > .5f ? new Color32(86, 212, 106, 255) : groupShown[i] > .25f ? new Color32(242, 196, 67, 255) : new Color32(255, 96, 96, 255);
                groupTags[i].color = i == targetMark ? (Color)UIKit.Gold : Color.white;
                bool hide = (moveP < 1 && i == movingFoe) || enemyAppear < 1;
                groupGroups[i].alpha = Mathf.MoveTowards(groupGroups[i].alpha, hide ? 0 : 1, Time.deltaTime * 6);
            }
            bool arrow = on && targetMark >= 0;
            if (targetArrow.gameObject.activeSelf != arrow) targetArrow.gameObject.SetActive(arrow);
            if (arrow)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 6)) * 6;
                targetArrow.rectTransform.anchoredPosition = new Vector2(FoeX(targetMark) - 30, -(FoeHeadY(targetMark) - 86 - bob));
            }
        }
    }
}
