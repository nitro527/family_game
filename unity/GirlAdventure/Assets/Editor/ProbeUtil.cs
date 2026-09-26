using System.IO;
using UnityEngine;

namespace GirlAdventure.EditorTools
{
    public static class ProbeUtil
    {
        /// <summary>
        /// 배치 모드에는 게임 뷰가 없어 ScreenCapture 가 동작하지 않는다.
        /// 오버레이 캔버스를 잠시 카메라 캔버스로 바꿔 카메라 + UI 를 RenderTexture 에 그린 뒤 저장한다.
        /// </summary>
        public static void RenderToPng(string path, int w = 1280, int h = 720)
        {
            var cam = Camera.main;
            cam.aspect = w / (float)h;           // 배치 모드 화면 비율과 무관하게 16:9
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var rt = new RenderTexture(w, h, 24);
            var oldMode = canvas.renderMode;
            int oldOrder = canvas.sortingOrder;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 1000;          // 오버레이처럼 UI 가 항상 위에
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prev;
            cam.targetTexture = null;
            canvas.renderMode = oldMode;
            canvas.sortingOrder = oldOrder;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
