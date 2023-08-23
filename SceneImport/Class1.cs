using System;
using System.IO;
using BaseX;
using CodeX;
using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using B83.Win32;

namespace SceneImport
{
    

    public class MainMod : NeosMod
    {
        public override string Name => "Roblox Importer";

        public override string Author => "holadivinus";

        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(InputInterface), "ReceiveFiles")]
    class Patch_InputInterface_ReceiveFiles
    {
        public static bool Prefix(List<string> files)
        {
            if (files.Count == 1 && files[0].EndsWith(".roblox"))
            {
                new RobloxUnpacker(files[0]);
                return false;
            }
            else return true;
        }
    }

    class RobloxUnpacker
    {
        #region PartData
        public abstract class PartBase
        {
            public PartBase(string data, RobloxUnpacker parent)
            {
                deserialize(data);
                createSlot(parent);
            }

            public string Name;
            public float3 Position;
            public floatQ Orientation;
            public float3 Size;
            public int BrickColor;
            public int Material;
            public float Transparency;
            public bool CanCollide;
            public float Reflectance;
            public string[][] Decals;
            public string[] SpecialMesh;

            private void deserialize(string data)
            {
                foreach (string prop in data.Split('\n'))
                {
                    string[] keyVal = prop.Split(':');
                    if (!(keyVal.Length >= 2))
                        continue;
                    string key = keyVal[0].Trim();
                    string value = keyVal[1].Trim();

                    switch (key)
                    {
                        case nameof(Name):
                            Name = value;
                            break;
                        case nameof(Position):
                            Position = float3.Parse('[' + value.Replace(',', ';') + ']');
                            break;
                        case nameof(Orientation):
                            Orientation = floatQ.Euler(float3.Parse('[' + value.Replace(',', ';') + ']'));
                            break;
                        case nameof(Size):
                            Size = float3.Parse('[' + value.Replace(',', ';') + ']');
                            break;
                        case nameof(BrickColor):
                            BrickColor = int.Parse(value);
                            break;
                        case nameof(Material):
                            Material = int.Parse(value);
                            break;
                        case nameof(Transparency):
                            Transparency = 1-float.Parse(value);
                            break;
                        case nameof(CanCollide):
                            CanCollide = bool.Parse(value);
                            break;
                        case nameof(Reflectance):
                            Reflectance = float.Parse(value);
                            break;
                        case nameof(SpecialMesh):
                            SpecialMesh = value.Replace("{", "").Replace("}", "").Split(new string[] { "-TO-" }, StringSplitOptions.None);
                            break;
                        case "Decal":
                            if (Decals == null)
                                Decals = new string[0][];
                            Decals = Decals.AddItem(value.Replace("{", "").Replace("}", "").Split(new string[] { "-TO-" }, StringSplitOptions.None)).ToArray();
                            break;
                        default:
                            NotFound(key, value);
                            break;
                    }
                }

            }

            protected abstract void NotFound(string key, string value);

            private Slot createSlot(RobloxUnpacker unpacker)
            {
                Slot partSlot = unpacker.rblxPartsRoot.AddSlot(Name);
                partSlot.GlobalPosition = Position;
                partSlot.GlobalRotation = Orientation;
                partSlot.GlobalScale = Size;

                if (Transparency > (1 - .998f))
                {
                    if (SpecialMesh == null)
                    {
                        MeshRenderer visual = partSlot.AttachComponent<MeshRenderer>();

                        GetMesh(visual, unpacker);
                        visual.Materials.Add(unpacker.GetMainMaterialProvider(Transparency, Reflectance, Material)); // one of the 5x5 transparancy * reflectance * material mats
                        visual.Materials.Add(unpacker.GetSecondMaterialProvider(Transparency < 1)); // just the 1 tint mat

                        //visual.MaterialPropertyBlocks.Add(unpacker.GetMainPropertyBlock(Material)); // per renderer mat tex (DEPRECATED: Cant use triplaner UV with propblocks)
                        visual.MaterialPropertyBlocks.Add(null);
                        visual.MaterialPropertyBlocks.Add(unpacker.GetSecondPropertyBlock(BrickColor)); // per renderer tint

                        if (CanCollide)
                        {
                            MeshCollider col = partSlot.AttachComponent<MeshCollider>();
                            col.Mesh.TrySet((AssetProvider<Mesh>)visual.Mesh);
                            col.CharacterCollider.Value = true;
                            col.Type.Value = ColliderType.Static;
                        }
                    }
                    else
                    {
                        Slot meshSlot = partSlot.AddSlot("SpecialMesh");
                        MeshRenderer visual = meshSlot.AttachComponent<MeshRenderer>();
                        visual.Material.TrySet(unpacker.GetMeshMaterialProvider());
                        visual.MaterialPropertyBlocks.Add(unpacker.GetSecondPropertyBlock(BrickColor));

                        bool texSet = false;
                        for (int i = 0; i < SpecialMesh.Length; i++)
                        {
                            string fileMeshProp = SpecialMesh[i];
                            if (string.IsNullOrWhiteSpace(fileMeshProp))
                                continue;

                            switch (i)
                            {
                                case 0: // MeshType
                                    switch (fileMeshProp)
                                    {
                                        case "Enum.MeshType.Head":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///0d6622ae62755dd51e1b9392d3f849cf01afedb5452fce14cb6a01a76eaeeb46.meshx"), "Enum.MeshType.Head"));
                                            break;
                                        case "Enum.MeshType.Brick":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///b53d728254b770026e4d2289b6cc537b382be38594b99b51ea3af2ce5642b1eb.meshx"), "Enum.MeshType.Brick"));
                                            break;
                                        case "Enum.MeshType.Cylinder":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///f1637588304835e5383333ced05a7df5cc2b4a5215e2b9d93812139a78f2ac76.meshx"), "Enum.MeshType.Cylinder"));
                                            break;
                                        case "Enum.MeshType.Sphere":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///239f004c759f0c04e024dd718122bf1a670723f7b7d0245a2bb84e93ad7c7582.meshx"), "Enum.MeshType.Sphere"));
                                            break;
                                        case "Enum.MeshType.Torso":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///b53d728254b770026e4d2289b6cc537b382be38594b99b51ea3af2ce5642b1eb.meshx"), "Enum.MeshType.Brick"));
                                            break;
                                        case "Enum.MeshType.Wedge":
                                            visual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///7e2fb93542ae3547b4d5ac977086ee0f2f69b5c9849646e73067d30a8b453ff9.meshx"), "Enum.MeshType.Wedge"));
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                case 1: // MeshID
                                    visual.Mesh.TrySet(unpacker.GetFileMeshProvider(fileMeshProp));
                                    break;
                                case 2: // TexID
                                    MainTexturePropertyBlock fileTexBlock = unpacker.GetFileTexturePropBlock(fileMeshProp);
                                    if (fileTexBlock != null)
                                        visual.MaterialPropertyBlocks[0] = fileTexBlock;
                                    texSet = true;
                                    break;
                                case 3: // Scale
                                    if (SpecialMesh[0] == "Enum.MeshType.FileMesh")
                                    {
                                        meshSlot.GlobalScale = float3.Parse($"[{fileMeshProp.Replace(',', ';')}]") * new float3(-1, 1, 1);
                                    }
                                    else meshSlot.Scale_Field.Value = float3.Parse($"[{fileMeshProp.Replace(',', ';')}]");
                                    break;
                                case 4: // Offset
                                    meshSlot.Position_Field.Value = float3.Parse($"[{fileMeshProp.Replace(',', ';')}]");
                                    break;
                                case 5: // Color RGB
                                    float3 colRGB = float3.Parse($"[{fileMeshProp.Replace(',', ';')}]");
                                    bool flip = meshSlot.Scale_Field.Value.z < 0;
                                    if (colRGB == float3.One && !flip)
                                        continue;

                                    XiexeToonMaterial altMat = meshSlot.AttachComponent<XiexeToonMaterial>();
                                    altMat.CopyProperties((AssetProvider<Material>)visual.Material);
                                    altMat.CopyValues((AssetProvider<Material>)visual.Material);
                                    visual.Material.TrySet(altMat);

                                    altMat.Color.Value = texSet ? new color(colRGB.x, colRGB.y, colRGB.z) : (unpacker.s_colorDict[BrickColor].Item2 / 255);
                                    altMat.Culling.Value = flip ? Culling.Front : Culling.Back;
                                    UniLog.Log(flip);
                                    break;
                            }
                        }
                    }
                }
                if (Decals != null)
                {
                    partSlot.Name = "DECAL";
                    foreach (string[] decals in Decals)
                    {
                        Slot decalSlot = partSlot.AddSlot(decals[1]);
                        MeshRenderer decalVisual = decalSlot.AttachComponent<MeshRenderer>();
                        decalVisual.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///e1177130cdb60f4914b31ad923a6046717424e09705e550a9ebd1b37ef9d2eba.meshx"), "DecalPlane"));

                        decalVisual.Material.TrySet(unpacker.GetDecalMaterial());
                        decalVisual.MaterialPropertyBlocks.Add(unpacker.GetFileTexturePropBlock(decals[1]));

                        UniLog.Log(decals[3]);
                        switch (decals[3])
                        {
                            case "Enum.NormalId.Top":
                                decalSlot.Position_Field.Value = new float3(0, 0.505f, 0);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(0, 0, 0);
                                break;
                            case "Enum.NormalId.Front":
                                decalSlot.Position_Field.Value = new float3(0, 0, -0.505f);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(90, 180, 180);
                                decalSlot.Scale_Field.Value *= new float3(1, -1, 1);
                                break;
                            case "Enum.NormalId.Back":
                                decalSlot.Position_Field.Value = new float3(0, 0, 0.505f);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(-90, 180, 180);
                                decalSlot.Scale_Field.Value *= new float3(-1, -1, -1);
                                break;
                            case "Enum.NormalId.Bottom":
                                decalSlot.Position_Field.Value = new float3(0, -0.505f, 0);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(0, 180, 180);
                                break;
                            case "Enum.NormalId.Left":
                                decalSlot.Position_Field.Value = new float3(-0.505f, 0, 0);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(90, 0, 90);
                                break;
                            case "Enum.NormalId.Right":
                                decalSlot.Position_Field.Value = new float3(0.505f, 0, 0);
                                decalSlot.Rotation_Field.Value = floatQ.Euler(0, 0, -90);
                                break;
                            default:
                                break;
                        }
                    }
                }
                return partSlot;
            }

            protected abstract void GetMesh(MeshRenderer targRenderer, RobloxUnpacker unpacker);
        }
        public class Part : PartBase
        {
            public enum PartType { Ball, Block, CornerWedge, Cylinder, Wedge }
            private static readonly Dictionary<PartType, Uri> s_meshUris = new Dictionary<PartType, Uri>()
            {
                {PartType.Block, new Uri("neosdb:///92a497f5052b3b1494d8d92347ffcece2cc7572d8fa707900843e95d7864952c.meshx")},
                {PartType.Ball, new Uri("neosdb:///283b5ced1d9128fd7a56d3bf498f4ca5582cf41f6fb952dbe197de3e0a23a0db.meshx")},
                {PartType.CornerWedge, new Uri("neosdb:///6c47fb0f2188c0468677e1ec279860d12a7f10c469aeb0222665473d880b71b4.meshx")},
                {PartType.Cylinder, new Uri("neosdb:///d6ff759dc058e94befd162cff71fa56c7b63cb2042e518a8006ef701671258e2.meshx")},
                {PartType.Wedge, new Uri("neosdb:///3cd651533256623f4f1bbbadddf41fa69d3a98ce0adc3d395ad8026f890c40c7.meshx")}
            };

            public Part(string data, RobloxUnpacker parent) : base(data, parent) { }
            public PartType Shape;

            protected override void NotFound(string key, string value)
            {
                if (key == nameof(Shape))
                    Shape = (PartType)int.Parse(value);
            }

            
            protected override void GetMesh(MeshRenderer targRenderer, RobloxUnpacker unpacker)
                => targRenderer.Mesh.TrySet(unpacker.GetMeshProvider(s_meshUris[Shape], Shape.GetType().Name + '.' + Shape.ToString()));
        }
        
        public class WedgePart : PartBase 
        {
            public WedgePart(string data, RobloxUnpacker parent) : base(data, parent) { }
            protected override void NotFound(string key, string value) { }
            protected override void GetMesh(MeshRenderer targRenderer, RobloxUnpacker unpacker)
            {
                targRenderer.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///3cd651533256623f4f1bbbadddf41fa69d3a98ce0adc3d395ad8026f890c40c7.meshx"), "PartType.WedgePart"));
                targRenderer.Slot.Scale_Field.Value *= new float3(1, 1, -1);
            }
        }
        public class CornerWedgePart : PartBase 
        {
            public CornerWedgePart(string data, RobloxUnpacker parent) : base(data, parent) { }
            protected override void NotFound(string key, string value) { }
            protected override void GetMesh(MeshRenderer targRenderer, RobloxUnpacker unpacker)
            {
                targRenderer.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///6c47fb0f2188c0468677e1ec279860d12a7f10c469aeb0222665473d880b71b4.meshx"), "PartType.Block"));
                targRenderer.Slot.Scale_Field.Value *= new float3(1, 1, -1);
            }
        }
        public class TrussPart : PartBase 
        {
            public TrussPart(string data, RobloxUnpacker parent) : base(data, parent) { }
            protected override void NotFound(string key, string value) { }
            protected override void GetMesh(MeshRenderer targRenderer, RobloxUnpacker unpacker)
                => targRenderer.Mesh.TrySet(unpacker.GetMeshProvider(new Uri("neosdb:///92a497f5052b3b1494d8d92347ffcece2cc7572d8fa707900843e95d7864952c.meshx"), "PartType.CornerWedgePart"));
        }
        #endregion

        public RobloxUnpacker(string filePath)
        {
            string[] assetPaths = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(filePath), "assets"));
            foreach (string assetPath in assetPaths)
                _assetPaths.Add(Path.GetFileNameWithoutExtension(assetPath).Split('_')[0], assetPath);

            world = Engine.Current.WorldManager.FocusedWorld;
            world.RunSynchronously(() =>
            {
                MeshImporter.i = 3;

                rblxImportRoot = world.RootSlot.AddSlot("RobloxImport");
                rblxAssetsRoot = rblxImportRoot.AddSlot("RobloxAssets");
                rblxPartsRoot = rblxImportRoot.AddSlot("RobloxParts");

                Log = rblxImportRoot.AttachComponent<Comment>();
                string er = "";
                try
                {
                    string[] parts = File.ReadAllText(filePath).Split(new string[] { "\n_" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        int iof = part.IndexOf(':');
                        string partType = part.Substring(0, iof);
                        string rest = part.Substring(iof + 1);

                        switch (partType)
                        {
                            case nameof(Part):
                                new Part(rest, this);
                                break;
                            case nameof(WedgePart):
                                new WedgePart(rest, this);
                                break;
                            case nameof(CornerWedgePart):
                                new CornerWedgePart(rest, this);
                                break;
                            case nameof(TrussPart):
                                new TrussPart(rest, this);
                                break;
                        }
                    }
                } catch(Exception ex)
                {
                    Log.Text.Value = ex.ToString() + er;
                }
                rblxImportRoot.Scale_Field.Value = new float3(-1, 1, 1);
            });
        }
        World world;

        Dictionary<string, string> _assetPaths = new Dictionary<string, string>();
        Slot rblxImportRoot;
        Slot rblxAssetsRoot;
        Slot rblxPartsRoot;

        Comment Log;

        Slot rblxMeshesRoot;
        Dictionary<Uri, AssetProvider<Mesh>> createdMeshes = new Dictionary<Uri, AssetProvider<Mesh>>();
        /// <summary>
        /// Gets or Creates a Mesh Provider for a uri if there is none.
        /// </summary>
        /// <param name="uri">The uri to find/add</param>
        /// <param name="name">The name of the Slot for the AssetProvider Component.</param>
        /// <returns></returns>
        private AssetProvider<Mesh> GetMeshProvider(Uri uri, string name)
        {
            if (rblxMeshesRoot == null)
                rblxMeshesRoot = rblxAssetsRoot.AddSlot("Meshes");

            if (createdMeshes.TryGetValue(uri, out AssetProvider<Mesh> mesh))
                return mesh;
            else
            {
                Slot mSlot = rblxMeshesRoot.AddSlot(name);
                StaticMesh sMesh = mSlot.AttachComponent<StaticMesh>();
                sMesh.URL.Value = uri;
                createdMeshes.Add(uri, sMesh);
                return sMesh;
            }
        }

        Dictionary<string, AssetProvider<Mesh>> createdSpecialMeshes = new Dictionary<string, AssetProvider<Mesh>>();
        Dictionary<string, Action> specialMeshImportCompletes = new Dictionary<string, Action>();
        /// <summary>
        /// Imports a mesh file from the current .roblox file import's sibling directory names "assets", based on a prefix.
        /// Returns the mesh file's AssetProvider`[Mesh].
        /// </summary>
        /// <param name="ID">The Mesh file name's prefix</param>
        /// <returns></returns>
        private AssetProvider<Mesh> GetFileMeshProvider(string ID)
        {
            if (rblxMeshesRoot == null)
                rblxMeshesRoot = rblxAssetsRoot.AddSlot("Meshes");

            if (createdSpecialMeshes.TryGetValue(ID, out AssetProvider<Mesh> mesh))
                return mesh;
            else
            {
                Slot newSlot = rblxMeshesRoot.AddSlot(ID);
                StaticMesh finalAssetProv = newSlot.AttachComponent<StaticMesh>();
                createdSpecialMeshes.Add(ID, finalAssetProv);

                if (_assetPaths.TryGetValue(ID, out string assetPath))
                    MeshImporter.ImportMeshGetURI(assetPath, (uri) => finalAssetProv.URL.Value = uri);

                return finalAssetProv;
            }
        }
        static class MeshImporter 
        {
            public static int i;

            private class FileData
            {
                public string FilePath;
                public Action<Uri> OnComplete;
                public NeosLogoMenuProgress ProgressIndicator;
            }

            static Dictionary<string, FileData> s_pendingFiles = new Dictionary<string, FileData>();
            public static void ImportMeshGetURI(string filePath, Action<Uri> onComplete)
            {
                if (s_pendingFiles.TryGetValue(filePath, out FileData foundData))
                {
                    Action<Uri> old = foundData.OnComplete;
                    foundData.OnComplete = (uri) => 
                    {
                        old?.Invoke(uri);
                        onComplete?.Invoke(uri);
                    };
                } 
                else s_pendingFiles.Add(filePath, new FileData() { FilePath=filePath, OnComplete=onComplete, ProgressIndicator=null });

                FrooxEngineRunnerFound.Drop(filePath);
            }

            static class FrooxEngineRunnerFound
            {
                public static FrooxEngineRunner Instance;
                public static MethodInfo DropMethod = AccessTools.Method(typeof(FrooxEngineRunner), "DragDropHook_OnDroppedFiles");
                public static void Drop(string dropee)
                {
                    if (Instance == null)
                        Instance = UnityEngine.Object.FindObjectsOfType<FrooxEngineRunner>()[0];

                    DropMethod.Invoke(Instance, new object[] { new List<string> { dropee }, new POINT(0, 0) });
                }
            }

            [HarmonyPatch(typeof(ModelImportDialog), "OnAttach")]
            static class ModelImportDialogue_OnAttach_Patch
            {
                
                [HarmonyPostfix]
                static void PostFix(ModelImportDialog __instance)
                {
                    __instance.RunInUpdates(i += 3, () =>
                    {
                        string targetMesh = __instance.Paths.First();
                        if (s_pendingFiles.ContainsKey(targetMesh))
                        {
                            __instance._assetsOnObject.Value = true;
                            __instance.RunImport();
                        }
                    });
                }
            }

            [HarmonyPatch(typeof(ModelImporter), "ImportModelAsync")]
            static class ModelImporter_ImportModelAsync_Patch
            {
                [HarmonyPostfix]
                static void PostFix(string file, Slot targetSlot, ModelImportSettings settings, Slot assetsSlot, IProgressIndicator progressIndicator)
                { 
                    if (s_pendingFiles.TryGetValue(file, out FileData foundData))
                        foundData.ProgressIndicator = (NeosLogoMenuProgress)progressIndicator;
                }
            }
            [HarmonyPatch(typeof(NeosLogoMenuProgress), "ProgressDone")]
            static class IProgressIndicator_ProgressDone_Patch
            {
                [HarmonyPrefix]
                static void PreFix(NeosLogoMenuProgress __instance)
                {
                    FileData foundData = s_pendingFiles.Values.FirstOrDefault(fd => fd.ProgressIndicator == __instance);
                    if (foundData == null)
                        return;
                    Engine.Current.WorldManager.FocusedWorld.LocalUserSpace.RunInUpdates(3, () => 
                    {
                        Slot modelRoot = Engine.Current.WorldManager.FocusedWorld.LocalUserSpace.Children.FirstOrDefault(ch => ch.Name == Path.GetFileName(foundData.FilePath));
                        if (modelRoot == null)
                            return;

                        StaticMesh modelMesh = modelRoot.GetComponentInChildren<StaticMesh>();
                        if (modelMesh == null) 
                            return;

                        foundData.OnComplete.Invoke(modelMesh.URL.Value);
                        s_pendingFiles.Remove(foundData.FilePath);
                        modelRoot.Destroy();
                    });
                }
            }
        }

        Dictionary<string, MainTexturePropertyBlock> createdSpecialTextures = new Dictionary<string, MainTexturePropertyBlock>();
        private MainTexturePropertyBlock GetFileTexturePropBlock(string ID) 
        {
            if (rblxTexturesRoot == null)
                rblxTexturesRoot = rblxAssetsRoot.AddSlot("Textures");

            if (createdSpecialTextures.TryGetValue(ID, out MainTexturePropertyBlock block)) 
                return block;
            else
            {
                Slot newSlot = rblxTexturesRoot.AddSlot(ID);
                StaticTexture2D newTex = newSlot.AttachComponent<StaticTexture2D>();
                MainTexturePropertyBlock o = newSlot.AttachComponent<MainTexturePropertyBlock>();
                o.Texture.TrySet(newTex);

                if (_assetPaths.TryGetValue(ID, out string assetPath))
                    TextureImporter.ImportTexGetURI(assetPath, (uri) => newTex.URL.Value = uri);
                else
                {
                    createdSpecialTextures.Add(ID, null);
                    return null;
                }

                createdSpecialTextures.Add(ID, o);
                return o;
            }
        }
        private XiexeToonMaterial decalMat;
        private XiexeToonMaterial GetDecalMaterial()
        {
            if (rblxMaterialsRoot == null)
                rblxMaterialsRoot = rblxAssetsRoot.AddSlot("Materials");

            if (decalMat == null)
            {
                Slot decSlot = rblxMaterialsRoot.AddSlot("DecalMat");
                decalMat = decSlot.AttachComponent<XiexeToonMaterial>();
                decalMat.BlendMode.Value = BlendMode.Alpha;

                decalMat.ShadowRamp.TrySet(decSlot.AttachComponent<StaticTexture2D>());
                ((StaticTexture2D)decalMat.ShadowRamp).URL.Value = new Uri("neosdb:///d18f055d31acb340e03d0a93ca959799136958ca54000698816fa2ee8a5238bc.png");
            }
            return decalMat;
        }
        static class TextureImporter
        {
            public static int i;

            private class FileData
            {
                public string FilePath;
                public Action<Uri> OnComplete;
            }

            static Dictionary<string, FileData> s_pendingFiles = new Dictionary<string, FileData>();
            public static void ImportTexGetURI(string filePath, Action<Uri> onComplete)
            {
                if (s_pendingFiles.TryGetValue(filePath, out FileData foundData))
                {
                    Action<Uri> old = foundData.OnComplete;
                    foundData.OnComplete = (uri) =>
                    {
                        old?.Invoke(uri);
                        onComplete?.Invoke(uri);
                    };
                }
                else s_pendingFiles.Add(filePath, new FileData() { FilePath = filePath, OnComplete = onComplete });

                FrooxEngineRunnerFound.Drop(filePath);
            }

            static class FrooxEngineRunnerFound
            {
                public static FrooxEngineRunner Instance;
                public static MethodInfo DropMethod = AccessTools.Method(typeof(FrooxEngineRunner), "DragDropHook_OnDroppedFiles");
                public static void Drop(string dropee)
                {
                    if (Instance == null)
                        Instance = UnityEngine.Object.FindObjectsOfType<FrooxEngineRunner>()[0];

                    DropMethod.Invoke(Instance, new object[] { new List<string> { dropee }, new POINT(0, 0) });
                }
            }

            [HarmonyPatch(typeof(ImageImportDialog), "OnAttach")]
            static class ImageImportDialog_OnAttach_Patch
            {

                [HarmonyPostfix]
                static void PostFix(ImageImportDialog __instance)
                {
                    __instance.RunInUpdates(i += 3, () =>
                    {
                        string targetTex = __instance.Paths.First();
                        if (s_pendingFiles.ContainsKey(targetTex))
                            __instance.RunImport();
                        
                    });
                }
            }

            [HarmonyPatch(typeof(ImageImporter), "ImportImage")]
            static class ImageImporter_ImportImage_Patch
            {
                [HarmonyPostfix]
                static void PostFix(string path, Slot targetSlot)
                {
                    if (s_pendingFiles.TryGetValue(path, out FileData foundData))
                    {
                        Action updLoop = null;
                            
                        updLoop = () => 
                        {
                            StaticTexture2D tex = targetSlot.GetComponent<StaticTexture2D>();
                            if (tex != null)
                            {
                                if (tex.URL.Value.ToString().Length > 10)
                                {
                                    foundData.OnComplete.Invoke(new Uri(tex.URL.Value.ToString()));
                                    targetSlot.Destroy();
                                }
                                else targetSlot.RunInUpdates(5, updLoop);
                            }
                            else targetSlot.RunInUpdates(5, updLoop);
                        };

                        targetSlot.RunInUpdates(5, updLoop);
                    }
                }
            }
        }

        Slot rblxMaterialsRoot;
        private Dictionary<(int, int, int), AssetProvider<Material>> MainMaterials = new Dictionary<(int, int, int), AssetProvider<Material>>();
        private AssetProvider<Material> GetMainMaterialProvider(float transparency, float reflectivity, int material)
        {
            if (rblxMaterialsRoot == null)
                rblxMaterialsRoot = rblxAssetsRoot.AddSlot("Materials");

            int transp = (int)(transparency * 5); int reflec = (int)(reflectivity * 5); MatDef? matDef = GetMaterialTexture(material);

            if (matDef == null)
                material = -1;

            if (MainMaterials.TryGetValue((transp, reflec, material), out AssetProvider<Material> mat))
                return mat;
            else
            {
                PBS_TriplanarMetallic newMat = rblxMaterialsRoot.AddSlot($"{transp}x{reflec}x{material}_PBSMat").AttachComponent<PBS_TriplanarMetallic>();
                if (transp < 5)
                {
                    newMat.Transparent.Value = true;
                    newMat.AlbedoColor.Value = new color(1f, transp / 5f);
                }
                newMat.Smoothness.Value = reflec / 5f;
                newMat.TextureScale.Value = new float2(.2f, .2f);

                if (matDef.HasValue)
                    foreach (Def def in matDef.Value.Defs)
                    {
                        if (def.TexType == TexType.Diffuse)
                        {
                            newMat.AlbedoTexture.TrySet(def.TexRef.tex);
                        } else if (def.TexType == TexType.Normal)
                        {
                            newMat.NormalMap.TrySet(def.TexRef.tex);
                        }
                    }

                MainMaterials.Add((transp, reflec, material), newMat);
                return newMat;
            }
        }
        private XiexeToonMaterial meshMaterial;
        private XiexeToonMaterial GetMeshMaterialProvider()
        {
            if (rblxMaterialsRoot == null)
                rblxMaterialsRoot = rblxAssetsRoot.AddSlot("Materials");

            if (meshMaterial == null)
            {
                Slot meshMatSlot = rblxMaterialsRoot.AddSlot("MeshMaterial");
                meshMaterial = meshMatSlot.AttachComponent<XiexeToonMaterial>();
                meshMaterial.ShadowRamp.TrySet(meshMatSlot.AttachComponent<StaticTexture2D>());
                ((StaticTexture2D)meshMaterial.ShadowRamp).URL.Value = new Uri("neosdb:///d18f055d31acb340e03d0a93ca959799136958ca54000698816fa2ee8a5238bc.png");
            }
            return meshMaterial;
        }

        Slot BrickColorTintMaterial;
        private UI_UnlitMaterial SecondMaterialProvider1;
        private UI_UnlitMaterial SecondMaterialProvider2;
        private AssetProvider<Material> GetSecondMaterialProvider(bool transparent)
        {
            if (rblxMaterialsRoot == null)
                rblxMaterialsRoot = rblxAssetsRoot.AddSlot("Materials");
            if (BrickColorTintMaterial == null)
                BrickColorTintMaterial = rblxMaterialsRoot.AddSlot("BrickColorTintMaterial");

            UI_UnlitMaterial mat = transparent ? SecondMaterialProvider2 : SecondMaterialProvider1;

            if (mat == null)
            {
                if (!transparent)
                {
                    mat = SecondMaterialProvider1 = BrickColorTintMaterial.AttachComponent<UI_UnlitMaterial>();
                    mat.StencilComparison.Value = StencilComparison.NotEqual;
                    mat.StencilOperation.Value = StencilOperation.Zero;
                }
                else
                    mat = SecondMaterialProvider2 = BrickColorTintMaterial.AttachComponent<UI_UnlitMaterial>();

                mat.Sidedness.Value = Sidedness.Front;
                mat.BlendMode.Value = BlendMode.Multiply;
                mat.OffsetFactor.Value = -.1f;
                mat.ZWrite.Value = ZWrite.On;
                mat.ZTest.Value = ZTest.Equal;
            }

            return mat;
        }

        Slot rblxTexturesRoot;
        private MatDef? GetMaterialTexture(int materialNum)
        {
            if (rblxTexturesRoot == null)
                rblxTexturesRoot = rblxAssetsRoot.AddSlot("Textures");

            if (s_matTexDict.TryGetValue(materialNum, out MatDef associatedData))
            {
                Slot texSlot = rblxTexturesRoot.AddSlot(associatedData.Name);
                foreach (Def def in associatedData.Defs)
                {
                    if (def.TexRef.tex == null)
                    {
                        def.TexRef.tex = texSlot.AttachComponent<StaticTexture2D>();
                        def.TexRef.tex.URL.Value = def.Uri;
                        def.TexRef.tex.IsNormalMap.Value = def.TexType == TexType.Normal;
                    }
                }
                return associatedData;
            }
            else return null;
        }

        Slot rblxColorsRoot;
        private MainTexturePropertyBlock GetSecondPropertyBlock(int brickColor)
        {
            if (rblxColorsRoot == null)
                rblxColorsRoot = rblxAssetsRoot.AddSlot("Colors");

            if (s_colorDict.TryGetValue(brickColor, out (string, color, PropRef) associatedData))
            {
                if (associatedData.Item3.block == null)
                {
                    Slot colSlot = rblxColorsRoot.AddSlot(associatedData.Item1);
                    SolidColorTexture colTex = colSlot.AttachComponent<SolidColorTexture>();
                    colTex.Color.Value = associatedData.Item2.SetA(255) / 255;
                    associatedData.Item3.block = colSlot.AttachComponent<MainTexturePropertyBlock>();
                    associatedData.Item3.block.Texture.TrySet(colTex);
                }
                return associatedData.Item3.block;
            }
            throw new Exception("Color not in s_colorDict: " + brickColor);
        }

        private struct MatDef
        {
            public MatDef(string name, params Def[] defs)
            {
                Name = name; Defs = defs;
            }

            public string Name;
            public Def[] Defs;
        }
        private class Def
        {
            public Def(TexType type, string uri)
            {
                TexType = type;
                Uri = new Uri(uri);
                TexRef = new TexRef();
            }

            public TexType TexType;
            public Uri Uri;
            public TexRef TexRef;
        }
        private enum TexType
        {
            Diffuse, Normal, //NormalDetail, Reflection, Specular
        }

        private readonly Dictionary<int, MatDef> s_matTexDict = new Dictionary<int, MatDef>()//""
        {
            {848, new MatDef("Brick", new Def(TexType.Diffuse, "neosdb:///66acac0695dc90ba29ba7f855da07a13a3e5bd3ede2dfd967299e7fbae8f0a4d.dds"), new Def(TexType.Normal, "neosdb:///e9a111af5f2940bfc662a2249c04c3f1586bbf743e3c99c6d34957afb617c684.dds"))},
            {880, new MatDef("Cobblestone", new Def(TexType.Diffuse, "neosdb:///6d4dff884885ef59e1dcbf8c67d6c129f902beaf5e4a353977c8d1903edcb33c.dds"), new Def(TexType.Normal, "neosdb:///de6c0db283429b44ac503bf41967e597483fa7df5575fa7af960a5d5abcc9fab.dds"))},
            {816, new MatDef("Concrete", new Def(TexType.Diffuse, "neosdb:///fce292b13d41fe3b4caf813a1aa683ce71ce8151a4206a0001847573f3e8b178.dds"))},
            {1056, new MatDef("Diamondplate", new Def(TexType.Diffuse, "neosdb:///d96818b30547ef9227b2d307ce3f5be77ff0838243045d85e822a0d3f2ac2ebd.dds"), new Def(TexType.Normal, "neosdb:///37fba62cad9139fabce926f75254c3b5b458337bf459b7d3f3834aafc9241656.dds"))},
            {1312, new MatDef("Fabric", new Def(TexType.Diffuse, "neosdb:///af839ecf006fb0d50b7b424c291445f4f5dd8fdf74a5fd8e5ff51ca4a190100e.dds"), new Def(TexType.Normal, "neosdb:///528c204849de11643e74cf3d1b6d14fe1547a4cad7aa9f6b71c0396fa9be53fa.dds"))},
            {1568, new MatDef("Glass", new Def(TexType.Diffuse, "neosdb:///bdebf9362c652a37af7e123af283b146137586de30f1901903fd0ea46cbf2a47.dds"), new Def(TexType.Normal, "neosdb:///be28730c010ae7eeb2311cb35eb207f5ff75f99c2460d5d41cd5f9c5746721ad.dds"))},
            {832, new MatDef("Granite", new Def(TexType.Diffuse, "neosdb:///f0c5e84008d51442e3e3042adfa48415604b16c62badd01650c769b416d6b437.dds"))},
            {1280, new MatDef("Grass", new Def(TexType.Diffuse, "neosdb:///f04b61e0091dc1c9f5e1756803fdcfae20175ed1fd8129e2bfb7986e6c3a0943.dds"), new Def(TexType.Normal, "neosdb:///ca1c532083996ce2e7fa9dc0458d980c47b6b85359b76abc7ae6f2bb0f2f7ced.dds"))},
            {1536, new MatDef("Ice", new Def(TexType.Normal, "neosdb:///68591058c8a8b4f7629b9b40107145cb29ca8cd03853baca1ddadbaa1acb731b.dds"))},
            {784, new MatDef("Marble", new Def(TexType.Diffuse, "neosdb:///0fd2dee7c3e77ad4c5567cbe342fc168e7801b0150bfef56f1154ee693ea08b2.dds"), new Def(TexType.Normal, "neosdb:///de0183672916f51e1d860382b4922e9d64951ce0bb1422492eaeb186195efe3a.dds"))},
            {1088, new MatDef("Metal", new Def(TexType.Diffuse, "neosdb:///d8dde1b3eab4cfb8ab499c6257b29abf9c56ce35c6417cb7e131e887c20730a0.dds"), new Def(TexType.Normal, "neosdb:///cf27f8aa6c5812e8c3d24ea8988a0bb16b36d32db3cc0c085b06c2d69291438d.dds"))},
            {864, new MatDef("Pebble",  new Def(TexType.Diffuse, "neosdb:///54f5f356611b4bb6a226c8cea05cb62c9044ffe36087bd1fc522ff82df755b9c.dds"))},
            {1040, new MatDef("Rust",   new Def(TexType.Diffuse, "neosdb:///b2953135466ed65a51064702ec940fcc6e5613cec0e25fdeb49d7bf4fe892439.dds"), new Def(TexType.Normal, "neosdb:///bd57ca4663f44acd24a45a1ffcba261d955bc1978f840169a0e2cf7329f8fc39.dds"))},
            {1296, new MatDef("Sand",   new Def(TexType.Diffuse, "neosdb:///9a8b284ca5f93fc48a907bb8216268ed6222c5fd9c29bfb3e81ab43476d8a2ea.dds"), new Def(TexType.Normal, "neosdb:///a514eaf10ed84c7b9fac046a61aa5b2ef497e81ec7313cc22b3329618e9348ff.dds"))},
            {800, new MatDef("Slate",   new Def(TexType.Diffuse, "neosdb:///12f1096ac8acf838c6a7dc6779549682b8e5f80abb7d3e2fa6432a4d8cc0eeed.dds"), new Def(TexType.Normal, "neosdb:///d6470299db9b487585200b8911e9057f8daf30ed5ad413494b39b65d29b04cab.dds"))},
            {512, new MatDef("Wood",    new Def(TexType.Diffuse, "neosdb:///7878fd7dd23084c636dfa3d81d4b4b4491729f4836971c3079900a74b869dd88.dds"), new Def(TexType.Normal, "neosdb:///33a73bed6e9efff8d3e5cf102a092f7375b6f5cc73bdeb9a291914459f97abbf.dds"))},
            {528, new MatDef("WoodPlanks", new Def(TexType.Diffuse, "neosdb:///85a91ec4c91fa657e58c2398a6a48f86d85066e251e4387c04673dd0f45dd738.dds"), new Def(TexType.Normal, "neosdb:///98afe961894252a8f9e2f40bede6831e0763f5ddc2a4a579cca7bd48f2c197a6.dds"))},
            {1072, new MatDef("Foil", new Def(TexType.Normal, "neosdb:///68591058c8a8b4f7629b9b40107145cb29ca8cd03853baca1ddadbaa1acb731b.dds"))},
        };
        private class PropRef
        {
            public MainTexturePropertyBlock block;
        }
        private class TexRef
        {
            public StaticTexture2D tex;
        }
        private readonly Dictionary<int, (string, color, PropRef)> s_colorDict = new Dictionary<int, (string, color, PropRef)>()
        {
            { 1, ("White", new color(242, 243, 243), new PropRef()) },
            { 2, ("Grey", new color(161, 165, 162), new PropRef()) },
            { 3, ("Light yellow", new color(249, 233, 153), new PropRef()) },
            { 5, ("Brick yellow", new color(215, 197, 154), new PropRef()) },
            { 6, ("Light green (Mint)", new color(194, 218, 184), new PropRef()) },
            { 9, ("Light reddish violet", new color(232, 186, 200), new PropRef()) },
            { 11, ("Pastel Blue", new color(128, 187, 219), new PropRef()) },
            { 12, ("Light orange brown", new color(203, 132, 66), new PropRef()) },
            { 18, ("Nougat", new color(204, 142, 105), new PropRef()) },
            { 21, ("Bright red", new color(196, 40, 28), new PropRef()) },
            { 22, ("Med. reddish violet", new color(196, 112, 160), new PropRef()) },
            { 23, ("Bright blue", new color(13, 105, 172), new PropRef()) },
            { 24, ("Bright yellow", new color(245, 205, 48), new PropRef()) },
            { 25, ("Earth orange", new color(98, 71, 50), new PropRef()) },
            { 26, ("Black", new color(27, 42, 53), new PropRef()) },
            { 27, ("Dark grey", new color(109, 110, 108), new PropRef()) },
            { 28, ("Dark green", new color(40, 127, 71), new PropRef()) },
            { 29, ("Medium green", new color(161, 196, 140), new PropRef()) },
            { 36, ("Lig. Yellowich orange", new color(243, 207, 155), new PropRef()) },
            { 37, ("Bright green", new color(75, 151, 75), new PropRef()) },
            { 38, ("Dark orange", new color(160, 95, 53), new PropRef()) },
            { 39, ("Light bluish violet", new color(193, 202, 222), new PropRef()) },
            { 40, ("Transparent", new color(236, 236, 236), new PropRef()) },
            { 41, ("Tr. Red", new color(205, 84, 75), new PropRef()) },
            { 42, ("Tr. Lg blue", new color(193, 223, 240), new PropRef()) },
            { 43, ("Tr. Blue", new color(123, 182, 232), new PropRef()) },
            { 44, ("Tr. Yellow", new color(247, 241, 141), new PropRef()) },
            { 45, ("Light blue", new color(180, 210, 228), new PropRef()) },
            { 47, ("Tr. Flu. Reddish orange", new color(217, 133, 108), new PropRef()) },
            { 48, ("Tr. Green", new color(132, 182, 141), new PropRef()) },
            { 49, ("Tr. Flu. Green", new color(248, 241, 132), new PropRef()) },
            { 50, ("Phosph. White", new color(236, 232, 222), new PropRef()) },
            { 100, ("Light red", new color(238, 196, 182), new PropRef()) },
            { 101, ("Medium red", new color(218, 134, 122), new PropRef()) },
            { 102, ("Medium blue", new color(110, 153, 202), new PropRef()) },
            { 103, ("Light grey", new color(199, 193, 183), new PropRef()) },
            { 104, ("Bright violet", new color(107, 50, 124), new PropRef()) },
            { 105, ("Br. yellowish orange", new color(226, 155, 64), new PropRef()) },
            { 106, ("Bright orange", new color(218, 133, 65), new PropRef()) },
            { 107, ("Bright bluish green", new color(0, 143, 156), new PropRef()) },
            { 108, ("Earth yellow", new color(104, 92, 67), new PropRef()) },
            { 110, ("Bright bluish violet", new color(67, 84, 147), new PropRef()) },
            { 111, ("Tr. Brown", new color(191, 183, 177), new PropRef()) },
            { 112, ("Medium bluish violet", new color(104, 116, 172), new PropRef()) },
            { 113, ("Tr. Medi. reddish violet", new color(229, 173, 200), new PropRef()) },
            { 115, ("Med. yellowish green", new color(199, 210, 60), new PropRef()) },
            { 116, ("Med. bluish green", new color(85, 165, 175), new PropRef()) },
            { 118, ("Light bluish green", new color(183, 215, 213), new PropRef()) },
            { 119, ("Br. yellowish green", new color(164, 189, 71), new PropRef()) },
            { 120, ("Lig. yellowish green", new color(217, 228, 167), new PropRef()) },
            { 121, ("Med. yellowish orange", new color(231, 172, 88), new PropRef()) },
            { 123, ("Br. reddish orange", new color(211, 111, 76), new PropRef()) },
            { 124, ("Bright reddish violet", new color(146, 57, 120), new PropRef()) },
            { 125, ("Light orange", new color(234, 184, 146), new PropRef()) },
            { 126, ("Tr. Bright bluish violet", new color(165, 165, 203), new PropRef()) },
            { 127, ("Gold", new color(220, 188, 129), new PropRef()) },
            { 128, ("Dark nougat", new color(174, 122, 89), new PropRef()) },
            { 131, ("Silver", new color(156, 163, 168), new PropRef()) },
            { 133, ("Neon orange", new color(213, 115, 61), new PropRef()) },
            { 134, ("Neon green", new color(216, 221, 86), new PropRef()) },
            { 135, ("Sand blue", new color(116, 134, 157), new PropRef()) },
            { 136, ("Sand violet", new color(135, 124, 144), new PropRef()) },
            { 137, ("Medium orange", new color(224, 152, 100), new PropRef()) },
            { 138, ("Sand yellow", new color(149, 138, 115), new PropRef()) },
            { 140, ("Earth blue", new color(32, 58, 86), new PropRef()) },
            { 141, ("Earth green", new color(39, 70, 45), new PropRef()) },
            { 143, ("Tr. Flu. Blue", new color(207, 226, 247), new PropRef()) },
            { 145, ("Sand blue metallic", new color(121, 136, 161), new PropRef()) },
            { 146, ("Sand violet metallic", new color(149, 142, 163), new PropRef()) },
            { 147, ("Sand yellow metallic", new color(147, 135, 103), new PropRef()) },
            { 148, ("Dark grey metallic", new color(87, 88, 87), new PropRef()) },
            { 149, ("Black metallic", new color(22, 29, 50), new PropRef()) },
            { 150, ("Light grey metallic", new color(171, 173, 172), new PropRef()) },
            { 151, ("Sand green", new color(120, 144, 130), new PropRef()) },
            { 153, ("Sand red", new color(149, 121, 119), new PropRef()) },
            { 154, ("Dark red", new color(123, 46, 47), new PropRef()) },
            { 157, ("Tr. Flu. Yellow", new color(255, 246, 123), new PropRef()) },
            { 158, ("Tr. Flu. Red", new color(225, 164, 194), new PropRef()) },
            { 168, ("Gun metallic", new color(117, 108, 98), new PropRef()) },
            { 176, ("Red flip/flop", new color(151, 105, 91), new PropRef()) },
            { 178, ("Yellow flip/flop", new color(180, 132, 85), new PropRef()) },
            { 179, ("Silver flip/flop", new color(137, 135, 136), new PropRef()) },
            { 180, ("Curry", new color(215, 169, 75), new PropRef()) },
            { 190, ("Fire Yellow", new color(249, 214, 46), new PropRef()) },
            { 191, ("Flame yellowish orange", new color(232, 171, 45), new PropRef()) },
            { 192, ("Reddish brown", new color(105, 64, 40), new PropRef()) },
            { 193, ("Flame reddish orange", new color(207, 96, 36), new PropRef()) },
            { 194, ("Medium stone grey", new color(163, 162, 165), new PropRef()) },
            { 195, ("Royal blue", new color(70, 103, 164), new PropRef()) },
            { 196, ("Dark Royal blue", new color(35, 71, 139), new PropRef()) },
            { 198, ("Bright reddish lilac", new color(142, 66, 133), new PropRef()) },
            { 199, ("Dark stone grey", new color(99, 95, 98), new PropRef()) },
            { 200, ("Lemon metalic", new color(130, 138, 93), new PropRef()) },
            { 208, ("Light stone grey", new color(229, 228, 223), new PropRef()) },
            { 209, ("Dark Curry", new color(176, 142, 68), new PropRef()) },
            { 210, ("Faded green", new color(112, 149, 120), new PropRef()) },
            { 211, ("Turquoise", new color(121, 181, 181), new PropRef()) },
            { 212, ("Light Royal blue", new color(159, 195, 233), new PropRef()) },
            { 213, ("Medium Royal blue", new color(108, 129, 183), new PropRef()) },
            { 216, ("Rust", new color(144, 76, 42), new PropRef()) },
            { 217, ("Brown", new color(124, 92, 70), new PropRef()) },
            { 218, ("Reddish lilac", new color(150, 112, 159), new PropRef()) },
            { 219, ("Lilac", new color(107, 98, 155), new PropRef()) },
            { 220, ("Light lilac", new color(167, 169, 206), new PropRef()) },
            { 221, ("Bright purple", new color(205, 98, 152), new PropRef()) },
            { 222, ("Light purple", new color(228, 173, 200), new PropRef()) },
            { 223, ("Light pink", new color(220, 144, 149), new PropRef()) },
            { 224, ("Light brick yellow", new color(240, 213, 160), new PropRef()) },
            { 225, ("Warm yellowish orange", new color(235, 184, 127), new PropRef()) },
            { 226, ("Cool yellow", new color(253, 234, 141), new PropRef()) },
            { 232, ("Dove blue", new color(125, 187, 221), new PropRef()) },
            { 268, ("Medium lilac", new color(52, 43, 117), new PropRef()) },
            { 301, ("Slime green", new color(80, 109, 84), new PropRef()) },
            { 302, ("Smoky grey", new color(91, 93, 105), new PropRef()) },
            { 303, ("Dark blue", new color(0, 16, 176), new PropRef()) },
            { 304, ("Parsley green", new color(44, 101, 29), new PropRef()) },
            { 305, ("Steel blue", new color(82, 124, 174), new PropRef()) },
            { 306, ("Storm blue", new color(51, 88, 130), new PropRef()) },
            { 307, ("Lapis", new color(16, 42, 220), new PropRef()) },
            { 308, ("Dark indigo", new color(61, 21, 133), new PropRef()) },
            { 309, ("Sea green", new color(52, 142, 64), new PropRef()) },
            { 310, ("Shamrock", new color(91, 154, 76), new PropRef()) },
            { 311, ("Fossil", new color(159, 161, 172), new PropRef()) },
            { 312, ("Mulberry", new color(89, 34, 89), new PropRef()) },
            { 313, ("Forest green", new color(31, 128, 29), new PropRef()) },
            { 314, ("Cadet blue", new color(159, 173, 192), new PropRef()) },
            { 315, ("Electric blue", new color(9, 137, 207), new PropRef()) },
            { 316, ("Eggplant", new color(123, 0, 123), new PropRef()) },
            { 317, ("Moss", new color(124, 156, 107), new PropRef()) },
            { 318, ("Artichoke", new color(138, 171, 133), new PropRef()) },
            { 319, ("Sage green", new color(185, 196, 177), new PropRef()) },
            { 320, ("Ghost grey", new color(202, 203, 209), new PropRef()) },
            { 321, ("Lilac", new color(167, 94, 155), new PropRef()) },
            { 322, ("Plum", new color(123, 47, 123), new PropRef()) },
            { 323, ("Olivine", new color(148, 190, 129), new PropRef()) },
            { 324, ("Laurel green", new color(168, 189, 153), new PropRef()) },
            { 325, ("Quill grey", new color(223, 223, 222), new PropRef()) },
            { 327, ("Crimson", new color(151, 0, 0), new PropRef()) },
            { 328, ("Mint", new color(177, 229, 166), new PropRef()) },
            { 329, ("Baby blue", new color(152, 194, 219), new PropRef()) },
            { 330, ("Carnation pink", new color(255, 152, 220), new PropRef()) },
            { 331, ("Persimmon", new color(255, 89, 89), new PropRef()) },
            { 332, ("Maroon", new color(117, 0, 0), new PropRef()) },
            { 333, ("Gold", new color(239, 184, 56), new PropRef()) },
            { 334, ("Daisy orange", new color(248, 217, 109), new PropRef()) },
            { 335, ("Pearl", new color(231, 231, 236), new PropRef()) },
            { 336, ("Fog", new color(199, 212, 228), new PropRef()) },
            { 337, ("Salmon", new color(255, 148, 148), new PropRef()) },
            { 338, ("Terra Cotta", new color(190, 104, 98), new PropRef()) },
            { 339, ("Cocoa", new color(86, 36, 36), new PropRef()) },
            { 340, ("Wheat", new color(241, 231, 199), new PropRef()) },
            { 341, ("Buttermilk", new color(254, 243, 187), new PropRef()) },
            { 342, ("Mauve", new color(224, 178, 208), new PropRef()) },
            { 343, ("Sunrise", new color(212, 144, 189), new PropRef()) },
            { 344, ("Tawny", new color(150, 85, 85), new PropRef()) },
            { 345, ("Rust", new color(143, 76, 42), new PropRef()) },
            { 346, ("Cashmere", new color(211, 190, 150), new PropRef()) },
            { 347, ("Khaki", new color(226, 220, 188), new PropRef()) },
            { 348, ("Lily white", new color(237, 234, 234), new PropRef()) },
            { 349, ("Seashell", new color(233, 218, 218), new PropRef()) },
            { 350, ("Burgundy", new color(136, 62, 62), new PropRef()) },
            { 351, ("Cork", new color(188, 155, 93), new PropRef()) },
            { 352, ("Burlap", new color(199, 172, 120), new PropRef()) },
            { 353, ("Beige", new color(202, 191, 163), new PropRef()) },
            { 354, ("Oyster", new color(187, 179, 178), new PropRef()) },
            { 355, ("Pine Cone", new color(108, 88, 75), new PropRef()) },
            { 356, ("Fawn brown", new color(160, 132, 79), new PropRef()) },
            { 357, ("Hurricane grey", new color(149, 137, 136), new PropRef()) },
            { 358, ("Cloudy grey", new color(171, 168, 158), new PropRef()) },
            { 359, ("Linen", new color(175, 148, 131), new PropRef()) },
            { 360, ("Copper", new color(150, 103, 102), new PropRef()) },
            { 361, ("Medium brown", new color(86, 66, 54), new PropRef()) },
            { 362, ("Bronze", new color(126, 104, 63), new PropRef()) },
            { 363, ("Flint", new color(105, 102, 92), new PropRef()) },
            { 364, ("Dark taupe", new color(90, 76, 66), new PropRef()) },
            { 365, ("Burnt Sienna", new color(106, 57, 9), new PropRef()) },
            { 1001, ("Institutional white", new color(248, 248, 248), new PropRef()) },
            { 1002, ("Mid gray", new color(205, 205, 205), new PropRef()) },
            { 1003, ("Really black", new color(17, 17, 17), new PropRef()) },
            { 1004, ("Really red", new color(255, 0, 0), new PropRef()) },
            { 1005, ("Deep orange", new color(255, 176, 0), new PropRef()) },
            { 1006, ("Alder", new color(180, 128, 255), new PropRef()) },
            { 1007, ("Dusty Rose", new color(163, 75, 75), new PropRef()) },
            { 1008, ("Olive", new color(193, 190, 66), new PropRef()) },
            { 1009, ("New Yeller", new color(255, 255, 0), new PropRef()) },
            { 1010, ("Really blue", new color(0, 0, 255), new PropRef()) },
            { 1011, ("Navy blue", new color(0, 32, 96), new PropRef()) },
            { 1012, ("Deep blue", new color(33, 84, 185), new PropRef()) },
            { 1013, ("Cyan", new color(4, 175, 236), new PropRef()) },
            { 1014, ("CGA brown", new color(170, 85, 0), new PropRef()) },
            { 1015, ("Magenta", new color(170, 0, 170), new PropRef()) },
            { 1016, ("Pink", new color(255, 102, 204), new PropRef()) },
            { 1017, ("Deep orange", new color(255, 175, 0), new PropRef()) },
            { 1018, ("Teal", new color(18, 238, 212), new PropRef()) },
            { 1019, ("Toothpaste", new color(0, 255, 255), new PropRef()) },
            { 1020, ("Lime green", new color(0, 255, 0), new PropRef()) },
            { 1021, ("Camo", new color(58, 125, 21), new PropRef()) },
            { 1022, ("Grime", new color(127, 142, 96), new PropRef()) },
            { 1023, ("Lavender", new color(135, 128, 255), new PropRef()) },
            { 1024, ("Pastel light blue", new color(175, 221, 255), new PropRef()) },
            { 1025, ("Pastel orange", new color(255, 201, 201), new PropRef()) },
            { 1026, ("Pastel violet", new color(177, 167, 255), new PropRef()) },
            { 1027, ("Pastel blue-green", new color(204, 255, 204), new PropRef()) },
            { 1028, ("Pastel green", new color(199, 255, 199), new PropRef()) },
            { 1029, ("Pastel yellow", new color(255, 255, 204), new PropRef()) },
            { 1030, ("Pastel brown", new color(255, 204, 153), new PropRef()) },
            { 1031, ("Royal purple", new color(117, 69, 255), new PropRef()) },
            { 1032, ("Hot pink", new color(255, 0, 191), new PropRef()) },
        };
    }
}
