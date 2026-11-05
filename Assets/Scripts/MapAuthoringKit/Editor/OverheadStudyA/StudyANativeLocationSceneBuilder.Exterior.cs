using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using FantasyRoyale.Playtest.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    public static partial class StudyANativeLocationSceneBuilder
    {
        private const string NativeExteriorSourcePath = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/IsolatedStyleFamily-v2-20261004-215942621/Sources/bank_west_style.png";
        private const string NativeExteriorSourceSHA256 = "b975b0da6456582c76ce2831dd4b820b7c6c87077484abb1f4f1ed0a5118161a";
        private const string NativeExteriorSourceMetaSHA256 = "149834344ab296fedc0aa14d1e0859e1d76e9948f04767252884e2f1cd2652c8";

        private sealed class NativeExteriorPresentationAssets
        {
            public StudyANativeExteriorPresentationPlan Plan;
            public string ProfileJSON, ProfileSHA256;
            public Texture2D Texture;
            public Sprite GroundSprite;
            public Material GroundMaterial;
            public Color GroundColor;
        }

        // Read-only preflight precedes EnsureFolder, copied scenes and every persisted slice.
        private static NativeExteriorPresentationAssets ResolveNativeExteriorPresentation(StudyANativeLocationSceneConfig config,
            StudyANativeLocationAssembly assembly, string recipeId, StudyANativeLocationGeometry.Plan geometry, out string profileJSON)
        {
            profileJSON = null;
            if (string.IsNullOrEmpty(config.NativeExteriorPresentationProfilePath) &&
                string.IsNullOrEmpty(config.NativeExteriorPresentationProfileSHA256)) return null;
            GuardFile(config.NativeExteriorPresentationProfilePath, config.NativeExteriorPresentationProfileSHA256);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.NativeExteriorPresentationProfilePath);
            Require(asset != null && !EditorUtility.IsDirty(asset), "Exterior presentation profile is missing or dirty.");
            profileJSON = asset.text;
            var profile = JsonUtility.FromJson<StudyANativeExteriorPresentationProfile>(profileJSON);
            var plan = StudyANativeExteriorPresentation.BuildPlan(profile, assembly, recipeId, geometry);
            if (!plan.Applied) return null;
            Require(profile.SourceTexturePath == NativeExteriorSourcePath &&
                profile.SourceTextureSHA256 == NativeExteriorSourceSHA256 &&
                profile.SourceTextureMetaSHA256 == NativeExteriorSourceMetaSHA256 &&
                profile.SourceExtentPx == new Vector2Int(768, 640) &&
                profile.SourceRectPx == new RectInt(389, 384, 31, 64),
                "Exterior presentation may use only the pinned original bank's native outside face.");
            GuardFile(profile.SourceTexturePath, profile.SourceTextureSHA256);
            GuardFile(profile.SourceTexturePath + ".meta", profile.SourceTextureMetaSHA256);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(profile.SourceTexturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(profile.SourceTexturePath);
            var importer = AssetImporter.GetAtPath(profile.SourceTexturePath) as TextureImporter;
            Require(sprite != null && texture != null && importer != null &&
                !EditorUtility.IsDirty(sprite) && !EditorUtility.IsDirty(texture) && !EditorUtility.IsDirty(importer) &&
                texture.width == profile.SourceExtentPx.x && texture.height == profile.SourceExtentPx.y &&
                sprite.texture == texture && !sprite.packed && sprite.rect == new Rect(0, 0, 768, 640) &&
                sprite.pivot == sprite.rect.size / 2 && Mathf.Abs(sprite.pixelsPerUnit - PPU) < .0001f &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.spriteImportMode == SpriteImportMode.Single && importer.npotScale == TextureImporterNPOTScale.None,
                "Exterior source retains native uncompressed Point/Single64PPU import without packing or resampling.");
            VerifyNativeExteriorPixels(profile, plan, geometry);

            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.CatalogPath);
            Require(catalogAsset != null && !EditorUtility.IsDirty(catalogAsset), "Original exterior source catalog must be clean.");
            var original = JsonUtility.FromJson<StudyANativeLocationCatalog>(catalogAsset.text);
            Require(original != null && original.Locations != null, "Original exterior source catalog is invalid.");
            GuardNativeExteriorLayerDomain(original);
            var a = original.Locations.Single(d => d.Id == "A");
            Require(a.PrefabSHA256 == OriginalPrefabHashes[0] && a.GroundSHA256 == OriginalGroundHashes[0],
                "Exterior host material requires the unchanged original A source.");
            GuardFile(a.PrefabPath, a.PrefabSHA256); GuardFile(a.GroundPath, a.GroundSHA256);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(a.PrefabPath);
            Require(prefab != null && !EditorUtility.IsDirty(prefab), "Original exterior ground prefab is missing or dirty.");
            var grounds = prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sprite != null &&
                AssetDatabase.GetAssetPath(r.sprite) == a.GroundPath &&
                r.GetComponentInParent<StudyAIndependentObjectState>() == null).ToArray();
            Require(grounds.Length == 1 && grounds[0].GetComponentInParent<SortingGroup>() == null &&
                grounds[0].sortingLayerName == "MapGround" && grounds[0].sortingOrder == 0 &&
                grounds[0].sharedMaterial != null && !EditorUtility.IsDirty(grounds[0].sharedMaterial) &&
                grounds[0].sharedMaterial.renderQueue == 3000 &&
                SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("MapGround")) <
                    SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("WorldObjects")),
                "Exterior uses the original clean ground material below the established foot-depth domain.");
            return new NativeExteriorPresentationAssets { Plan = plan, Texture = texture,
                GroundSprite = grounds[0].sprite, GroundMaterial = grounds[0].sharedMaterial, GroundColor = grounds[0].color,
                ProfileJSON = profileJSON, ProfileSHA256 = FileHash(config.NativeExteriorPresentationProfilePath) };
        }

        private static void GuardNativeExteriorLayerDomain(StudyANativeLocationCatalog original)
        {
            foreach (var definition in original.Locations)
            {
                var index = Array.IndexOf(OriginalIds, definition.Id);
                Require(index >= 0 && definition.PrefabSHA256 == OriginalPrefabHashes[index] &&
                    definition.GroundSHA256 == OriginalGroundHashes[index], "Exterior layer preflight requires the original A/B/C/D source pins.");
                GuardFile(definition.PrefabPath, definition.PrefabSHA256);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
                Require(prefab != null && !EditorUtility.IsDirty(prefab), "Exterior layer source prefab is missing or dirty.");
                foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                    r.sprite != null && r.sortingLayerName == "MapGround"))
                {
                    var isGround = AssetDatabase.GetAssetPath(renderer.sprite) == definition.GroundPath &&
                        renderer.GetComponentInParent<StudyAIndependentObjectState>() == null;
                    Require(isGround ? renderer.sortingOrder == 0 : renderer.sortingOrder > 4,
                        "Original ground remains order0; every source water, bank, shadow and contact layer must stay above exterior orders2/3/4.");
                }
            }
        }

        // A bank PNG is transparent; unlike GroundPixels, this decoder never requires a filled grass plane.
        private static void VerifyNativeExteriorPixels(StudyANativeExteriorPresentationProfile profile,
            StudyANativeExteriorPresentationPlan plan, StudyANativeLocationGeometry.Plan geometry)
        {
            Texture2D decoded = null;
            try
            {
                var bytes = File.ReadAllBytes(ProjectPath(profile.SourceTexturePath));
                VerifyRawNativePNG(bytes, profile.SourceExtentPx.x, profile.SourceExtentPx.y, 6);
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                Require(ImageConversion.LoadImage(decoded, bytes, false) && decoded.width == 768 && decoded.height == 640,
                    "Native exterior PNG decoding failed or changed dimensions.");
                var pixels = decoded.GetPixels32();
                Require(pixels.Length == 768 * 640, "Native exterior source pixel extent changed.");
                // The entire source phase keeps its opaque floor lip and at most nine pixels of outside earth.
                for (var y = 384; y < 448; y++)
                {
                    Require(pixels[y * 768 + 389].a == 255, "Native outside face must meet the floor at every source row.");
                    for (var x = 398; x < 420; x++)
                        Require(pixels[y * 768 + x].a == 0, "The pinned face must remain within nine outside pixels.");
                }
                foreach (var placement in plan.Placements)
                {
                    var source = placement.SourceRectPx; var target = placement.TargetRectWorldPx;
                    Require(placement.SourceTexturePath == profile.SourceTexturePath &&
                        placement.SourceTextureSHA256 == profile.SourceTextureSHA256 &&
                        placement.SourceTextureMetaSHA256 == profile.SourceTextureMetaSHA256 &&
                        placement.SourceExtentPx == profile.SourceExtentPx,
                        "Every exterior slice retains the same source pins.");
                    Require(placement.SortingOrder == (placement.Side == "S" ? 3 : placement.Side == "N" ? 4 : 2),
                        "Exterior directional orders2/3/4 stay below all original non-ground MapGround layers.");
                    var quarter = placement.RotationDegrees / 90;
                    Require(placement.RotationDegrees >= 0 && placement.RotationDegrees <= 270 &&
                        placement.RotationDegrees % 90 == 0 &&
                        target.width == (quarter % 2 == 0 ? source.width : source.height) &&
                        target.height == (quarter % 2 == 0 ? source.height : source.width),
                        "Exterior placement must use an exact native quarter turn without resampling.");
                    for (var y = 0; y < source.height; y++)
                    for (var x = 0; x < source.width; x++)
                    {
                        if (pixels[(source.y + y) * 768 + source.x + x].a == 0) continue;
                        int dx, dy;
                        switch (quarter)
                        {
                            case 0: dx = x; dy = y; break;
                            case 1: dx = source.height - 1 - y; dy = x; break;
                            case 2: dx = source.width - 1 - x; dy = source.height - 1 - y; break;
                            default: dx = y; dy = source.width - 1 - x; break;
                        }
                        var point = new Vector2Int(target.x + dx, target.y + dy);
                        Require(!geometry.FloorsPx.Any(r => r.Contains(point)) &&
                            geometry.PhysicalPerimeterPx.Any(r => r.Contains(point)),
                            "Every visible outside-face pixel stays outside the floor and inside the existing physical wall.");
                    }
                }
            }
            finally { if (decoded != null) Object.DestroyImmediate(decoded); }
        }

        private static string ExteriorSliceKey(RectInt rect) => rect.x + "-" + rect.y + "-" + rect.width + "-" + rect.height;

        // Only this invocation's fresh output owns persisted sprites; original PNG/importer/material stay read-only.
        private static void ApplyNativeExteriorPresentation(GameObject mapRoot, string ownedOutput,
            StudyANativeLocationSceneReport report, NativeExteriorPresentationAssets exterior)
        {
            if (exterior == null) return;
            Require(mapRoot != null && exterior.Plan.Applied &&
                mapRoot.GetComponentInParent<SortingGroup>() == null &&
                mapRoot.GetComponentInParent<StudyAIndependentObjectState>() == null &&
                mapRoot.transform.position == Vector3.zero && mapRoot.transform.rotation == Quaternion.identity &&
                mapRoot.transform.lossyScale == Vector3.one, "Exterior host requires the generated identity MapRoot outside all owners.");
            var grounds = mapRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                r.sprite == exterior.GroundSprite && r.GetComponentInParent<StudyAIndependentObjectState>() == null &&
                r.GetComponentInParent<SortingGroup>() == null).ToArray();
            Require(grounds.Length == 1 && grounds[0].sortingLayerName == "MapGround" && grounds[0].sortingOrder == 0 &&
                grounds[0].sharedMaterial == exterior.GroundMaterial && grounds[0].color == exterior.GroundColor,
                "Copied A ground must preserve its original unowned ground material and sorting domain.");
            GuardFile(NativeExteriorSourcePath, NativeExteriorSourceSHA256);
            GuardFile(NativeExteriorSourcePath + ".meta", NativeExteriorSourceMetaSHA256);
            var ownerCount = mapRoot.GetComponentsInChildren<StudyAIndependentObjectState>(true).Length;
            var groupCount = mapRoot.GetComponentsInChildren<SortingGroup>(true).Length;
            var colliderCount = mapRoot.GetComponentsInChildren<Collider2D>(true).Length;
            var navigation = mapRoot.GetComponentsInChildren<StudyAIndependentNavigation>(true);
            Require(navigation.Length == 1 && navigation[0].FrameWorldBounds == report.FrameWorldBounds,
                "Exterior must preserve the original navigation frame.");
            var ground = grounds[0]; var slices = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            Require(mapRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                r.sprite != null && r.sortingLayerName == "MapGround").All(r => r.sortingOrder < 2 || r.sortingOrder > 4),
                "Before adding outside faces, the copied map must leave presentation orders2/3/4 unused.");
            foreach (var source in exterior.Plan.Placements.Select(p => p.SourceRectPx).Distinct())
            {
                var key = ExteriorSliceKey(source);
                var path = AssetPath(ownedOutput + "/NativeExteriorPresentation-" + key + ".asset");
                Require(!File.Exists(ProjectPath(path)) && !File.Exists(ProjectPath(path) + ".meta") &&
                    AssetDatabase.LoadMainAssetAtPath(path) == null, "Never overwrite an exterior presentation sprite.");
                Sprite created = null;
                try
                {
                    created = Sprite.Create(exterior.Texture, new Rect(source.x, source.y, source.width, source.height),
                        new Vector2(.5f, .5f), PPU, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                    Require(created != null, "Native exterior source slice creation failed.");
                    created.name = "NativeExteriorPresentation-" + key;
                    AssetDatabase.CreateAsset(created, path);
                    AssetDatabase.SaveAssetIfDirty(created);
                    // This is a serialized-asset readback, not a domain-reload or runtime rendering claim.
                    var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Require(saved != null && AssetDatabase.Contains(saved) && AssetDatabase.IsMainAsset(saved) &&
                        saved.texture == exterior.Texture && saved.rect == created.rect &&
                        saved.pivot == saved.rect.size / 2 && Mathf.Abs(saved.pixelsPerUnit - PPU) < .0001f,
                        "Persist the native outside-face slice before serializing the copied scene.");
                    slices.Add(key, saved);
                }
                finally { if (created != null && !AssetDatabase.Contains(created)) Object.DestroyImmediate(created); }
            }
            for (var i = 0; i < exterior.Plan.Placements.Length; i++)
            {
                var placement = exterior.Plan.Placements[i];
                var part = new GameObject("NativeExteriorPresentation-" + placement.LocationId + "-" + placement.Side + "-" + i);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(part, mapRoot.scene);
                part.layer = ground.gameObject.layer; part.transform.SetParent(mapRoot.transform, false);
                part.transform.localPosition = new Vector3(placement.WorldCenterPx.x / PPU, placement.WorldCenterPx.y / PPU, 0);
                part.transform.localRotation = Quaternion.Euler(0, 0, placement.RotationDegrees);
                part.transform.localScale = Vector3.one;
                var renderer = part.AddComponent<SpriteRenderer>(); renderer.sprite = slices[ExteriorSliceKey(placement.SourceRectPx)];
                renderer.sharedMaterial = ground.sharedMaterial; renderer.color = ground.color;
                renderer.sortingLayerID = ground.sortingLayerID; renderer.sortingOrder = placement.SortingOrder;
                renderer.drawMode = SpriteDrawMode.Simple; renderer.flipX = false; renderer.flipY = false;
                Require(part.GetComponent<Collider2D>() == null && part.GetComponent<SortingGroup>() == null &&
                    part.GetComponent<StudyAIndependentObjectState>() == null,
                    "Outside-face presentation adds no collider, owner or registered foot unit.");
            }
            Require(mapRoot.GetComponentsInChildren<StudyAIndependentObjectState>(true).Length == ownerCount &&
                mapRoot.GetComponentsInChildren<SortingGroup>(true).Length == groupCount &&
                mapRoot.GetComponentsInChildren<Collider2D>(true).Length == colliderCount &&
                navigation[0].FrameWorldBounds == report.FrameWorldBounds,
                "Outside faces must preserve every original owner, foot group, collider and navigation frame.");
            report.NativeExteriorPresentationSHA256 = exterior.ProfileSHA256;
            report.NativeExteriorPresentationSpriteCount = exterior.Plan.Placements.Length;
            report.NativeExteriorPresentationAssetCount = slices.Count;
            report.NativeExteriorPresentationJSON = exterior.ProfileJSON;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapRoot.scene);
        }
    }
}
