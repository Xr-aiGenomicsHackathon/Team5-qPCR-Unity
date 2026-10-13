using System.IO;
using UnityEditor;
using UnityEngine;

namespace Team5.qPCR.Editor
{
    public static class Team5Capture
    {
        public static string Capture(string name,int width=1920,int height=1080)
        {
            var camera=Camera.main;
            var canvas=GameObject.Find("COMPACT_DESKTOP_AND_XR_UI").GetComponent<Canvas>();
            var path=Path.GetFullPath("../deliverables/Team5-qPCR/Screenshots/"+name+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.08f;
                Canvas.ForceUpdateCanvases();
                foreach(var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
            }
            return path;
        }
    }
}
