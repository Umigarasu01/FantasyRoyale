using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    [Serializable]
    public sealed class StudyAGroundIntegrationExperimentReport
    {
        public bool Enabled, TreeRenderersUnchanged, CollidersUnchanged, GroundTilemapUnchanged;
        public bool UnderstoryAlphaUnchanged, ContactAlphaUnchanged, SharedMasksUnchanged, SourcePngsUnchanged, ContactLayerStateUnchanged;
        public int Version = 3, PixelsPerUnit = 64, ParticipatingTreeCount, GrassOverlayPixels;
        public int UnderstoryRgbChangedPixels, ContactRgbChangedPixels, ChangesOutsideRegion, ForbiddenOverlayPixels;
        public Rect Region;
        public float GrassOverlayAreaUnits, GrassOverlayRegionRatio;
        public string GrassOverlayPath, UnderstoryPath, ContactPath, GrassOverlayAlphaSha256;
        public string UnderstoryAlphaBefore, UnderstoryAlphaAfter, ContactAlphaBefore, ContactAlphaAfter;
        public string UnderstoryRgbaSha256, ContactRgbaSha256, GrassOverlayRgbaSha256;
        public string RendererStateBefore, RendererStateAfter, ColliderStateBefore, ColliderStateAfter;
        public string GroundStateBefore, GroundStateAfter, SharedMasksBefore, SharedMasksAfter;
        public string ContactLayerStateBefore, ContactLayerStateAfter;
        public string[] SourcePngPaths, SourcePngSha256, FootSelections;
        public string Recipe;
    }

    /// <summary>Comparison3: only grass/ground-contact RGB around unchanged native tree feet.</summary>
    public static class StudyAGroundIntegrationExperiment
    {
        public const int Version = 3, PixelsPerUnit = 64;
        public const string RootName = "GroundIntegrationExperiment_NoCollision";
        public static readonly Rect Region = new Rect(17.78125f, 2f, 12f, 12f);
        private sealed class Raster { public int Width, Height; public Color32[] Pixels; }
        private sealed class Foot
        {
            public Rect Bounds;
            public Color32 RootColor;
            public float Rx, Ry;
            public string Key;
        }

        public static StudyAGroundIntegrationExperimentReport Apply(Transform obstacleRoot, Transform decorativeRoot,
            Material material, string outputRoot, StudyABlockAssembly assembly, StudyABridgeRiverFinePlan finePlan,
            Dictionary<string, StudyAStarterProp> approvedProps, StudyAForestArtFinishReport baselineFinish,
            Tilemap ground, StudyASoilMask roadMask)
        {
            if (obstacleRoot == null || decorativeRoot == null || material == null || assembly == null || finePlan == null ||
                approvedProps == null || baselineFinish == null || !baselineFinish.Enabled || ground == null || roadMask == null)
                throw new ArgumentNullException("Comparison3 requires the completed baseline forest, native props, ground and exact road mask.");
            var scene = obstacleRoot.gameObject.scene;
            if (EditorUtility.IsPersistent(obstacleRoot) || EditorUtility.IsPersistent(decorativeRoot) || EditorUtility.IsPersistent(ground) ||
                !scene.IsValid() || !string.IsNullOrEmpty(scene.path) || scene != decorativeRoot.gameObject.scene || scene != ground.gameObject.scene ||
                decorativeRoot.Find(RootName) != null || decorativeRoot.position != Vector3.zero || decorativeRoot.rotation != Quaternion.identity ||
                decorativeRoot.lossyScale != Vector3.one || ground.transform.position != Vector3.zero || ground.transform.rotation != Quaternion.identity || ground.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("Comparison3 accepts only a fresh unsaved generated Scene with identity ground roots.");
            var n = (long)finePlan.Width * finePlan.Height;
            if (finePlan.Assembly == null || finePlan.Assembly.StableHash() != assembly.StableHash() || finePlan.PixelsPerUnit != PixelsPerUnit ||
                finePlan.Width != 2048 || finePlan.Height != 1024 || finePlan.BridgeStructureVersion != 1 ||
                finePlan.VisibleWaterMask == null || finePlan.WaterCollisionMask == null || finePlan.FineFreeWalkMask == null ||
                finePlan.VisibleWaterMask.LongLength != n || finePlan.WaterCollisionMask.LongLength != n || finePlan.FineFreeWalkMask.LongLength != n ||
                finePlan.DeckBounds == null || (finePlan.BridgeBodyCollisionMask != null && finePlan.BridgeBodyCollisionMask.LongLength != n) ||
                roadMask.Width != finePlan.Width || roadMask.Height != finePlan.Height || roadMask.Origin != finePlan.Origin ||
                roadMask.PixelsPerUnit != PixelsPerUnit || roadMask.Pixels == null || roadMask.Pixels.LongLength != n ||
                !finePlan.WorldBounds.Contains(Region.min) || !finePlan.WorldBounds.Contains(Region.max - Vector2.one / PixelsPerUnit))
                throw new ArgumentException("Comparison3 is limited to the existing BridgeStructure1 native frame and reviewed12x12 region.");
            RequireFreshOutput(outputRoot);
            var runRoot = outputRoot.Substring(0, outputRoot.LastIndexOf('/'));
            var finishRoot = decorativeRoot.Find(StudyAForestArtFinishRenderer.RootName);
            if (finishRoot == null) throw new InvalidOperationException("Baseline forest finish must run once before comparison3.");
            var under = RequireGroundRenderer(finishRoot, "ContinuousUndergrowth_NoCollision", 12, finePlan, baselineFinish.UnderstoryTexturePath, runRoot);
            var shadow = RequireGroundRenderer(finishRoot, "HardContactShadow_NoCollision", 13, finePlan, baselineFinish.ContactShadowTexturePath, runRoot);
            var groundRenderer = ground.GetComponent<TilemapRenderer>();
            if (groundRenderer == null || groundRenderer.sharedMaterial == null || groundRenderer.sortingLayerName != "MapGround" || groundRenderer.sortingOrder != 0)
                throw new InvalidDataException("Grass overlay requires the existing MapGround0 tilemap material.");

            var cache = new Dictionary<string, Raster>(StringComparer.Ordinal);
            var sourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            var underRaster = Read(AssetDatabase.GetAssetPath(under.sprite), cache, sourceHashes);
            var shadowRaster = Read(AssetDatabase.GetAssetPath(shadow.sprite), cache, sourceHashes);
            if (underRaster.Width != finePlan.Width || underRaster.Height != finePlan.Height || shadowRaster.Width != finePlan.Width || shadowRaster.Height != finePlan.Height)
                throw new InvalidDataException("Existing contact layers must retain the complete fine frame.");
            var feet = ReadFeet(obstacleRoot, approvedProps, baselineFinish, cache, sourceHashes);
            var regionFeet = feet.Where(f => Expand(f.Bounds, 1.25f).Overlaps(Region)).ToArray();
            if (regionFeet.Length == 0) throw new InvalidDataException("Comparison region contains no approved baseline tree feet.");
            var forbidden = Forbidden(assembly, finePlan, roadMask, obstacleRoot, approvedProps);
            var beforeRenderers = RendererState(scene.GetRootGameObjects(), under, shadow);
            var beforeContactLayers = ContactLayerState(under, shadow);
            var beforeColliders = ColliderState(scene.GetRootGameObjects());
            var beforeGround = Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(ground)));
            var beforeMasks = MaskState(finePlan, roadMask);
            var report = new StudyAGroundIntegrationExperimentReport
            {
                Enabled = true, Region = Region, ParticipatingTreeCount = regionFeet.Length,
                RendererStateBefore = beforeRenderers, ColliderStateBefore = beforeColliders, GroundStateBefore = beforeGround, SharedMasksBefore = beforeMasks,
                ContactLayerStateBefore = beforeContactLayers,
                UnderstoryAlphaBefore = AlphaHash(underRaster.Pixels), ContactAlphaBefore = AlphaHash(shadowRaster.Pixels),
                FootSelections = regionFeet.Select(f => f.Key + "|" + f.Bounds.ToString("R") + "|" + f.RootColor.ToString()).ToArray()
            };
            var underPixels = (Color32[])underRaster.Pixels.Clone(); var contactPixels = (Color32[])shadowRaster.Pixels.Clone();
            const int overlayWidth = 768, overlayHeight = 768;
            var overlay = new Color32[overlayWidth * overlayHeight];
            for (var y = 0; y < overlayHeight; y++) for (var x = 0; x < overlayWidth; x++)
            {
                var point = Region.min + new Vector2((x + .5f) / PixelsPerUnit, (y + .5f) / PixelsPerUnit);
                var fx = Mathf.FloorToInt((point.x - finePlan.Origin.x) * PixelsPerUnit);
                var fy = Mathf.FloorToInt((point.y - finePlan.Origin.y) * PixelsPerUnit);
                var i = fy * finePlan.Width + fx;
                if (forbidden[i] != 0) continue;
                Foot foot; float influence;
                if (!NearestFoot(point, regionFeet, out foot, out influence)) continue;
                Color32 native;
                if (!GrassTexel(ground, point, cache, sourceHashes, out native)) continue;
                // Reuse the actual native grass texel pattern; no random pixels, blur or texture synthesis.
                var integrated = Blend(native, foot.RootColor, .24f * influence);
                overlay[y * overlayWidth + x] = Shade(integrated, 1f - .035f * influence, 255);
                report.GrassOverlayPixels++;
                if (underPixels[i].a != 0)
                {
                    var shade = Mathf.Clamp(underPixels[i].g / 131f, .65f, 1f);
                    var next = Blend(underPixels[i], Shade(integrated, shade, underPixels[i].a), .70f * influence);
                    next.a = underPixels[i].a;
                    if (!EqualRgb(next, underPixels[i])) { underPixels[i] = next; report.UnderstoryRgbChangedPixels++; }
                }
                if (contactPixels[i].a != 0)
                {
                    var next = Blend(contactPixels[i], Shade(foot.RootColor, .42f, contactPixels[i].a), .45f * influence);
                    next.a = contactPixels[i].a;
                    if (!EqualRgb(next, contactPixels[i])) { contactPixels[i] = next; report.ContactRgbChangedPixels++; }
                }
            }
            if (report.GrassOverlayPixels == 0 || report.UnderstoryRgbChangedPixels == 0)
                throw new InvalidDataException("Comparison3 has no measurable local ground/contact effect.");
            report.GrassOverlayRegionRatio = report.GrassOverlayPixels / (float)overlay.Length;
            report.GrassOverlayAreaUnits = report.GrassOverlayPixels / (float)(PixelsPerUnit * PixelsPerUnit);
            if (report.GrassOverlayRegionRatio > .20f) throw new InvalidDataException("Local foot treatment exceeds20percent of the reviewed region.");
            ValidateChangedPixels(underRaster.Pixels, underPixels, shadowRaster.Pixels, contactPixels, overlay, forbidden, finePlan, report);
            report.UnderstoryAlphaAfter = AlphaHash(underPixels); report.ContactAlphaAfter = AlphaHash(contactPixels);
            report.UnderstoryAlphaUnchanged = report.UnderstoryAlphaBefore == report.UnderstoryAlphaAfter;
            report.ContactAlphaUnchanged = report.ContactAlphaBefore == report.ContactAlphaAfter;
            if (!report.UnderstoryAlphaUnchanged || !report.ContactAlphaUnchanged || report.ChangesOutsideRegion != 0 || report.ForbiddenOverlayPixels != 0)
                throw new InvalidDataException("Comparison3 changed alpha, escaped the region or covered protected land/water/deck.");

            Directory.CreateDirectory(ProjectPath(outputRoot));
            report.GrassOverlayPath = outputRoot + "/native-grass-contact.png";
            report.UnderstoryPath = outputRoot + "/existing-understory-rgb.png";
            report.ContactPath = outputRoot + "/existing-contact-rgb.png";
            var grassSprite = WriteSprite(overlay, overlayWidth, overlayHeight, report.GrassOverlayPath);
            var underSprite = WriteSprite(underPixels, finePlan.Width, finePlan.Height, report.UnderstoryPath);
            var contactSprite = WriteSprite(contactPixels, finePlan.Width, finePlan.Height, report.ContactPath);
            var root = new GameObject(RootName); root.transform.SetParent(decorativeRoot, false);
            var go = new GameObject("NativeGrassFootContact_NoCollision"); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(Region.xMin, Region.yMin, 0);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = grassSprite; renderer.sharedMaterial = groundRenderer.sharedMaterial;
            renderer.sortingLayerName = "MapGround"; renderer.sortingOrder = 1; renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            under.sprite = underSprite; shadow.sprite = contactSprite;
            report.RendererStateAfter = RendererState(scene.GetRootGameObjects(), under, shadow, renderer);
            report.ContactLayerStateAfter = ContactLayerState(under, shadow);
            report.ContactLayerStateUnchanged = beforeContactLayers == report.ContactLayerStateAfter;
            report.ColliderStateAfter = ColliderState(scene.GetRootGameObjects());
            report.GroundStateAfter = Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(ground)));
            report.SharedMasksAfter = MaskState(finePlan, roadMask);
            report.TreeRenderersUnchanged = beforeRenderers == report.RendererStateAfter;
            report.CollidersUnchanged = beforeColliders == report.ColliderStateAfter;
            report.GroundTilemapUnchanged = beforeGround == report.GroundStateAfter;
            report.SharedMasksUnchanged = beforeMasks == report.SharedMasksAfter;
            report.SourcePngsUnchanged = sourceHashes.All(p => FileHash(ProjectPath(p.Key)) == p.Value);
            if (!report.TreeRenderersUnchanged || !report.CollidersUnchanged || !report.GroundTilemapUnchanged || !report.SharedMasksUnchanged || !report.SourcePngsUnchanged || !report.ContactLayerStateUnchanged || root.GetComponentsInChildren<Collider2D>(true).Length != 0)
                throw new InvalidDataException("Comparison3 mutated a baseline renderer, collision, tilemap, mask or source PNG.");
            report.SourcePngPaths = sourceHashes.Keys.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            report.SourcePngSha256 = report.SourcePngPaths.Select(p => sourceHashes[p]).ToArray();
            report.GrassOverlayAlphaSha256 = AlphaHash(overlay);
            report.GrassOverlayRgbaSha256 = RgbaHash(overlay); report.UnderstoryRgbaSha256 = RgbaHash(underPixels); report.ContactRgbaSha256 = RgbaHash(contactPixels);
            report.Recipe = "Comparison3 only: unchanged baseline tree sprites/tints/count/poses and physics; native64 grass texels near approved native tree feet blend24percent maximum toward the actual tinted native root brown, with three coherent strength bands. Existing undergrowth/contact RGB changes only in the same reviewed12x12 region; every original alpha byte is unchanged. No random dots/noise, blur, concept-image texels, soil repaint or foliage replacement. MapGround1 grass overlay is binary alpha and excludes shared visible/collision water, BridgeBody/deck, fine soil, macro soil/roads, Actor route bands, walking ports, spawn and POI feet. Existing MapGround12/13 poses/orders/materials retained. Effect area and all baseline hashes are reported; actual pixel review remains required.";
            return report;
        }

        private static Foot[] ReadFeet(Transform root, Dictionary<string, StudyAStarterProp> approved,
            StudyAForestArtFinishReport finish, Dictionary<string, Raster> cache, Dictionary<string, string> hashes)
        {
            if (finish.Tints == null || finish.Tints.Length != finish.TreeTintCount) throw new InvalidDataException("Baseline tree tint receipt is incomplete.");
            var records = finish.Tints.ToDictionary(t => t.Hierarchy, StringComparer.Ordinal);
            var result = new List<Foot>();
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null))
            {
                StudyAForestArtTintRecord record;
                if (!records.TryGetValue(Hierarchy(r.transform), out record)) continue;
                var p = approved.Values.FirstOrDefault(p => p != null && p.Sprite == r.sprite && (p.Metrics.Role == "treeLarge" || p.Metrics.Role == "treeMedium"));
                if (p == null || r.transform.lossyScale != Vector3.one || r.transform.rotation != Quaternion.identity || r.HasPropertyBlock() || r.flipX || r.flipY ||
                    !r.color.Equals(record.Tint) || AssetDatabase.GetAssetPath(r.sprite) != record.SpritePath || (Vector2)r.transform.position != record.FootAnchor)
                    throw new InvalidDataException("Comparison3 must retain every baseline native tree sprite/tint/anchor.");
                var path = AssetDatabase.GetAssetPath(r.sprite); var raster = Read(path, cache, hashes);
                if (r.sprite.pixelsPerUnit != PixelsPerUnit || r.sprite.rect.position != Vector2.zero || r.sprite.rect.width != raster.Width || r.sprite.rect.height != raster.Height)
                    throw new InvalidDataException("Tree root colors require the native single-sprite PNG.");
                var local = p.Metrics.LocalFootprint;
                var x0 = Mathf.Clamp(Mathf.FloorToInt(local.xMin * PixelsPerUnit + r.sprite.pivot.x), 0, raster.Width);
                var x1 = Mathf.Clamp(Mathf.CeilToInt(local.xMax * PixelsPerUnit + r.sprite.pivot.x), 0, raster.Width);
                var y0 = Mathf.Clamp(Mathf.FloorToInt(local.yMin * PixelsPerUnit + r.sprite.pivot.y), 0, raster.Height);
                var y1 = Mathf.Clamp(Mathf.CeilToInt(local.yMax * PixelsPerUnit + r.sprite.pivot.y), 0, raster.Height);
                var browns = new List<Color32>();
                for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++)
                { var c = raster.Pixels[y * raster.Width + x]; if (c.a == 255 && c.r > c.g && c.g > c.b) browns.Add(c); }
                if (browns.Count == 0) throw new InvalidDataException("Approved physical tree foot contains no native opaque brown root texels.");
                var nativeRoot = browns.GroupBy(c => c.r + "," + c.g + "," + c.b).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().First();
                result.Add(new Foot
                {
                    Bounds = Translate(local, record.FootAnchor), RootColor = Multiply(nativeRoot, r.color), Key = record.Hierarchy,
                    Rx = Mathf.Clamp(local.width * .85f + .18f, .50f, 1.05f), Ry = Mathf.Clamp(local.height * .65f + .18f, .25f, .50f)
                });
            }
            if (result.Count != finish.TreeTintCount) throw new InvalidDataException("Comparison3 could not identify all baseline trees without replacement.");
            return result.OrderBy(f => f.Key, StringComparer.Ordinal).ToArray();
        }
        private static bool NearestFoot(Vector2 point, Foot[] feet, out Foot selected, out float influence)
        {
            selected = null; influence = 0; var nearest = float.PositiveInfinity;
            foreach (var f in feet)
            {
                var d = point - f.Bounds.center; var q = d.x * d.x / (f.Rx * f.Rx) + d.y * d.y / (f.Ry * f.Ry);
                if (q <= 1f && q < nearest) { nearest = q; selected = f; }
            }
            if (selected == null) return false;
            influence = nearest <= .30f ? 1f : nearest <= .65f ? .65f : .30f;
            return true;
        }
        private static bool GrassTexel(Tilemap ground, Vector2 point, Dictionary<string, Raster> cache, Dictionary<string, string> hashes, out Color32 color)
        {
            color = default(Color32); var cell = ground.WorldToCell(point); var sprite = ground.GetSprite(cell);
            if (sprite == null) return false;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (!path.StartsWith(OverheadStudyASettings.ArtRoot + "/DerivedGroundR2/grass_", StringComparison.Ordinal)) return false;
            if (ground.GetTransformMatrix(cell) != Matrix4x4.identity || ground.tileAnchor != new Vector3(.5f, .5f, 0) ||
                ground.layoutGrid.cellSize != Vector3.one || sprite.pixelsPerUnit != PixelsPerUnit || sprite.pivot != new Vector2(32, 32) ||
                sprite.rect.position != Vector2.zero || sprite.rect.width != 64 || sprite.rect.height != 64)
                throw new InvalidDataException("Native grass texel sampling requires unchanged1u64px cells/pivots/transforms.");
            var raster = Read(path, cache, hashes);
            if (raster.Width != 64 || raster.Height != 64) throw new InvalidDataException("Approved grass PNG is not native64.");
            var origin = ground.CellToWorld(cell);
            var x = Mathf.Clamp(Mathf.FloorToInt((point.x - origin.x) * PixelsPerUnit), 0, 63);
            var y = Mathf.Clamp(Mathf.FloorToInt((point.y - origin.y) * PixelsPerUnit), 0, 63);
            var tint = ground.color * ground.GetColor(cell);
            if (tint.a != 1f || raster.Pixels[y * 64 + x].a != 255) throw new InvalidDataException("Comparison3 only overlays fully opaque existing grass.");
            color = Multiply(raster.Pixels[y * 64 + x], tint); return true;
        }
        private static byte[] Forbidden(StudyABlockAssembly assembly, StudyABridgeRiverFinePlan fine, StudyASoilMask road,
            Transform obstacles, Dictionary<string, StudyAStarterProp> approved)
        {
            var mask = (byte[])fine.VisibleWaterMask.Clone();
            for (var i = 0; i < mask.Length; i++) if (fine.WaterCollisionMask[i] != 0 || road.Pixels[i] != 0 ||
                (fine.BridgeBodyCollisionMask != null && fine.BridgeBodyCollisionMask[i] != 0)) mask[i] = 255;
            foreach (var r in fine.DeckBounds) Fill(mask, fine, r);
            foreach (var c in assembly.RoadCells.Concat(assembly.SoilCells).Distinct()) Fill(mask, fine, new Rect(c.x, c.y, 1, 1));
            foreach (var route in assembly.MainRoutes) for (var i = 0; i < route.Count; i++)
            {
                var a = route[Math.Max(0, i - 1)] + new Vector2(0, .22f); var b = route[i] + new Vector2(0, .22f);
                Fill(mask, fine, Rect.MinMaxRect(Mathf.Min(a.x, b.x) - .24f, Mathf.Min(a.y, b.y) - .275f, Mathf.Max(a.x, b.x) + .24f, Mathf.Max(a.y, b.y) + .275f));
            }
            foreach (var b in assembly.Blocks) foreach (var port in b.Definition.Ports.Where(p => p.IsWalkable))
                foreach (var c in port.Cells) Fill(mask, fine, new Rect(c.x + b.Origin.x, c.y + b.Origin.y, 1, 1));
            Fill(mask, fine, new Rect(assembly.Spawn.x - .5f, assembly.Spawn.y - .5f, 1, 1));
            foreach (var r in obstacles.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var p = approved.Values.FirstOrDefault(p => p != null && p.Sprite == r.sprite && (p.Metrics.Role == "fountain" || p.Metrics.Role == "shop"));
                if (p != null) Fill(mask, fine, Expand(Translate(p.Metrics.LocalFootprint, r.transform.position), .125f));
            }
            return mask;
        }
        private static void Fill(byte[] pixels, StudyABridgeRiverFinePlan fine, Rect rect)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt((rect.xMin - fine.Origin.x) * PixelsPerUnit));
            var y0 = Mathf.Max(0, Mathf.FloorToInt((rect.yMin - fine.Origin.y) * PixelsPerUnit));
            var x1 = Mathf.Min(fine.Width, Mathf.CeilToInt((rect.xMax - fine.Origin.x) * PixelsPerUnit));
            var y1 = Mathf.Min(fine.Height, Mathf.CeilToInt((rect.yMax - fine.Origin.y) * PixelsPerUnit));
            for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) pixels[y * fine.Width + x] = 255;
        }
        private static void ValidateChangedPixels(Color32[] oldUnder, Color32[] under, Color32[] oldContact, Color32[] contact,
            Color32[] overlay, byte[] forbidden, StudyABridgeRiverFinePlan fine, StudyAGroundIntegrationExperimentReport report)
        {
            for (var y = 0; y < fine.Height; y++) for (var x = 0; x < fine.Width; x++)
            {
                var i = y * fine.Width + x;
                if (under[i].a != oldUnder[i].a || contact[i].a != oldContact[i].a) throw new InvalidDataException("Original alpha byte changed.");
                var changed = !under[i].Equals(oldUnder[i]) || !contact[i].Equals(oldContact[i]);
                if (changed && !Region.Contains((Vector2)fine.Origin + new Vector2((x + .5f) / PixelsPerUnit, (y + .5f) / PixelsPerUnit))) report.ChangesOutsideRegion++;
                if (changed && forbidden[i] != 0) throw new InvalidDataException("Contact RGB changed on protected water/deck/soil/road/port/spawn.");
            }
            for (var y = 0; y < 768; y++) for (var x = 0; x < 768; x++)
            {
                var c = overlay[y * 768 + x]; if (c.a != 0 && c.a != 255) throw new InvalidDataException("Grass overlay alpha must be binary.");
                if (c.a == 0) continue;
                var point = Region.min + new Vector2((x + .5f) / PixelsPerUnit, (y + .5f) / PixelsPerUnit);
                var fx = Mathf.FloorToInt((point.x - fine.Origin.x) * PixelsPerUnit); var fy = Mathf.FloorToInt((point.y - fine.Origin.y) * PixelsPerUnit);
                if (forbidden[fy * fine.Width + fx] != 0) report.ForbiddenOverlayPixels++;
            }
        }
        private static SpriteRenderer RequireGroundRenderer(Transform finish, string name, int order, StudyABridgeRiverFinePlan fine, string expected, string run)
        {
            var t = finish.Find(name); var r = t == null ? null : t.GetComponent<SpriteRenderer>();
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.sprite == null || !r.color.Equals(Color.white) || r.HasPropertyBlock() || r.flipX || r.flipY ||
                r.sortingLayerName != "MapGround" || r.sortingOrder != order || r.transform.position != new Vector3(fine.Origin.x, fine.Origin.y, 0) ||
                r.transform.rotation != Quaternion.identity || r.transform.lossyScale != Vector3.one || r.sprite.pixelsPerUnit != PixelsPerUnit || r.sprite.pivot != Vector2.zero ||
                r.drawMode != SpriteDrawMode.Simple || r.maskInteraction != SpriteMaskInteraction.None || AssetDatabase.GetAssetPath(r.sprite) != expected || !expected.StartsWith(run + "/", StringComparison.Ordinal))
                throw new InvalidDataException("Existing baseline ground-contact layer must be a native run-owned unchanged renderer.");
            return r;
        }
        private static Raster Read(string path, Dictionary<string, Raster> cache, Dictionary<string, string> hashes)
        {
            Raster result; if (cache.TryGetValue(path, out result)) return result;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("\\") ||
                path.Split('/').Any(p => p.Length == 0 || p == "." || p == "..") || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Explicit native PNG asset path required.");
            var bytes = File.ReadAllBytes(ProjectPath(path)); var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!temp.LoadImage(bytes, false)) throw new InvalidDataException("Native PNG could not be decoded.");
                result = new Raster { Width = temp.width, Height = temp.height, Pixels = temp.GetPixels32() };
                cache.Add(path, result); hashes.Add(path, Hash(bytes)); return result;
            }
            finally { Object.DestroyImmediate(temp); }
        }
        private static Sprite WriteSprite(Color32[] pixels, int width, int height, string path)
        {
            if (File.Exists(ProjectPath(path))) throw new IOException("Comparison output exists.");
            var temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try { temp.SetPixels32(pixels); temp.Apply(false, false); File.WriteAllBytes(ProjectPath(path), temp.EncodeToPNG()); }
            finally { Object.DestroyImmediate(temp); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = false; importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048; importer.isReadable = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = Vector2.zero; settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed; platform.maxTextureSize = 2048; importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport(); var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.width != width || sprite.rect.height != height || sprite.pixelsPerUnit != PixelsPerUnit || sprite.pivot != Vector2.zero || sprite.GetPhysicsShapeCount() != 0)
                throw new InvalidDataException("Comparison display PNG native dimensions/pivot/no-physics contract failed.");
            return sprite;
        }
        private static void RequireFreshOutput(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(StudyABlockSceneBuilder.ArtRoot + "/", StringComparison.Ordinal) || path.Contains("\\") ||
                path.Split('/').Any(p => p.Length == 0 || p == "." || p == "..") || Directory.Exists(ProjectPath(path)) || File.Exists(ProjectPath(path)))
                throw new IOException("Fresh dedicated generated comparison directory required.");
            var actual = Path.GetFullPath(ProjectPath(path)); var allowed = Path.GetFullPath(ProjectPath(StudyABlockSceneBuilder.ArtRoot)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!actual.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Comparison output escaped generated art root.");
            for (var p = Directory.GetParent(actual); p != null; p = p.Parent)
                if (p.Exists && (p.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Comparison output traverses a reparse directory.");
        }
        private static string RendererState(GameObject[] roots, params Renderer[] except)
        {
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => !except.Contains(r))
                .OrderBy(r => Hierarchy(r.transform), StringComparer.Ordinal).Select(r => Hierarchy(r.transform) + "|" + EditorJsonUtility.ToJson(r) + "|" + r.transform.localToWorldMatrix.ToString("R")))));
        }
        private static string ContactLayerState(params SpriteRenderer[] renderers)
        {
            // The sprite reference is the sole permitted change on these existing display layers.
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", renderers.Select(r => string.Join("|", new[] {
                Hierarchy(r.transform), r.transform.localToWorldMatrix.ToString("R"), r.enabled.ToString(), r.gameObject.activeSelf.ToString(),
                r.color.ToString("R"), r.flipX.ToString(), r.flipY.ToString(), r.drawMode.ToString(), r.maskInteraction.ToString(),
                r.spriteSortPoint.ToString(), r.sortingLayerName, r.sortingOrder.ToString(), r.size.ToString("R"),
                r.tileMode.ToString(), r.adaptiveModeThreshold.ToString("R", CultureInfo.InvariantCulture), r.HasPropertyBlock().ToString(),
                AssetDatabase.GetAssetPath(r.sharedMaterial), r.sharedMaterial == null ? "null" : EditorJsonUtility.ToJson(r.sharedMaterial) })))));
        }
        private static string ColliderState(GameObject[] roots)
        {
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true))
                .OrderBy(c => Hierarchy(c.transform), StringComparer.Ordinal).Select(c => Hierarchy(c.transform) + "|" + EditorJsonUtility.ToJson(c) + "|" + c.transform.localToWorldMatrix.ToString("R")))));
        }
        private static string MaskState(StudyABridgeRiverFinePlan fine, StudyASoilMask road)
        {
            return Hash(Encoding.UTF8.GetBytes(string.Join("|", new[] { Hash(fine.VisibleWaterMask), Hash(fine.WaterCollisionMask), Hash(fine.FineFreeWalkMask),
                fine.BridgeBodyCollisionMask == null ? "none" : Hash(fine.BridgeBodyCollisionMask), Hash(road.Pixels), fine.Origin.ToString(), fine.Width.ToString(), fine.Height.ToString(),
                string.Join(";", fine.DeckBounds.Select(r => r.ToString("R"))) })));
        }
        private static string AlphaHash(Color32[] pixels) => Hash(pixels.Select(p => p.a).ToArray());
        private static string RgbaHash(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++) { bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a; }
            return Hash(bytes);
        }
        private static Color32 Blend(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)), (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)), (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)), a.a);
        private static Color32 Shade(Color32 a, float scale, byte alpha) => new Color32((byte)Mathf.RoundToInt(a.r * scale), (byte)Mathf.RoundToInt(a.g * scale), (byte)Mathf.RoundToInt(a.b * scale), alpha);
        private static Color32 Multiply(Color32 a, Color c) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(a.r * c.r), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(a.g * c.g), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(a.b * c.b), 0, 255), a.a);
        private static bool EqualRgb(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
        private static Rect Translate(Rect rect, Vector2 p) { rect.position += p; return rect; }
        private static Rect Expand(Rect r, float n) => Rect.MinMaxRect(r.xMin - n, r.yMin - n, r.xMax + n, r.yMax + n);
        private static string Hierarchy(Transform t) { var names = new List<string>(); for (; t != null; t = t.parent) names.Add(t.name); names.Reverse(); return string.Join("/", names); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string FileHash(string path) => Hash(File.ReadAllBytes(path));
        private static string ProjectPath(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar));
    }
}
