using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public class AuraOutlineController : MonoBehaviour
    {
        public string AuraLayerName = "TransparentFX";
        public static Material softEdgeMaterial;
        public float sigma = 40.0f;
        public int downsample = 2;

        private RenderTexture SourceRT;
        private RenderTexture temp1;
        private RenderTexture temp2;
        private Camera captureCam;
        private Camera sourceCamera;
        private int captureLayer;

        public static AuraOutlineController main;
        public static Material FlameMat;

        void Awake()
        {
            int width = Screen.width / downsample;
            int height = Screen.height / downsample;
            SourceRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            temp1 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            temp2 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);

            captureLayer = LayerMask.NameToLayer(AuraLayerName);

            sourceCamera = FindObjectOfType<Camera>();
            GameObject camGO = new GameObject("CaptureCam");
            captureCam = camGO.AddComponent<Camera>();

            captureCam.clearFlags = CameraClearFlags.SolidColor;
            captureCam.backgroundColor = new Color(0, 0, 0, 0);
            captureCam.transform.localEulerAngles = new Vector3(180f, 0f, 0f);
            captureCam.orthographic = true;
            captureCam.orthographicSize = 5f;
            captureCam.nearClipPlane = -20;
            captureCam.farClipPlane = 1000;
            captureCam.cullingMask = 1 << captureLayer;
            captureCam.enabled = false;

            main = this;
        }

        void LateUpdate()
        {
            if (SourceRT == null || captureCam == null) return;

            captureCam.targetTexture = SourceRT;
            captureCam.transform.position = sourceCamera.transform.position;
            captureCam.orthographicSize = sourceCamera.orthographicSize;
            captureCam.Render();

            RenderTexture blurred = Blur(SourceRT, sigma);
            Shader.SetGlobalTexture("_AuraBlurRT", blurred);
        }

        public RenderTexture Blur(RenderTexture source, float sigma)
        {
            if (softEdgeMaterial == null) return source;

            softEdgeMaterial.SetFloat("_Sigma", sigma / captureCam.orthographicSize);
            softEdgeMaterial.SetVector("_Direction", new Vector2(1f, 0f));
            Graphics.Blit(source, temp1, softEdgeMaterial);
            softEdgeMaterial.SetVector("_Direction", new Vector2(0f, 1f));
            Graphics.Blit(temp1, temp2, softEdgeMaterial);

            return temp2;
        }

        void OnDestroy()
        {
            if (temp1) temp1.Release();
            if (temp2) temp2.Release();

            if (captureCam != null)
            {
                DestroyImmediate(captureCam.gameObject);
            }
        }
    }
}
