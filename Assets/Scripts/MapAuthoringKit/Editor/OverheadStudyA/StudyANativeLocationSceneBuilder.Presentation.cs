using System;
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
        private sealed class NativePresentationAssets
        {
            public StudyANativeGroundPresentationPlan Plan;
            public string ProfileJSON, ProfileSHA256;
            public Texture2D[] Textures;
            public Material GroundMaterial;
            public Sprite TargetGroundSprite;
            public Color GroundColor;
        }

        // Read-only source, importer and pure-plan preflight. Call before EnsureFolder or scene mutation.
        private static NativePresentationAssets ResolveNativePresentation(StudyANativeLocationSceneConfig config,
            StudyANativeLocationAssembly assembly, string recipeId, out string profileJSON)
        {
            profileJSON = null;
            if (string.IsNullOrEmpty(config.NativeGroundPresentationProfilePath) &&
                string.IsNullOrEmpty(config.NativeGroundPresentationProfileSHA256)) return null;
            GuardFile(config.NativeGroundPresentationProfilePath, config.NativeGroundPresentationProfileSHA256);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.NativeGroundPresentationProfilePath);
            Require(asset != null && !EditorUtility.IsDirty(asset), "Ground presentation profile is missing or dirty.");
            profileJSON = asset.text;
            var profile = JsonUtility.FromJson<StudyANativeGroundPresentationProfile>(profileJSON);
            var plan = StudyANativeGroundPresentation.BuildPlan(profile, assembly, recipeId);
            if (!plan.Applied) return null;
            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.CatalogPath);
            Require(catalogAsset != null && !EditorUtility.IsDirty(catalogAsset), "Original source catalog must be clean.");
            var original = JsonUtility.FromJson<StudyANativeLocationCatalog>(catalogAsset.text);
            Require(original != null && original.Locations != null, "Original presentation source catalog is invalid.");
            var textures = new Texture2D[plan.Placements.Length];
            for (var i = 0; i < plan.Placements.Length; i++)
            {
                var p = plan.Placements[i];
                var definition = original.Locations.SingleOrDefault(d => (d.Id == "C" || d.Id == "D") && d.GroundPath == p.SourceTexturePath);
                Require(definition != null && definition.GroundSHA256 == p.SourceTextureSHA256 &&
                    p.SourceTextureSHA256 == OriginalGroundHashes[Array.IndexOf(OriginalIds, definition.Id)] &&
                    definition.ExtentPx == p.SourceExtentPx, "Presentation may reference only the unchanged original C/D ground textures.");
                GuardFile(p.SourceTexturePath, p.SourceTextureSHA256); GuardFile(p.SourceTexturePath + ".meta", p.SourceTextureMetaSHA256);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p.SourceTexturePath);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.SourceTexturePath);
                var importer = AssetImporter.GetAtPath(p.SourceTexturePath) as TextureImporter;
                Require(sprite != null && texture != null && importer != null && !EditorUtility.IsDirty(sprite) &&
                    !EditorUtility.IsDirty(texture) && !EditorUtility.IsDirty(importer) && texture.width == p.SourceExtentPx.x &&
                    texture.height == p.SourceExtentPx.y && sprite.texture == texture && !sprite.packed &&
                    sprite.rect == new Rect(0, 0, p.SourceExtentPx.x, p.SourceExtentPx.y) &&
                    sprite.pivot == sprite.rect.size / 2 && Mathf.Abs(sprite.pixelsPerUnit - PPU) < .0001f &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && importer.spriteImportMode == SpriteImportMode.Single,
                    "Presentation source must retain native uncompressed Point/Single64PPU import without atlas packing or resampling.");
                using (var pixels = new GroundPixels(p.SourceTexturePath, p.SourceTextureSHA256, p.SourceExtentPx.x, p.SourceExtentPx.y)) { }
                textures[i] = texture;
            }
            var target = original.Locations.Single(d => d.Id == "C");
            GuardFile(target.PrefabPath, target.PrefabSHA256);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
            var grounds = new[] { NativeGroundRenderer(prefab) };
            Require(grounds.Length == 1 && grounds[0].GetComponentInParent<SortingGroup>() == null &&
                grounds[0].sortingLayerName == "MapGround" && grounds[0].sortingOrder == 0 &&
                grounds[0].sharedMaterial != null && !EditorUtility.IsDirty(grounds[0].sharedMaterial) &&
                grounds[0].sharedMaterial.renderQueue == 3000 &&
                SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("MapGround")) <
                    SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("WorldObjects")),
                "Presentation requires the original clean ground material and the established lower MapGround domain.");
            return new NativePresentationAssets { Plan = plan, Textures = textures, GroundMaterial = grounds[0].sharedMaterial,
                TargetGroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(target.GroundPath), GroundColor = grounds[0].color,
                ProfileJSON = profileJSON, ProfileSHA256 = FileHash(config.NativeGroundPresentationProfilePath) };
        }

        // Call on the fresh MapRoot before SaveScene. Sprite assets belong only to this invocation's fresh output.
        private static void ApplyNativePresentation(GameObject mapRoot, string ownedOutput,
            StudyANativeLocationSceneReport report, NativePresentationAssets presentation)
        {
            if (presentation == null) return;
            Require(mapRoot != null && mapRoot.GetComponentInParent<SortingGroup>() == null &&
                mapRoot.GetComponentInParent<StudyAIndependentObjectState>() == null && presentation.Plan.Applied,
                "Ground presentation must stay outside all owners and foot sorting groups.");
            Require(mapRoot.transform.position == Vector3.zero && mapRoot.transform.rotation == Quaternion.identity &&
                mapRoot.transform.lossyScale == Vector3.one, "Presentation requires the generated identity MapRoot.");
            var grounds = mapRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(r =>
                r.sprite == presentation.TargetGroundSprite && r.GetComponentInParent<StudyAIndependentObjectState>() == null &&
                r.GetComponentInParent<SortingGroup>() == null).ToArray();
            Require(grounds.Length == 1 && grounds[0].sortingLayerName == "MapGround" && grounds[0].sortingOrder == 0 &&
                grounds[0].sharedMaterial == presentation.GroundMaterial && grounds[0].color == presentation.GroundColor,
                "The generated C ground must preserve its original unowned ground material, color and sorting domain.");
            var targetGround = grounds[0];
            var plan = presentation.Plan;
            for (var i = 0; i < plan.Placements.Length; i++)
            {
                var p = plan.Placements[i];
                GuardFile(p.SourceTexturePath, p.SourceTextureSHA256); GuardFile(p.SourceTexturePath + ".meta", p.SourceTextureMetaSHA256);
                var path = AssetPath(ownedOutput + "/NativeGroundPresentation-" + i + ".asset");
                Require(!File.Exists(ProjectPath(path)) && !File.Exists(ProjectPath(path) + ".meta") &&
                    AssetDatabase.LoadMainAssetAtPath(path) == null, "Never overwrite an existing presentation sprite.");
                Sprite created = null;
                try
                {
                    created = Sprite.Create(presentation.Textures[i], new Rect(p.SourceRectPx.x, p.SourceRectPx.y,
                        p.SourceRectPx.width, p.SourceRectPx.height), new Vector2(.5f, .5f), PPU, 0,
                        SpriteMeshType.FullRect, Vector4.zero, false);
                    Require(created != null, "Native source-slice sprite creation failed.");
                    created.name = "NativeGroundPresentation-" + i;
                    AssetDatabase.CreateAsset(created, path);
                    AssetDatabase.SaveAssetIfDirty(created);
                    var saved = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Require(saved != null && AssetDatabase.Contains(saved) && AssetDatabase.IsMainAsset(saved) &&
                        saved.texture == presentation.Textures[i] && saved.rect == created.rect &&
                        saved.pivot == saved.rect.size / 2 && Mathf.Abs(saved.pixelsPerUnit - PPU) < .0001f,
                        "The native slice must be persisted as an owned sprite asset before scene serialization.");
                    var part = new GameObject("NativeGroundPresentation-" + i);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(part, mapRoot.scene);
                    part.layer = targetGround.gameObject.layer;
                    part.transform.SetParent(mapRoot.transform, false);
                    part.transform.localPosition = new Vector3(p.WorldCenterPx.x / PPU, p.WorldCenterPx.y / PPU, 0);
                    part.transform.localRotation = Quaternion.identity; part.transform.localScale = Vector3.one;
                    var renderer = part.AddComponent<SpriteRenderer>(); renderer.sprite = saved;
                    renderer.sharedMaterial = targetGround.sharedMaterial; renderer.color = targetGround.color;
                    renderer.sortingLayerID = targetGround.sortingLayerID; renderer.sortingOrder = 1;
                    renderer.drawMode = SpriteDrawMode.Simple; renderer.flipX = false; renderer.flipY = false;
                    Require(part.GetComponent<Collider2D>() == null && part.GetComponent<SortingGroup>() == null &&
                        part.GetComponent<StudyAIndependentObjectState>() == null, "A copied ground slice adds no collider, owner or foot unit.");
                }
                finally
                {
                    if (created != null && !AssetDatabase.Contains(created)) Object.DestroyImmediate(created);
                }
            }
            report.NativeGroundPresentationSHA256 = presentation.ProfileSHA256;
            report.NativeGroundPresentationSpriteCount = plan.Placements.Length;
            report.NativeGroundPresentationJSON = presentation.ProfileJSON;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapRoot.scene);
        }
    }
}
