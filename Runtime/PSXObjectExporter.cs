using System.Collections.Generic;
using SplashEdit.RuntimeCode;
using UnityEngine;
using UnityEngine.Serialization;

namespace SplashEdit.RuntimeCode
{
    public enum PSXCollisionType
    {
        None = 0,
        Static = 1,
        Dynamic = 2
    }

    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [Icon("Packages/net.psxsplash.splashedit/Icons/PSXObjectExporter.png")]
    public class PSXObjectExporter : MonoBehaviour, IPSXExportable
    {
        public LuaFile LuaFile => luaFile;

        [FormerlySerializedAs("IsActive")]
        [SerializeField] private bool isActive = true;
        public bool IsActive => isActive;

        public List<PSXTexture2D> Textures { get; set; } = new List<PSXTexture2D>();
        public PSXMesh Mesh { get; protected set; }

        [FormerlySerializedAs("BitDepth")]
        [SerializeField] private PSXBPP bitDepth = PSXBPP.TEX_8BIT;
        [SerializeField] private LuaFile luaFile;

        [FormerlySerializedAs("collisionType")]
        [SerializeField] private PSXCollisionType collisionType = PSXCollisionType.None;

        public PSXBPP BitDepth => bitDepth;
        public PSXCollisionType CollisionType => collisionType;

        private readonly Dictionary<(int, PSXBPP), PSXTexture2D> cache = new();

        public void CreatePSXTextures2D()
        {
            Renderer renderer = GetComponent<Renderer>();
            Textures.Clear();
            if (renderer == null) return;

            Material[] materials = renderer.sharedMaterials;
            foreach (Material mat in materials)
            {
                if (mat == null || mat.mainTexture == null) continue;

                Texture mainTexture = mat.mainTexture;
                Texture2D tex2D = mainTexture is Texture2D existing
                    ? existing
                    : ConvertToTexture2D(mainTexture);

                if (tex2D == null) continue;

                if (cache.TryGetValue((tex2D.GetInstanceID(), bitDepth), out var cached))
                {
                    Textures.Add(cached);
                }
                else
                {
                    var tex = PSXTexture2D.CreateFromTexture2D(tex2D, bitDepth);
                    tex.OriginalTexture = tex2D;
                    cache.Add((tex2D.GetInstanceID(), bitDepth), tex);
                    Textures.Add(tex);
                }
            }
        }

        private static Texture2D ConvertToTexture2D(Texture src)
        {
            Texture2D texture2D = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);

            RenderTexture currentActiveRT = RenderTexture.active;
            RenderTexture.active = src as RenderTexture;

            texture2D.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = currentActiveRT;

            return texture2D;
        }

        public PSXTexture2D GetTexture(int index)
        {
            if (index >= 0 && index < Textures.Count)
            {
                return Textures[index];
            }
            return null;
        }

        public void CreatePSXMesh(float GTEScaling)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                Mesh = PSXMesh.CreateFromUnityRenderer(renderer, GTEScaling, transform, Textures);
            }
        }
    }
}