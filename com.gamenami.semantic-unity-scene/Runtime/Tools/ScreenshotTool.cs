using UnityEngine;
using System.Collections;
using System;

namespace Gamenami.SemanticUnityScene
{
    public class ScreenshotTool : HiddenSingleton<ScreenshotTool>
    {
        [Range(0, 100)]
        public int jpgQuality = 75;

        public void GetScreenshotBytes(Action<byte[]> onCompleteCallback) 
        {
            StartCoroutine(CaptureScreenshotBytes(onCompleteCallback));
        }

        private IEnumerator CaptureScreenshotBytes(Action<byte[]> onCompleteCallback) {
            yield return new WaitForEndOfFrame();
            
            var width = Screen.width;
            var height = Screen.height;
            var screenshotTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            
            screenshotTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshotTexture.Apply();
            
            byte[] jpgBytes = screenshotTexture.EncodeToJPG(jpgQuality);
            
            // Log for debugging size
            // Debug.Log($"Screenshot size: {jpgBytes.Length / 1024} KB.");

            Destroy(screenshotTexture);
            
            onCompleteCallback?.Invoke(jpgBytes);
        }
    }
}