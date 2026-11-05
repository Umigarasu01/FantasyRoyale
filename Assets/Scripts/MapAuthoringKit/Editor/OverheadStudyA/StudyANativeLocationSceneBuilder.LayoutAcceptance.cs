using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using FantasyRoyale.Playtest.OverheadStudyA;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    // 原画の実byteを検査し、既存Builderへ地面だけの受入れ結果を渡す。
    public static partial class StudyANativeLocationSceneBuilder
    {
        [Serializable] private sealed class GroundAcceptanceProfile
        {
            public string Schema, Id, ProfileKey;
            public int Version, PPU;
            public GroundInput[] Inputs;
            public GroundMask[] Masks;
            public GroundCandidate[] Candidates;
            public GroundPrefabPin[] PrefabPins;
            public GroundSourceAssetPin[] SourceAssetPins;
            public GroundInput ReferenceProfile;
            public GroundEdgeProfile Primary, LegacyGrass;
        }
        [Serializable] private sealed class GroundInput
        {
            public string Role, RelativePath, AssetPath, SHA256;
            public int Width, Height;
        }
        [Serializable] private sealed class GroundMask
        {
            public string Role, RelativePath, AssetPath, SHA256;
            public int Width, Height;
            public RectInt[] EditableRects, ProtectedRects;
        }
        [Serializable] private sealed class GroundCandidate
        {
            public string Role, ExpectedFileName, ExpectedAssetPath, SourceRole, MaskRole;
            public int Width, Height;
        }
        [Serializable] private sealed class GroundPrefabPin { public string Role, AssetPath, SHA256, MetaSHA256; }
        [Serializable] private sealed class GroundSourceAssetPin { public string AssetPath, SHA256, MetaSHA256; }
        [Serializable] private sealed class GroundRGBA { public byte r, g, b, a; }
        [Serializable] private sealed class GroundEdgeProfile
        {
            public int BColumn, DColumn, FirstRow, BFirstRow, CColumn, CFirstRow, SampleCount;
            public GroundRGBA[] RGBA;
        }
        [Serializable] private sealed class GroundPixelReceipt
        {
            public string ProfilePath, ProfileSHA256;
            public string[] OriginalPrefabSHA256, OriginalGroundSHA256, MaskSHA256;
            public string BGroundPath, DGroundPath, BGroundSHA256, DGroundSHA256;
            public string ArtAcceptanceReceiptPath, ArtAcceptanceReceiptSHA256;
            public bool PixelContractPassed;
            public bool UnityImportOrRuntimeVerified;
            public string Qualification = "Current raw PNG/source-bound pixel contract only; no Unity import, runtime movement, depth or artistic quality certification.";
        }
        [Serializable] private sealed class NativeLayoutPreflightDocument
        {
            public string LayoutSetPath, LayoutSetSHA256, ProfilePath, ProfileSHA256;
            public StudyANativeLayoutAcceptedCatalog AcceptedCatalog;
            public GroundPixelReceipt CurrentE2Pixels;
            public string EUnavailableReason;
        }
        // 公式受領と原画・native接続画像の目視判定を、同一sourceのhashで固定する。
        [Serializable] private sealed class NativeGroundArtAcceptanceReceipt
        {
            public string Schema;
            public bool OfficialReceptionVerified, VisualAccepted;
            public string IntakeSpecSHA256, PrimaryProfileSHA256;
            public GroundSourceAssetPin[] SourcePins;
            public GroundAcceptanceBinding[] Masks, Candidates;
            public GroundReviewedArtifact[] ReviewedArtifacts;
            public GroundLibraryInput[] LibraryInputs;
        }
        [Serializable] private sealed class GroundAcceptanceBinding { public string Role, AssetPath, SHA256; }
        [Serializable] private sealed class GroundReviewedArtifact { public string Role, AssetPath, SHA256; }
        [Serializable] private sealed class GroundLibraryInput
        {
            public string Role, LibraryFileId, ReceptionReceiptPath, ReceptionReceiptSHA256, GroundSHA256;
        }

        private const string LayoutAssetRoot = "Assets/Art/Map/OverheadStudyA/IndependentPrefabStudies/NativeLocationLayouts-v2-20261006";
        private const string PrimaryProfileSHA256 = "36b26d7d46ff198bec0f2e2ce3be58b99e36549edcfd9c6a20a1ba40278905d7";
        private static readonly string[] OriginalIds = { "A", "B", "C", "D" };
        private static readonly string[] OriginalGroundHashes = {
            "8fdd5d5c217d7adc1d4f696ad3d35901887d92516b65447c373fd60828e8dfaf",
            "b9f92007e6eb724a631ae1b5b1d5f4c1422c4d36eac89bc234efbd916dc40c98",
            "95cde8c91ba7241a61476eb66d8c052e2a52113da2b8cc023047ebcf1d590f88",
            "e4663662521b559cba8295b97f3fc7c9ad19433529f7271aa81116a244b43ebc" };
        private static readonly string[] OriginalPrefabHashes = {
            "17ed05033a213f2ff7521ec6fc5b8e5bd32b9d6c820335027d1a0a30596a7adb",
            "e9d0ed8575216724028536756e8b9d264c546f7cd1bee8b0bfe63abe8a808e5d",
            "e36286640f0228880bacdbb5ea274e9582fc6ba52a92b5e07f608b19e2050b45",
            "1f9d850f64a965d646450231356884de0504fa1a0f7daf63177ad1464e96a314" };
        private static readonly string[] AcceptanceMaskHashes = {
            "5cea802bc711ef8c62fe7900bf0c964ffb70f123293f0a98e128be7c9688c468",
            "b1a768011137b7792cc7d38401d5ec5205941cd0daf5a8052b50ba9c8f85a7b6" };

        // v1入口を保ったまま、原画の再検証receiptだけをpure recipe resolverへ渡す。
        private static StudyANativeLocationAssembly ResolveConfiguredNativeLayout(StudyANativeLocationSceneConfig config,
            StudyANativeLocationCatalog original, StudyANativeLocationSceneReport report,
            out StudyANativeLocationCatalog effective, out Dictionary<string, Sprite> groundOverrides)
        {
            Require(config.MinBlocks == 4 && config.MaxBlocks == 4 && config.RequestedBlockCount == 4,
                "Native layout recipes require exactly four logical locations; use the original v1 config for subsets.");
            var setAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath(config.LayoutSetPath));
            Require(setAsset != null && !EditorUtility.IsDirty(setAsset), "Native layout set is missing or dirty.");
            var set = JsonUtility.FromJson<StudyANativeLocationLayoutSet>(setAsset.text);
            Require(set != null && set.BaseCatalog != null && original != null &&
                JsonUtility.ToJson(set.BaseCatalog) == JsonUtility.ToJson(original), "Layout BaseCatalog must exactly preserve the existing v1 catalog.");
            Require(original.Locations.Length == 4 && original.Locations.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(OriginalIds),
                "Native layout recipes require one original A/B/C/D each.");
            foreach (var definition in original.Locations)
            {
                var i = Array.IndexOf(OriginalIds, definition.Id);
                Require(definition.PrefabSHA256 == OriginalPrefabHashes[i] && definition.GroundSHA256 == OriginalGroundHashes[i], "Original source pins changed.");
                GuardNativePrefab(definition);
            }
            // The profile is a fixed, hashed policy, never an Accepted bool or mutable manifest assertion.
            ReadAndGuardNativeGroundProfile(config, original);
            var receipts = new List<StudyANativeVariantPreflightReceipt>();
            Require(set.Variants != null, "Native layout variants are missing.");
            foreach (var variant in set.Variants)
            {
                Require(variant != null && variant.Definition != null, "Native layout variant is missing.");
                var source = original.Locations.Single(d => d.Id == variant.LogicalLocationId);
                var definition = variant.Definition;
                Require(definition.PrefabPath == source.PrefabPath && definition.PrefabSHA256 == source.PrefabSHA256 &&
                    definition.ExtentPx == source.ExtentPx && definition.SpawnPx == source.SpawnPx &&
                    definition.SourceOwnerIds.SequenceEqual(source.SourceOwnerIds) &&
                    JsonUtility.ToJson(new FootUnitEnvelope { Units = definition.FootUnits }) == JsonUtility.ToJson(new FootUnitEnvelope { Units = source.FootUnits }) &&
                    variant.SourceGroundSHA256 == source.GroundSHA256, "Ground variants cannot change original prefab, owner IDs, source foot units or spawn.");
                if (definition.GroundPath != source.GroundPath) continue;
                Require(definition.GroundSHA256 == source.GroundSHA256 &&
                    variant.MaskSHA256 == StudyABlockGenerator.NativeNoRepaintGroundMaskSHA256,
                    "An unchanged ground requires the fixed no-repaint policy token and the identical original path/SHA.");
                GuardFile(definition.PrefabPath, definition.PrefabSHA256); GuardFile(definition.GroundPath, definition.GroundSHA256);
                var pinReceiptHash = TextHash("FantasyRoyale.NativeOriginalGroundPreflight.v1\n" + variant.Id + "\n" +
                    definition.PrefabPath + "\n" + FileHash(definition.PrefabPath) + "\n" + definition.GroundPath + "\n" +
                    FileHash(definition.GroundPath) + "\n" + StudyABlockGenerator.NativeNoRepaintGroundMaskSHA256 + "\n");
                receipts.Add(StudyABlockGenerator.CreateNativeVariantPreflightReceipt(variant, FileHash(definition.PrefabPath),
                    FileHash(definition.GroundPath), variant.PortBandVersion, StudyABlockGenerator.NativeNoRepaintGroundMaskSHA256, pinReceiptHash));
            }
            var changed = set.Variants.Where(v => v.Definition.GroundPath != original.Locations.Single(d => d.Id == v.LogicalLocationId).GroundPath).ToArray();
            Require(changed.Length == 2 && changed.Select(v => v.LogicalLocationId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "B", "D" }),
                "E accepts exactly two new ground variants, with one B and one D.");
            var b = changed.Single(v => v.LogicalLocationId == "B"); var d = changed.Single(v => v.LogicalLocationId == "D");
            Require(b.PortBandVersion == 2 && d.PortBandVersion == 2 && b.MaskSHA256 == AcceptanceMaskHashes[0] && d.MaskSHA256 == AcceptanceMaskHashes[1],
                "E ground variants require the exact E2 band version and real mask PNG hashes.");
            GroundPixelReceipt currentPixels = null;
            var unavailable = "E is pending: both officially accepted new ground file SHA256 values are required.";
            if (ValidNativeGroundSHA(b.Definition.GroundSHA256) && ValidNativeGroundSHA(d.Definition.GroundSHA256))
            {
                try
                {
                    currentPixels = VerifyAcceptedE2Grounds(config, original, b.Definition.GroundPath, b.Definition.GroundSHA256,
                        d.Definition.GroundPath, d.Definition.GroundSHA256);
                    VerifyNativeGroundArtAcceptance(config, ReadAndGuardNativeGroundProfile(config, original), currentPixels);
                    currentPixels.ArtAcceptanceReceiptPath = config.ArtAcceptanceReceiptPath;
                    currentPixels.ArtAcceptanceReceiptSHA256 = FileHash(config.ArtAcceptanceReceiptPath);
                    var receiptHash = TextHash(JsonUtility.ToJson(currentPixels, true) + "\n");
                    foreach (var variant in changed)
                        receipts.Add(StudyABlockGenerator.CreateNativeVariantPreflightReceipt(variant, FileHash(variant.Definition.PrefabPath),
                            FileHash(variant.Definition.GroundPath), 2, variant.MaskSHA256, receiptHash));
                    unavailable = string.Empty;
                }
                catch (Exception exception)
                {
                    // Invalid current pixels cannot become a seed candidate. An explicit E request is rejected by the resolver.
                    currentPixels = null; unavailable = "E current raw ground preflight rejected: " + exception.Message;
                }
            }
            var accepted = new StudyANativeLayoutAcceptedCatalog { LayoutSetId = set.Id, LayoutSetVersion = set.Version, VariantReceipts = receipts.ToArray() };
            report.LayoutSetPath = config.LayoutSetPath; report.LayoutSetSHA256 = FileHash(config.LayoutSetPath);
            report.GroundAcceptanceQualification = unavailable.Length == 0 ? currentPixels.Qualification : unavailable;
            var result = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, config.Seed, config.RecipeId ?? string.Empty);
            effective = result.EffectiveCatalog; groundOverrides = null;
            if (result.RecipeId == "E")
            {
                Require(currentPixels != null && currentPixels.PixelContractPassed, "E requires this invocation's real current raw PNG proof.");
                groundOverrides = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var variant in changed)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(variant.Definition.GroundPath);
                    Require(sprite != null, "Accepted native E ground must be imported before generating a copied output.");
                    groundOverrides.Add(variant.LogicalLocationId, sprite);
                }
            }
            report.RecipeId = result.RecipeId; report.LayoutContentHash = result.ContentHash;
            var evidence = new NativeLayoutPreflightDocument { LayoutSetPath = report.LayoutSetPath, LayoutSetSHA256 = report.LayoutSetSHA256,
                ProfilePath = config.GroundAcceptanceProfilePath, ProfileSHA256 = FileHash(config.GroundAcceptanceProfilePath),
                AcceptedCatalog = accepted, CurrentE2Pixels = currentPixels, EUnavailableReason = unavailable };
            report.GroundPreflightJSON = JsonUtility.ToJson(evidence, true) + "\n";
            report.GroundPreflightSHA256 = TextHash(report.GroundPreflightJSON);
            report.LayoutResolutionJSON = JsonUtility.ToJson(result, true) + "\n";
            return result.Assembly;
        }
        [Serializable] private sealed class FootUnitEnvelope { public StudyANativeFootUnit[] Units; }
        private static bool ValidNativeGroundSHA(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        private static void WriteConfiguredNativeLayoutEvidence(StudyANativeLocationSceneConfig config, string ownedOutput,
            StudyANativeLocationSceneReport report)
        {
            if (string.IsNullOrEmpty(config.LayoutSetPath)) return;
            GuardFile(config.LayoutSetPath, report.LayoutSetSHA256);
            GuardFile(config.GroundAcceptanceProfilePath, config.GroundAcceptanceProfileSHA256);
            var setAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.LayoutSetPath);
            var profileAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.GroundAcceptanceProfilePath);
            Require(setAsset != null && profileAsset != null && TextHash(report.GroundPreflightJSON) == report.GroundPreflightSHA256,
                "Native layout preflight evidence changed.");
            WriteNew(ownedOutput + "/source-layout-set.json", setAsset.text);
            WriteNew(ownedOutput + "/source-ground-acceptance-profile.json", profileAsset.text);
            WriteNew(ownedOutput + "/ground-preflight.json", report.GroundPreflightJSON);
            WriteNew(ownedOutput + "/layout-resolution.json", report.LayoutResolutionJSON);
            foreach (var name in new[] { "source-layout-set.json", "source-ground-acceptance-profile.json", "ground-preflight.json", "layout-resolution.json" })
                AssetDatabase.ImportAsset(ownedOutput + "/" + name);
            if (report.RecipeId == "E")
            {
                GuardFile(config.ArtAcceptanceReceiptPath, config.ArtAcceptanceReceiptSHA256);
                var artReceipt = AssetDatabase.LoadAssetAtPath<TextAsset>(config.ArtAcceptanceReceiptPath);
                Require(artReceipt != null && !EditorUtility.IsDirty(artReceipt), "Accepted source art receipt changed.");
                WriteNew(ownedOutput + "/source-art-acceptance.json", artReceipt.text);
                AssetDatabase.ImportAsset(ownedOutput + "/source-art-acceptance.json");
            }
        }

        // 同じ原画・検査仕様・元source・実review画像・公式ローカル受領証跡をhashで照合する。
        private static void VerifyNativeGroundArtAcceptance(StudyANativeLocationSceneConfig config, GroundAcceptanceProfile profile, GroundPixelReceipt pixels)
        {
            GuardFile(config.ArtAcceptanceReceiptPath, config.ArtAcceptanceReceiptSHA256);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.ArtAcceptanceReceiptPath);
            Require(asset != null && !EditorUtility.IsDirty(asset), "Official and visual art receipt is missing or dirty.");
            var receipt = JsonUtility.FromJson<NativeGroundArtAcceptanceReceipt>(asset.text);
            Require(receipt != null && receipt.Schema == "FantasyRoyale.NativeGroundArtAcceptanceReceipt.v1" &&
                receipt.OfficialReceptionVerified && receipt.VisualAccepted && receipt.IntakeSpecSHA256 == pixels.ProfileSHA256 &&
                receipt.PrimaryProfileSHA256 == PrimaryProfileSHA256, "Source-bound official reception and native art visual acceptance are required.");
            var expectedSource = profile.SourceAssetPins.Concat(profile.PrefabPins.Select(p => new GroundSourceAssetPin {
                AssetPath = p.AssetPath, SHA256 = p.SHA256, MetaSHA256 = p.MetaSHA256 })).ToDictionary(p => p.AssetPath, StringComparer.Ordinal);
            Require(receipt.SourcePins != null && receipt.SourcePins.Length == expectedSource.Count, "Art receipt must bind every original PNG/meta and prefab/meta source.");
            var boundSources = receipt.SourcePins.ToDictionary(p => p.AssetPath, StringComparer.Ordinal);
            foreach (var pin in expectedSource.Values)
            {
                Require(boundSources.ContainsKey(pin.AssetPath) && boundSources[pin.AssetPath].SHA256 == pin.SHA256 &&
                    boundSources[pin.AssetPath].MetaSHA256 == pin.MetaSHA256, "Art receipt original source binding differs.");
                GuardFile(pin.AssetPath, pin.SHA256); GuardFile(pin.AssetPath + ".meta", pin.MetaSHA256);
            }
            Require(receipt.Masks != null && receipt.Masks.Length == 2 && receipt.Candidates != null && receipt.Candidates.Length == 2,
                "Art receipt must bind the two exact E ground candidates and their masks.");
            foreach (var role in new[] { "B_E", "D_W" })
            {
                var mask = receipt.Masks.Single(p => p.Role == role); var expected = profile.Masks.Single(p => p.Role == role);
                Require(mask.AssetPath == expected.AssetPath && mask.SHA256 == expected.SHA256, "Art receipt E2 mask binding differs.");
                GuardFile(mask.AssetPath, mask.SHA256);
                var ground = receipt.Candidates.Single(p => p.Role == role);
                var path = role == "B_E" ? pixels.BGroundPath : pixels.DGroundPath;
                var hash = role == "B_E" ? pixels.BGroundSHA256 : pixels.DGroundSHA256;
                Require(ground.AssetPath == path && ground.SHA256 == hash, "Art receipt actual received ground binding differs.");
                GuardFile(path, hash);
            }
            Require(receipt.ReviewedArtifacts != null && receipt.ReviewedArtifacts.Length > 0 &&
                receipt.ReviewedArtifacts.Any(p => p.Role == "NativeCollar"), "Source-bound reviewed native collar pixels are required.");
            foreach (var artifact in receipt.ReviewedArtifacts)
            {
                GuardFile(artifact.AssetPath, artifact.SHA256);
                Require(artifact.AssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase), "Reviewed native artifact must be a real PNG.");
                var bytes = System.IO.File.ReadAllBytes(ProjectPath(artifact.AssetPath));
                Require(bytes.Length > 33 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71, "Reviewed artifact PNG is missing.");
                var width = PNGBigEndian(bytes, 16); var height = PNGBigEndian(bytes, 20);
                Require(width > 0 && height > 0 && width <= int.MaxValue && height <= int.MaxValue, "Reviewed native artifact dimensions are invalid.");
                VerifyRawNativePNG(bytes, (int)width, (int)height, 6);
            }
            Require(receipt.LibraryInputs != null && receipt.LibraryInputs.Length == 2 &&
                receipt.LibraryInputs.Select(p => p.Role).OrderBy(role => role, StringComparer.Ordinal).SequenceEqual(new[] { "B_E", "D_W" }),
                "Official receipt requires the two exact Library ground roles; one authorized ZIP may carry both PNGs.");
            foreach (var role in new[] { "B_E", "D_W" })
            {
                var input = receipt.LibraryInputs.Single(p => p.Role == role);
                var hash = role == "B_E" ? pixels.BGroundSHA256 : pixels.DGroundSHA256;
                Require(input.LibraryFileId != null && input.LibraryFileId.StartsWith("libfile_", StringComparison.Ordinal) &&
                    input.LibraryFileId.Length == 40 && input.LibraryFileId.Substring(8).All(Uri.IsHexDigit) && input.GroundSHA256 == hash,
                    "Official Library identity must bind the actual accepted raw ground bytes.");
                GuardFile(input.ReceptionReceiptPath, input.ReceptionReceiptSHA256);
                var proof = AssetDatabase.LoadAssetAtPath<TextAsset>(input.ReceptionReceiptPath);
                Require(proof != null && !EditorUtility.IsDirty(proof) && proof.text.Contains(input.LibraryFileId) &&
                    proof.text.IndexOf(hash, StringComparison.OrdinalIgnoreCase) >= 0, "Official local reception proof does not bind this Library ID and raw PNG hash.");
            }
        }

        // Unity imported textureの設定に依存せず、CPUでPNGのnative RGBAを読む。
        private sealed class GroundPixels : IDisposable
        {
            private readonly Texture2D texture;
            private readonly Color32[] pixels;
            public readonly int Width, Height;
            public GroundPixels(string assetPath, string hash, int width, int height, bool requireRGBA8 = false, bool requireGray8Mask = false)
            {
                GuardFile(assetPath, hash);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(ProjectPath(assetPath));
                    Require(bytes.Length > 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                        bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10, "Expected a raw PNG: " + assetPath);
                    VerifyRawNativePNG(bytes, width, height, requireRGBA8 ? 6 : requireGray8Mask ? 0 : -1);
                    Require(ImageConversion.LoadImage(texture, bytes, false), "CPU PNG decoding failed: " + assetPath);
                    Width = texture.width; Height = texture.height;
                    Require(Width == width && Height == height, "PNG dimensions are not native: " + assetPath);
                    pixels = texture.GetPixels32();
                    Require(pixels.Length == width * height && pixels.All(p => p.a == 255), "Every ground/mask texel must be opaque: " + assetPath);
                }
                catch { Object.DestroyImmediate(texture); throw; }
            }
            public Color32 TL(int x, int y) => pixels[(Height - 1 - y) * Width + x];
            public void Dispose() { Object.DestroyImmediate(texture); }
        }

        private static bool SameRGBA(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static uint PNGBigEndian(byte[] bytes, int offset) => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        private static void VerifyRawNativePNG(byte[] bytes, int width, int height, int colorType)
        {
            Require(bytes.Length > 33 && PNGBigEndian(bytes, 8) == 13 && System.Text.Encoding.ASCII.GetString(bytes, 12, 4) == "IHDR" &&
                PNGBigEndian(bytes, 16) == width && PNGBigEndian(bytes, 20) == height && bytes[24] == 8 &&
                (colorType < 0 || bytes[25] == colorType) && bytes[26] == 0 && bytes[27] == 0 && bytes[28] <= 1, "PNG IHDR is not the exact supported native format.");
            var ended = false; var dataSeen = false;
            for (var offset = 8; offset < bytes.Length;)
            {
                Require(!ended && offset <= bytes.Length - 12, "PNG chunk header or trailing data is invalid.");
                var length = PNGBigEndian(bytes, offset);
                Require(length <= int.MaxValue && (long)offset + length + 12 <= bytes.Length, "PNG chunk length is invalid.");
                var chunk = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                Require(chunk != "acTL" && chunk != "fcTL" && chunk != "fdAT", "Animated PNG is not accepted.");
                uint crc = 0xffffffff;
                for (var index = offset + 4; index < offset + 8 + (int)length; index++)
                {
                    crc ^= bytes[index];
                    for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1;
                }
                Require((crc ^ 0xffffffff) == PNGBigEndian(bytes, offset + 8 + (int)length), "PNG chunk CRC differs.");
                if (chunk == "IDAT") dataSeen = true;
                if (chunk == "IEND") { Require(length == 0, "PNG IEND length is invalid."); ended = true; }
                offset = checked(offset + (int)length + 12);
            }
            Require(ended && dataSeen, "Complete PNG IDAT and IEND are required.");
        }

        private static GroundAcceptanceProfile ReadAndGuardNativeGroundProfile(StudyANativeLocationSceneConfig config, StudyANativeLocationCatalog original)
        {
            GuardFile(config.GroundAcceptanceProfilePath, config.GroundAcceptanceProfileSHA256);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(config.GroundAcceptanceProfilePath);
            Require(asset != null && !EditorUtility.IsDirty(asset), "Fixed ground acceptance profile is missing or dirty.");
            var profile = JsonUtility.FromJson<GroundAcceptanceProfile>(asset.text);
            Require(profile != null && profile.Schema == "FantasyRoyale.NativeGroundAcceptanceProfile.v2" &&
                profile.Id == "E-ground-reception-v2-20261006" && profile.PPU == PPU && profile.Version == 2 &&
                profile.ProfileKey == "native-band-v2/grass-family-1/soil64/height0/phase-port-centred" &&
                profile.Inputs != null && profile.Inputs.Length == 4 && profile.PrefabPins != null && profile.PrefabPins.Length == 4 &&
                profile.Masks != null && profile.Masks.Length == 2 && profile.Candidates != null && profile.Candidates.Length == 2 &&
                profile.SourceAssetPins != null && profile.SourceAssetPins.Length == 33 && profile.ReferenceProfile != null &&
                profile.ReferenceProfile.SHA256 == PrimaryProfileSHA256, "Fixed E2 source-bound profile is invalid.");
            GuardFile(profile.ReferenceProfile.AssetPath, PrimaryProfileSHA256);
            var pins = profile.SourceAssetPins.ToDictionary(p => AssetPath(p.AssetPath), StringComparer.Ordinal);
            foreach (var pin in pins.Values)
            {
                Require(pin.AssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase), "Source render pin must identify a real original PNG.");
                GuardFile(pin.AssetPath, pin.SHA256); GuardFile(pin.AssetPath + ".meta", pin.MetaSHA256);
            }
            foreach (var definition in original.Locations)
            {
                var i = Array.IndexOf(OriginalIds, definition.Id);
                Require(i >= 0 && definition.PrefabSHA256 == OriginalPrefabHashes[i] && definition.GroundSHA256 == OriginalGroundHashes[i], "Original source pins changed.");
                var pin = profile.PrefabPins.Single(p => p.Role == definition.Id);
                var input = profile.Inputs.Single(p => p.Role == definition.Id);
                Require(pin.AssetPath == definition.PrefabPath && pin.SHA256 == definition.PrefabSHA256 &&
                    input.AssetPath == definition.GroundPath && input.SHA256 == definition.GroundSHA256 &&
                    input.Width == definition.ExtentPx.x && input.Height == definition.ExtentPx.y,
                    "Fixed profile source pins differ from the unchanged v1 catalog.");
                GuardFile(pin.AssetPath, pin.SHA256); GuardFile(pin.AssetPath + ".meta", pin.MetaSHA256);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
                Require(prefab != null, "Original source prefab is missing.");
                foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null || !renderer.enabled || !NativePrefabPartActive(renderer.transform)) continue;
                    var sourcePath = AssetDatabase.GetAssetPath(renderer.sprite);
                    Require(pins.ContainsKey(sourcePath), "Original prefab sprite is absent from the source-bound PNG/meta pins: " + sourcePath);
                }
            }
            return profile;
        }
        private static bool NativePrefabPartActive(Transform part)
        {
            for (var current = part; current != null; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
        private static bool InsideE2Mask(string id, int x, int y)
        {
            if (id == "B") return x >= 352 && x < 384 && y >= 0 && y < 640;
            return y != 511 && ((x >= 0 && x < 32 && y >= 0 && y < 511) ||
                (x >= 0 && x < 288 && y >= 279 && y < 407));
        }
        private static void VerifyMaskAndRetainedPixels(string id, GroundPixels original, GroundPixels candidate, GroundPixels mask)
        {
            var editableCount = 0;
            for (var y = 0; y < original.Height; y++)
            for (var x = 0; x < original.Width; x++)
            {
                var editable = InsideE2Mask(id, x, y); var expected = editable ? (byte)255 : (byte)0;
                var m = mask.TL(x, y);
                Require(m.r == expected && m.g == expected && m.b == expected && m.a == 255,
                    "Mask does not exactly match the fixed E2 native region: " + id);
                if (editable) editableCount++;
                else Require(SameRGBA(original.TL(x, y), candidate.TL(x, y)), "Pixels changed outside the E2 mask: " + id + " " + x + "," + y);
            }
            Require(editableCount == (id == "B" ? 20480 : 49120), "Editable native mask area changed.");
        }
        private static void VerifyEdgeProfiles(GroundAcceptanceProfile profile, GroundPixels originalB, GroundPixels originalC)
        {
            var p = profile.Primary; var l = profile.LegacyGrass;
            Require(p != null && p.BColumn == 383 && p.DColumn == 0 && p.FirstRow == 0 && p.SampleCount == 512 && p.RGBA != null && p.RGBA.Length == 512,
                "Primary512 source profile fields changed.");
            Require(l != null && l.BColumn == 383 && l.BFirstRow == 512 && l.CColumn == 0 && l.CFirstRow == 0 && l.SampleCount == 128 && l.RGBA != null && l.RGBA.Length == 128,
                "Legacy128 source profile fields changed.");
            for (var y = 0; y < 512; y++)
            {
                var expected = y == 511 ? originalC.TL(0, 0) : originalB.TL(383, y);
                Require(SameRGBA(expected, new Color32(p.RGBA[y].r, p.RGBA[y].g, p.RGBA[y].b, p.RGBA[y].a)), "Primary profile is not source-bound.");
            }
            for (var y = 0; y < 128; y++)
                Require(SameRGBA(originalC.TL(0, y), new Color32(l.RGBA[y].r, l.RGBA[y].g, l.RGBA[y].b, l.RGBA[y].a)), "Legacy profile is not source-bound.");
        }
        private static void VerifyActualEdges(GroundPixels a, GroundPixels oldB, GroundPixels c, GroundPixels oldD, GroundPixels b, GroundPixels d)
        {
            for (var y = 0; y < 512; y++)
            {
                var expected = y == 511 ? c.TL(0, 0) : oldB.TL(383, y);
                Require(SameRGBA(b.TL(383, y), expected) && SameRGBA(d.TL(0, y), expected), "Actual primary512 native RGBA differs from the accepted source-bound profile.");
            }
            for (var y = 0; y < 128; y++)
                Require(SameRGBA(b.TL(383, y + 512), c.TL(0, y)), "Actual incidental B/C legacy128 native RGBA differs.");
            for (var y = 0; y < 640; y++)
                Require(SameRGBA(a.TL(767, y), oldB.TL(0, y)) && SameRGBA(b.TL(0, y), oldB.TL(0, y)), "Existing A/B full640 edge changed.");
            for (var x = 0; x < 640; x++)
                Require(SameRGBA(d.TL(x, 511), oldD.TL(x, 511)), "D full bottom640 source RGBA changed.");
            for (var x = 0; x < 512; x++)
                Require(SameRGBA(oldD.TL(x, 511), c.TL(x, 0)) && SameRGBA(d.TL(x, 511), c.TL(x, 0)), "Existing C/D full512 edge changed.");
            var corner = new Color32(88, 162, 35, 255);
            Require(SameRGBA(c.TL(0, 0), corner) && SameRGBA(d.TL(0, 511), corner) &&
                SameRGBA(b.TL(383, 511), corner) && SameRGBA(b.TL(383, 512), corner), "Actual T-corner RGBA differs.");
        }

        // Accepted宣言を根拠にせず、元source pinsと現在PNGを再読取してpixel契約を判定する。
        private static GroundPixelReceipt VerifyAcceptedE2Grounds(StudyANativeLocationSceneConfig config,
            StudyANativeLocationCatalog original, string bPath, string bHash, string dPath, string dHash)
        {
            var profile = ReadAndGuardNativeGroundProfile(config, original);
            var inputs = profile.Inputs.ToDictionary(p => p.Role, StringComparer.Ordinal);
            var pins = profile.PrefabPins.ToDictionary(p => p.Role, StringComparer.Ordinal);
            Require(bPath == LayoutAssetRoot + "/Sources/B_E_ground.png" && dPath == LayoutAssetRoot + "/Sources/D_W_ground.png", "Unexpected native E ground asset destination.");
            foreach (var definition in original.Locations)
            {
                var i = Array.IndexOf(OriginalIds, definition.Id);
                Require(i >= 0 && definition.GroundSHA256 == OriginalGroundHashes[i] && definition.PrefabSHA256 == OriginalPrefabHashes[i], "Original source pins changed.");
                Require(inputs.ContainsKey(definition.Id) && pins.ContainsKey(definition.Id), "Fixed profile omits an original source pin.");
                var input = inputs[definition.Id]; var pin = pins[definition.Id];
                Require(input.AssetPath == definition.GroundPath && input.SHA256 == OriginalGroundHashes[i] &&
                    input.Width == definition.ExtentPx.x && input.Height == definition.ExtentPx.y &&
                    pin.AssetPath == definition.PrefabPath && pin.SHA256 == OriginalPrefabHashes[i], "Profile source pin differs from the current original catalog.");
                GuardFile(pin.AssetPath, pin.SHA256);
            }
            var bMask = profile.Masks.Single(p => p.Width == 384 && p.Height == 640);
            var dMask = profile.Masks.Single(p => p.Width == 640 && p.Height == 512);
            Require(bMask.SHA256 == AcceptanceMaskHashes[0] && dMask.SHA256 == AcceptanceMaskHashes[1], "Fixed E2 mask source SHA changed.");
            Require(profile.Candidates.Any(p => p.ExpectedAssetPath == bPath && p.Width == 384 && p.Height == 640) &&
                profile.Candidates.Any(p => p.ExpectedAssetPath == dPath && p.Width == 640 && p.Height == 512), "Fixed profile candidate destinations changed.");
            using (var a = new GroundPixels(inputs["A"].AssetPath, inputs["A"].SHA256, 768, 640))
            using (var oldB = new GroundPixels(inputs["B"].AssetPath, inputs["B"].SHA256, 384, 640))
            using (var c = new GroundPixels(inputs["C"].AssetPath, inputs["C"].SHA256, 512, 384))
            using (var oldD = new GroundPixels(inputs["D"].AssetPath, inputs["D"].SHA256, 640, 512))
            using (var b = new GroundPixels(bPath, bHash, 384, 640, true))
            using (var d = new GroundPixels(dPath, dHash, 640, 512, true))
            using (var bm = new GroundPixels(bMask.AssetPath, bMask.SHA256, 384, 640, false, true))
            using (var dm = new GroundPixels(dMask.AssetPath, dMask.SHA256, 640, 512, false, true))
            {
                VerifyEdgeProfiles(profile, oldB, c);
                VerifyMaskAndRetainedPixels("B", oldB, b, bm);
                VerifyMaskAndRetainedPixels("D", oldD, d, dm);
                VerifyActualEdges(a, oldB, c, oldD, b, d);
            }
            return new GroundPixelReceipt { ProfilePath = config.GroundAcceptanceProfilePath, ProfileSHA256 = FileHash(config.GroundAcceptanceProfilePath),
                OriginalPrefabSHA256 = OriginalPrefabHashes, OriginalGroundSHA256 = OriginalGroundHashes, MaskSHA256 = AcceptanceMaskHashes,
                BGroundPath = bPath, DGroundPath = dPath, BGroundSHA256 = FileHash(bPath), DGroundSHA256 = FileHash(dPath), PixelContractPassed = true };
        }

        // コピーしたinstanceのowner外の地面1rendererだけを差替える。
        private static void ApplyAcceptedNativeGround(GameObject instance, string logicalId, Sprite acceptedGround)
        {
            Require(logicalId == "B" || logicalId == "D", "Only the two accepted E ground variants may override original ground.");
            var originalGroundHash = OriginalGroundHashes[Array.IndexOf(OriginalIds, logicalId)];
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sprite != null &&
                r.GetComponentInParent<StudyAIndependentObjectState>() == null &&
                FileHash(AssetDatabase.GetAssetPath(r.sprite)) == originalGroundHash).ToArray();
            Require(renderers.Length == 1 && acceptedGround != null, "Expected exactly one immutable-source ground renderer outside all source owners.");
            var renderer = renderers[0];
            renderer.sprite = acceptedGround;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
            var sourceRenderer = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
            var modifications = PrefabUtility.GetPropertyModifications(instance);
            Require(renderer.sprite == acceptedGround && sourceRenderer != null && modifications != null &&
                modifications.Any(m => m.target == sourceRenderer && m.propertyPath == "m_Sprite" && m.objectReference == acceptedGround),
                "The copied native ground m_Sprite override was not recorded for scene serialization.");
        }
    }
}
