using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.OverheadStudyA
{
    /// <summary>元catalogを保持し、認定済み素材の組合せだけを全4ロケーションのrecipeとして選ぶ。</summary>
    [Serializable]
    public sealed class StudyANativeLocationLayoutSet
    {
        public string Id = "native-location-layouts-v2";
        public int Version = 1;
        public int PixelsPerUnit = 64;
        public StudyANativeLocationCatalog BaseCatalog;
        public StudyANativeLocationVariant[] Variants = Array.Empty<StudyANativeLocationVariant>();
        public StudyANativeLocationLayoutRecipe[] Recipes = Array.Empty<StudyANativeLocationLayoutRecipe>();
    }

    /// <summary>所有者と元prefabを共有する地面・接続口の差分。未納品GroundSHAは空のまま保持する。</summary>
    [Serializable]
    public sealed class StudyANativeLocationVariant
    {
        public string Id = string.Empty;
        public string LogicalLocationId = string.Empty;
        public StudyANativeLocationDefinition Definition;
        public string SourceGroundSHA256 = string.Empty;
        public int PortBandVersion = 1;
        public string MaskSHA256 = string.Empty;
    }

    [Serializable]
    public sealed class StudyANativeLocationLayoutRecipe
    {
        public string Id = string.Empty;
        public int Version = 1;
        public bool UseBaseCatalog;
        public string CatalogId = string.Empty;
        public int CatalogVersion = 1;
        public StudyANativeLocationVariantBinding[] Bindings = Array.Empty<StudyANativeLocationVariantBinding>();
        public StudyANativeLocationRecipeConnection[] Connections = Array.Empty<StudyANativeLocationRecipeConnection>();
        public StudyANativeSharedEdgeMatch[] SharedEdgeMatches = Array.Empty<StudyANativeSharedEdgeMatch>();
    }

    [Serializable]
    public sealed class StudyANativeLocationVariantBinding
    {
        public string LocationId = string.Empty;
        public string VariantId = string.Empty;
        public Vector2Int OriginPx;
    }

    [Serializable]
    public sealed class StudyANativeLocationRecipeConnection
    {
        public string FromLocationId = string.Empty;
        public string FromPortId = string.Empty;
        public string ToLocationId = string.Empty;
        public string ToPortId = string.Empty;
    }

    /// <summary>Editorが実ファイルと画素契約を確認して作るsource-bound証跡。boolだけで認定しない。</summary>
    [Serializable]
    public sealed class StudyANativeLayoutAcceptedCatalog
    {
        public string LayoutSetId = string.Empty;
        public int LayoutSetVersion;
        public StudyANativeVariantPreflightReceipt[] VariantReceipts = Array.Empty<StudyANativeVariantPreflightReceipt>();
    }

    [Serializable]
    public sealed class StudyANativeVariantPreflightReceipt
    {
        public string VariantId = string.Empty;
        public string DefinitionSHA256 = string.Empty;
        public string PrefabSHA256 = string.Empty;
        public string GroundSHA256 = string.Empty;
        public int PortBandVersion;
        public string MaskSHA256 = string.Empty;
        public string PreflightReceiptSHA256 = string.Empty;
    }

    [Serializable]
    public sealed class StudyANativeLayoutResolvedVariant
    {
        public string LocationId = string.Empty;
        public string VariantId = string.Empty;
        public string DefinitionSHA256 = string.Empty;
        public string SourceGroundSHA256 = string.Empty;
        public string PrefabSHA256 = string.Empty;
        public string GroundSHA256 = string.Empty;
        public int PortBandVersion;
        public string MaskSHA256 = string.Empty;
        public string PreflightReceiptSHA256 = string.Empty;
    }

    [Serializable]
    public sealed class StudyANativeLayoutReproduction
    {
        public string LayoutSetId = string.Empty;
        public int LayoutSetVersion;
        public string RecipeId = string.Empty;
        public int RecipeVersion;
        public int Seed;
        public StudyANativeLayoutResolvedVariant[] Variants = Array.Empty<StudyANativeLayoutResolvedVariant>();
    }

    /// <summary>v1の地形hashと、素材variantを含むv2内容hash・再現証跡を分けて返す。</summary>
    [Serializable]
    public sealed class StudyANativeLayoutAssemblyResult
    {
        public string RecipeId = string.Empty;
        public StudyANativeLocationCatalog EffectiveCatalog;
        public StudyANativeLocationAssembly Assembly;
        public StudyANativeLayoutResolvedVariant[] Variants = Array.Empty<StudyANativeLayoutResolvedVariant>();
        public string ContentHash = string.Empty;
        public StudyANativeLayoutReproduction Reproduction;
    }

    public static partial class StudyABlockGenerator
    {
        // UTF8の "FantasyRoyale.NativeGroundEditMask.v1\nmode=no-repaint\n"。Ground画像のhashとは別の契約。
        public const string NativeNoRepaintGroundMaskSHA256 = "d74ed6f9538e75e9f68a8385449418896ce67a34c1e6362eb5358f57609b44cd";
        public const string NativeEPortCompatibilityKey = "native-band-v2/grass-family-1/soil64/height0/phase-port-centred";

        /// <summary>認定可能なrecipeだけを独立PRNGで選び、元v1生成器へ同seedを渡す。</summary>
        public static StudyANativeLayoutAssemblyResult GenerateNativeLayoutAssembly(
            StudyANativeLocationLayoutSet set, StudyANativeLayoutAcceptedCatalog accepted, int seed,
            string requestedRecipeId = "")
        {
            RequireNativeLayoutSet(set);
            RequireNativeLayoutEvidenceShape(set, accepted);
            var automatic = string.IsNullOrEmpty(requestedRecipeId) || requestedRecipeId == "Auto";
            var eligible = set.Recipes.Where(r => NativeLayoutUnavailableReason(set, accepted, r) == string.Empty)
                .OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
            StudyANativeLocationLayoutRecipe recipe;
            if (automatic)
            {
                if (eligible.Length == 0) throw new ArgumentException("No native layout recipe has matching source-bound preflight receipts.");
                // v1のcatalog seed/PRNGには混ぜず、recipe選択だけを別のstreamで再現する。
                var selection = new NativeRandom("native-layout-selection/" + set.Id, set.Version, seed);
                recipe = eligible[selection.Next(eligible.Length)];
            }
            else
            {
                recipe = set.Recipes.FirstOrDefault(r => r.Id == requestedRecipeId);
                if (recipe == null) throw new ArgumentException("Requested native layout recipe is not declared: " + requestedRecipeId);
                var unavailable = NativeLayoutUnavailableReason(set, accepted, recipe);
                if (unavailable != string.Empty) throw new ArgumentException("Requested native layout recipe is not accepted: " + recipe.Id + ". " + unavailable);
            }
            var effective = recipe.UseBaseCatalog ? set.BaseCatalog : NativeResolveVariantCatalog(set, recipe);
            // S0は元catalogそのものとseedをそのまま使う。旧subset入口は変更しない。
            var assembly = GenerateNativeAssembly(effective, seed, 4, 4, 4);
            RequireNativeRecipeOutput(recipe, assembly);
            if (!recipe.UseBaseCatalog)
                assembly.FixedFullCatalogLayoutQualification = "Accepted recipe E selects the fixed A-B-D-C normalized layout. Different seeds within one recipe do not prove different geometry; compare v2 ContentHash across recipes/material variants.";
            var variants = recipe.Bindings.OrderBy(b => b.LocationId, StringComparer.Ordinal).Select(b =>
            {
                var v = set.Variants.First(x => x.Id == b.VariantId);
                var receipt = accepted.VariantReceipts.First(x => x.VariantId == v.Id);
                return new StudyANativeLayoutResolvedVariant { LocationId = b.LocationId, VariantId = v.Id,
                    DefinitionSHA256 = NativeLayoutDefinitionHash(v.Definition),
                    SourceGroundSHA256 = v.SourceGroundSHA256, PrefabSHA256 = v.Definition.PrefabSHA256,
                    GroundSHA256 = v.Definition.GroundSHA256, PortBandVersion = v.PortBandVersion,
                    MaskSHA256 = v.MaskSHA256, PreflightReceiptSHA256 = receipt.PreflightReceiptSHA256 };
            }).ToArray();
            return new StudyANativeLayoutAssemblyResult { RecipeId = recipe.Id, EffectiveCatalog = effective,
                Assembly = assembly, Variants = variants, ContentHash = NativeLayoutContentHash(assembly, variants),
                Reproduction = new StudyANativeLayoutReproduction { LayoutSetId = set.Id, LayoutSetVersion = set.Version,
                    RecipeId = recipe.Id, RecipeVersion = recipe.Version, Seed = seed, Variants = variants } };
        }

        /// <summary>未納品・証跡不一致のrecipeを除外した、現在選択できるIDを返す。</summary>
        public static string[] GetAvailableNativeLayoutRecipeIds(StudyANativeLocationLayoutSet set,
            StudyANativeLayoutAcceptedCatalog accepted)
        {
            RequireNativeLayoutSet(set);
            RequireNativeLayoutEvidenceShape(set, accepted);
            return set.Recipes.Where(r => NativeLayoutUnavailableReason(set, accepted, r) == string.Empty)
                .Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Editorが確認した実byte hashと期待値を照合する。物理ファイルや画素はこの純APIでは検査しない。</summary>
        public static StudyANativeVariantPreflightReceipt CreateNativeVariantPreflightReceipt(
            StudyANativeLocationVariant variant, string actualPrefabSHA256, string actualGroundSHA256,
            int verifiedPortBandVersion, string verifiedMaskSHA256, string preflightReceiptSHA256)
        {
            if (variant == null || !NativeIdentity(variant.Id) || variant.Definition == null || !NativeLayoutSHA(variant.Definition.PrefabSHA256) ||
                !NativeLayoutSHA(variant.Definition.GroundSHA256) || !NativeLayoutSHA(variant.MaskSHA256) ||
                !NativeLayoutSHA(preflightReceiptSHA256) || actualPrefabSHA256 != variant.Definition.PrefabSHA256 ||
                actualGroundSHA256 != variant.Definition.GroundSHA256 || verifiedPortBandVersion != variant.PortBandVersion ||
                verifiedMaskSHA256 != variant.MaskSHA256)
                throw new ArgumentException("Native variant preflight hashes/version do not match its exact expected source contract.");
            return new StudyANativeVariantPreflightReceipt { VariantId = variant.Id, DefinitionSHA256 = NativeLayoutDefinitionHash(variant.Definition),
                PrefabSHA256 = actualPrefabSHA256, GroundSHA256 = actualGroundSHA256,
                PortBandVersion = verifiedPortBandVersion, MaskSHA256 = verifiedMaskSHA256,
                PreflightReceiptSHA256 = preflightReceiptSHA256 };
        }

        private static void RequireNativeLayoutSet(StudyANativeLocationLayoutSet set)
        {
            if (set == null || !NativeIdentity(set.Id) || set.Version <= 0 || set.PixelsPerUnit != 64 ||
                set.BaseCatalog == null || set.Variants == null || set.Recipes == null || set.Recipes.Length == 0)
                throw new ArgumentException("Native layout set needs an ID/version, PPU64, unchanged base catalog, variants and recipes.");
            var errors = NativeCatalogErrors(set.BaseCatalog);
            if (errors.Count != 0) throw new ArgumentException("Native layout base catalog is invalid: " + string.Join("; ", errors));
            var logical = new[] { "A", "B", "C", "D" };
            if (!set.BaseCatalog.Locations.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(logical))
                throw new ArgumentException("Native layouts require exactly the logical locations A/B/C/D.");
            var variants = new Dictionary<string, StudyANativeLocationVariant>(StringComparer.Ordinal);
            foreach (var v in set.Variants)
            {
                if (v == null || !NativeIdentity(v.Id) || variants.ContainsKey(v.Id) || !logical.Contains(v.LogicalLocationId) ||
                    v.Definition == null || v.Definition.Id != v.LogicalLocationId || v.PortBandVersion <= 0 ||
                    !NativeLayoutSHA(v.SourceGroundSHA256) ||
                    (!string.IsNullOrEmpty(v.Definition.GroundSHA256) && !NativeLayoutSHA(v.Definition.GroundSHA256)) ||
                    (!string.IsNullOrEmpty(v.MaskSHA256) && !NativeLayoutSHA(v.MaskSHA256)))
                    throw new ArgumentException("Native variant identity/source contract is invalid; pending ground/mask hashes must be empty or exact SHA256.");
                variants.Add(v.Id, v);
                var original = set.BaseCatalog.Locations.First(d => d.Id == v.LogicalLocationId);
                // 地面と口だけを変える。元prefabの所有者・足・source位置はvariant間で共有する。
                if (v.SourceGroundSHA256 != original.GroundSHA256 || !NativeVariantKeepsOriginalPrefab(v.Definition, original))
                    throw new ArgumentException("Native variant changed its original prefab/owner/foot/source-ground contract: " + v.Id);
                var unchangedGround = v.Definition.GroundPath == original.GroundPath && v.Definition.GroundSHA256 == original.GroundSHA256;
                if (unchangedGround && v.MaskSHA256 != NativeNoRepaintGroundMaskSHA256 ||
                    !unchangedGround && v.MaskSHA256 == NativeNoRepaintGroundMaskSHA256)
                    throw new ArgumentException("Unchanged ground requires the canonical no-repaint mask; edited ground requires its actual edit-mask SHA: " + v.Id);
                var single = new StudyANativeLocationCatalog { Id = "variant-shape", Version = 1, PixelsPerUnit = 64,
                    Locations = new[] { v.Definition }, SharedEdgeMatches = Array.Empty<StudyANativeSharedEdgeMatch>() };
                errors = NativeCatalogErrors(single);
                if (errors.Count != 0) throw new ArgumentException("Native variant is invalid: " + v.Id + ". " + string.Join("; ", errors));
            }
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in set.Recipes)
            {
                if (r == null || (r.Id != "S0" && r.Id != "E") || !recipeIds.Add(r.Id) || r.Version <= 0 ||
                    !NativeIdentity(r.CatalogId) || r.CatalogVersion <= 0 || r.Bindings == null || r.Connections == null ||
                    r.SharedEdgeMatches == null || r.SharedEdgeMatches.Any(m => m == null) || r.Connections.Any(c => c == null) ||
                    r.Bindings.Length != 4 || r.Connections.Length != 3 || r.UseBaseCatalog != (r.Id == "S0"))
                    throw new ArgumentException("Native recipe must be explicit S0 or E with four bindings and three primary connections.");
                if (r.Bindings.Any(b => b == null) || !r.Bindings.Select(b => b.LocationId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(logical))
                    throw new ArgumentException("Native recipe must resolve each logical location exactly once.");
                foreach (var b in r.Bindings)
                {
                    StudyANativeLocationVariant v;
                    if (!variants.TryGetValue(b.VariantId ?? string.Empty, out v) || v.LogicalLocationId != b.LocationId ||
                        b.OriginPx != NativeRecipeExpectedOrigin(r.Id, b.LocationId))
                        throw new ArgumentException("Native recipe variant binding or certified integer origin is invalid: " + r.Id);
                    if (r.UseBaseCatalog && NativeLayoutDefinitionHash(v.Definition) !=
                        NativeLayoutDefinitionHash(set.BaseCatalog.Locations.First(d => d.Id == b.LocationId)))
                        throw new ArgumentException("S0 must bind unchanged original catalog definitions.");
                }
                if (r.UseBaseCatalog && (r.CatalogId != set.BaseCatalog.Id || r.CatalogVersion != set.BaseCatalog.Version ||
                    NativeLayoutMatchKeys(r.SharedEdgeMatches).SequenceEqual(NativeLayoutMatchKeys(set.BaseCatalog.SharedEdgeMatches)) == false))
                    throw new ArgumentException("S0 must retain original v1 catalog identity/version/shared edge rules.");
                var effective = r.UseBaseCatalog ? set.BaseCatalog : NativeResolveVariantCatalog(set, r);
                if (r.Id == "E") RequireNativeEVariantProfile(set.BaseCatalog, effective);
                errors = NativeCatalogErrors(effective);
                if (errors.Count != 0) throw new ArgumentException("Resolved native recipe catalog is invalid: " + string.Join("; ", errors));
                var probe = NativeRecipeProbe(r, effective);
                errors = ValidateNativeAssembly(effective, probe);
                if (errors.Count != 0) throw new ArgumentException("Native recipe geometry/ports/shared contacts are invalid: " + r.Id + ". " + string.Join("; ", errors));
                var graph = r.Connections.Select(c => NativeLayoutPair(c.FromLocationId, c.ToLocationId)).OrderBy(x => x, StringComparer.Ordinal);
                var expected = r.Id == "S0" ? new[] { "A-B", "B-C", "C-D" } : new[] { "A-B", "B-D", "C-D" };
                if (!graph.SequenceEqual(expected)) throw new ArgumentException("Native recipe primary graph does not match its declared S0/E profile.");
                if (r.Id == "E" && !NativeLayoutMatchKeys(r.SharedEdgeMatches).SequenceEqual(NativeLayoutMatchKeys(new[] {
                    new StudyANativeSharedEdgeMatch { FromLocationId="A", ToLocationId="B", FromDirection=StudyAPortDirection.East, LengthPx=640 },
                    new StudyANativeSharedEdgeMatch { FromLocationId="B", ToLocationId="D", FromDirection=StudyAPortDirection.East, FromAlongMinPx=128, LengthPx=512 },
                    new StudyANativeSharedEdgeMatch { FromLocationId="C", ToLocationId="D", FromDirection=StudyAPortDirection.North, LengthPx=512 },
                    new StudyANativeSharedEdgeMatch { FromLocationId="B", ToLocationId="C", FromDirection=StudyAPortDirection.East, ToAlongMinPx=256, LengthPx=128 } })))
                    throw new ArgumentException("E requires exactly AB640/BD512/CD512 and incidental BC128; old shared rules cannot leak into E.");
            }
            if (!recipeIds.Contains("S0")) throw new ArgumentException("Native layout set must retain original recipe S0.");
        }

        private static void RequireNativeLayoutEvidenceShape(StudyANativeLocationLayoutSet set, StudyANativeLayoutAcceptedCatalog accepted)
        {
            if (accepted == null) return;
            if (accepted.VariantReceipts == null || accepted.VariantReceipts.Any(r => r == null) ||
                accepted.VariantReceipts.Select(r => r.VariantId).Distinct(StringComparer.Ordinal).Count() != accepted.VariantReceipts.Length ||
                accepted.VariantReceipts.Any(r => !set.Variants.Any(v => v.Id == r.VariantId)))
                throw new ArgumentException("Native preflight evidence has null/duplicate/unknown variant receipts.");
        }

        private static string NativeLayoutUnavailableReason(StudyANativeLocationLayoutSet set,
            StudyANativeLayoutAcceptedCatalog accepted, StudyANativeLocationLayoutRecipe recipe)
        {
            if (accepted == null || accepted.LayoutSetId != set.Id || accepted.LayoutSetVersion != set.Version)
                return "Source preflight is absent or belongs to a different layout-set version.";
            foreach (var b in recipe.Bindings)
            {
                var v = set.Variants.First(x => x.Id == b.VariantId);
                if (!NativeLayoutSHA(v.Definition.PrefabSHA256) || !NativeLayoutSHA(v.Definition.GroundSHA256) || !NativeLayoutSHA(v.MaskSHA256))
                    return "Variant is awaiting exact delivered source hashes/mask: " + v.Id;
                var r = accepted.VariantReceipts.FirstOrDefault(x => x.VariantId == v.Id);
                if (r == null || r.PrefabSHA256 != v.Definition.PrefabSHA256 || r.GroundSHA256 != v.Definition.GroundSHA256 ||
                    r.DefinitionSHA256 != NativeLayoutDefinitionHash(v.Definition) ||
                    r.PortBandVersion != v.PortBandVersion || r.MaskSHA256 != v.MaskSHA256 || !NativeLayoutSHA(r.PreflightReceiptSHA256))
                    return "Matching source-bound Editor preflight receipt is missing: " + v.Id;
            }
            return string.Empty;
        }

        private static StudyANativeLocationCatalog NativeResolveVariantCatalog(StudyANativeLocationLayoutSet set,
            StudyANativeLocationLayoutRecipe recipe)
        {
            return new StudyANativeLocationCatalog { Id = recipe.CatalogId, Version = recipe.CatalogVersion, PixelsPerUnit = 64,
                Locations = recipe.Bindings.OrderBy(b => b.LocationId, StringComparer.Ordinal)
                    .Select(b => NativeLayoutCopyDefinition(set.Variants.First(v => v.Id == b.VariantId).Definition)).ToArray(),
                SharedEdgeMatches = recipe.SharedEdgeMatches.Select(m => new StudyANativeSharedEdgeMatch {
                    FromLocationId = m.FromLocationId, ToLocationId = m.ToLocationId, FromDirection = m.FromDirection,
                    FromAlongMinPx = m.FromAlongMinPx, ToAlongMinPx = m.ToAlongMinPx, LengthPx = m.LengthPx }).ToArray() };
        }

        private static StudyANativeLocationDefinition NativeLayoutCopyDefinition(StudyANativeLocationDefinition d) =>
            new StudyANativeLocationDefinition { Id=d.Id, PrefabPath=d.PrefabPath, GroundPath=d.GroundPath,
                ExtentPx=d.ExtentPx, SpawnPx=d.SpawnPx, PrefabSHA256=d.PrefabSHA256, GroundSHA256=d.GroundSHA256,
                SourceOwnerIds=d.SourceOwnerIds.ToArray(), FootUnits=d.FootUnits.Select(f => new StudyANativeFootUnit {
                    OwnerId=f.OwnerId, PartPath=f.PartPath, TieOrder=f.TieOrder }).ToArray(),
                Ports=d.Ports.Select(p => new StudyANativeLocationPort { Id=p.Id, Direction=p.Direction, AnchorPx=p.AnchorPx,
                    AlongMinOffsetPx=p.AlongMinOffsetPx, MouthWidthPx=p.MouthWidthPx, Terrain=p.Terrain,
                    CompatibilityKey=p.CompatibilityKey, Height=p.Height }).ToArray() };

        private static bool NativeVariantKeepsOriginalPrefab(StudyANativeLocationDefinition d, StudyANativeLocationDefinition original) =>
            d.PrefabPath == original.PrefabPath && d.PrefabSHA256 == original.PrefabSHA256 && d.ExtentPx == original.ExtentPx &&
            d.SpawnPx == original.SpawnPx && d.SourceOwnerIds != null && original.SourceOwnerIds.SequenceEqual(d.SourceOwnerIds) &&
            d.FootUnits != null && d.FootUnits.All(f => f != null) &&
            original.FootUnits.Select(NativeLayoutFootKey).SequenceEqual(d.FootUnits.Select(NativeLayoutFootKey));

        private static string NativeLayoutFootKey(StudyANativeFootUnit f) => f.OwnerId + "|" + f.PartPath + "|" + f.TieOrder.ToString(CultureInfo.InvariantCulture);

        private static Vector2Int NativeRecipeExpectedOrigin(string recipe, string logical) =>
            recipe == "S0" ? logical == "A" ? Vector2Int.zero : logical == "B" ? new Vector2Int(768,0) :
                logical == "C" ? new Vector2Int(1152,128) : new Vector2Int(1152,512) :
                logical == "A" ? new Vector2Int(0,256) : logical == "B" ? new Vector2Int(768,256) :
                logical == "C" ? new Vector2Int(1152,0) : new Vector2Int(1152,384);

        private static StudyANativeLocationAssembly NativeRecipeProbe(StudyANativeLocationLayoutRecipe r, StudyANativeLocationCatalog c)
        {
            var ordered = r.Bindings.OrderBy(b => b.LocationId, StringComparer.Ordinal).ToArray();
            var result = new StudyANativeLocationAssembly { CatalogVersion = c.Version,
                Blocks=ordered.Select(b => new StudyANativePlacedLocation { InstanceId=b.LocationId, LocationId=b.LocationId,
                    OriginPx=b.OriginPx, ExtentPx=c.Locations.First(d => d.Id == b.LocationId).ExtentPx }).ToList() };
            foreach (var link in r.Connections)
            {
                if (link == null) throw new ArgumentException("Native recipe connection is null.");
                result.Connections.Add(new StudyANativeLocationConnection {
                    FromBlock=Array.FindIndex(ordered,b => b.LocationId == link.FromLocationId), FromPortId=link.FromPortId,
                    ToBlock=Array.FindIndex(ordered,b => b.LocationId == link.ToLocationId), ToPortId=link.ToPortId });
            }
            result.NormalizedBoundsPx = NativeBounds(result.Blocks);
            return result;
        }

        private static void RequireNativeRecipeOutput(StudyANativeLocationLayoutRecipe recipe, StudyANativeLocationAssembly assembly)
        {
            foreach (var b in recipe.Bindings)
                if (!assembly.Blocks.Any(x => x.LocationId == b.LocationId && x.OriginPx == b.OriginPx))
                    throw new ArgumentException("Generated native layout differs from its certified recipe origins.");
            var actual = assembly.Connections.Select(c => NativeLayoutConnectionKey(assembly.Blocks[c.FromBlock].LocationId,
                c.FromPortId, assembly.Blocks[c.ToBlock].LocationId, c.ToPortId)).OrderBy(x => x, StringComparer.Ordinal);
            var expected = recipe.Connections.Select(c => NativeLayoutConnectionKey(c.FromLocationId,c.FromPortId,c.ToLocationId,c.ToPortId))
                .OrderBy(x => x, StringComparer.Ordinal);
            if (!actual.SequenceEqual(expected)) throw new ArgumentException("Generated native layout differs from its certified primary graph/ports.");
        }

        private static string NativeLayoutPair(string a, string b) => StringComparer.Ordinal.Compare(a,b) < 0 ? a + "-" + b : b + "-" + a;
        private static string NativeLayoutConnectionKey(string a, string ap, string b, string bp)
        {
            var x = a + "/" + ap; var y = b + "/" + bp;
            return StringComparer.Ordinal.Compare(x,y) < 0 ? x + ">" + y : y + ">" + x;
        }

        private static IEnumerable<string> NativeLayoutMatchKeys(IEnumerable<StudyANativeSharedEdgeMatch> matches) =>
            matches.Select(m => m == null ? "null" : m.FromLocationId + "/" + m.ToLocationId + "/" + (int)m.FromDirection + "/" +
                m.FromAlongMinPx.ToString(CultureInfo.InvariantCulture) + "/" + m.ToAlongMinPx.ToString(CultureInfo.InvariantCulture) + "/" +
                m.LengthPx.ToString(CultureInfo.InvariantCulture)).OrderBy(x => x, StringComparer.Ordinal);

        private static string NativeLayoutContentHash(StudyANativeLocationAssembly assembly, StudyANativeLayoutResolvedVariant[] variants)
        {
            var text = new StringBuilder("native-layout-material-v2|"); NativeHashField(text,assembly.ContentHash);
            foreach (var v in variants.OrderBy(x => x.LocationId,StringComparer.Ordinal))
            {
                NativeHashField(text,v.LocationId); NativeHashField(text,v.VariantId); NativeHashField(text,v.SourceGroundSHA256);
                NativeHashField(text,v.DefinitionSHA256);
                NativeHashField(text,v.PrefabSHA256); NativeHashField(text,v.GroundSHA256);
                NativeHashField(text,v.PortBandVersion.ToString(CultureInfo.InvariantCulture)); NativeHashField(text,v.MaskSHA256);
            }
            // 証跡のrun/time/hashとseedは再現recordだけへ記録し、内容の種類を水増ししない。
            return NativeLayoutHash(text.ToString());
        }

        private static string NativeLayoutDefinitionHash(StudyANativeLocationDefinition d)
        {
            var text = new StringBuilder();
            foreach (var value in new[] { d.Id,d.PrefabPath,d.GroundPath,d.PrefabSHA256,d.GroundSHA256 }) NativeHashField(text,value ?? string.Empty);
            NativeHashField(text,d.ExtentPx.x.ToString(CultureInfo.InvariantCulture) + "," + d.ExtentPx.y.ToString(CultureInfo.InvariantCulture));
            NativeHashField(text,d.SpawnPx.x.ToString("R",CultureInfo.InvariantCulture) + "," + d.SpawnPx.y.ToString("R",CultureInfo.InvariantCulture));
            foreach (var owner in d.SourceOwnerIds) NativeHashField(text,owner);
            foreach (var foot in d.FootUnits) NativeHashField(text,NativeLayoutFootKey(foot));
            foreach (var p in d.Ports)
            {
                NativeHashField(text,p.Id); NativeHashField(text,((int)p.Direction).ToString(CultureInfo.InvariantCulture));
                NativeHashField(text,p.AnchorPx.x.ToString(CultureInfo.InvariantCulture) + "," + p.AnchorPx.y.ToString(CultureInfo.InvariantCulture)); NativeHashField(text,p.AlongMinOffsetPx.ToString(CultureInfo.InvariantCulture));
                NativeHashField(text,p.MouthWidthPx.ToString(CultureInfo.InvariantCulture)); NativeHashField(text,((int)p.Terrain).ToString(CultureInfo.InvariantCulture));
                NativeHashField(text,p.CompatibilityKey); NativeHashField(text,p.Height.ToString(CultureInfo.InvariantCulture));
            }
            return NativeLayoutHash(text.ToString());
        }

        private static void RequireNativeEVariantProfile(StudyANativeLocationCatalog original, StudyANativeLocationCatalog effective)
        {
            foreach (var id in new[] { "A", "C" })
                if (NativeLayoutDefinitionHash(original.Locations.First(d => d.Id == id)) !=
                    NativeLayoutDefinitionHash(effective.Locations.First(d => d.Id == id)))
                    throw new ArgumentException("E must preserve complete original A/C definitions, including unused C west port.");
            var b = effective.Locations.First(d => d.Id == "B"); var oldB = original.Locations.First(d => d.Id == "B");
            var dWest = effective.Locations.First(d => d.Id == "D"); var oldD = original.Locations.First(d => d.Id == "D");
            if (b.Ports == null || dWest.Ports == null || b.Ports.Any(p => p == null) || dWest.Ports.Any(p => p == null) ||
                b.Ports.Length != oldB.Ports.Length || dWest.Ports.Length != oldD.Ports.Length + 1 ||
                b.Ports.Count(p => p.Direction == StudyAPortDirection.East) != 1 ||
                dWest.Ports.Count(p => p.Direction == StudyAPortDirection.West) != 1 ||
                !b.Ports.Where(p => p.Direction != StudyAPortDirection.East).Select(NativeLayoutPortKey).SequenceEqual(
                    oldB.Ports.Where(p => p.Direction != StudyAPortDirection.East).Select(NativeLayoutPortKey)) ||
                !dWest.Ports.Where(p => p.Direction != StudyAPortDirection.West).Select(NativeLayoutPortKey).SequenceEqual(oldD.Ports.Select(NativeLayoutPortKey)))
                throw new ArgumentException("E must preserve B west/D south and add only the certified B-east/D-west connection.");
            var be = b.Ports.Single(p => p.Direction == StudyAPortDirection.East);
            var dw = dWest.Ports.Single(p => p.Direction == StudyAPortDirection.West);
            if (be.Id != oldB.Ports.Single(p => p.Direction == StudyAPortDirection.East).Id ||
                !NativeIsEPort(be, StudyAPortDirection.East, new Vector2Int(384,297)) ||
                !NativeIsEPort(dw, StudyAPortDirection.West, new Vector2Int(0,169)) ||
                b.GroundPath == oldB.GroundPath || dWest.GroundPath == oldD.GroundPath)
                throw new ArgumentException("E B-east/D-west needs its fixed soil64/-32/height0 semantic profile and new ground paths.");
        }

        private static bool NativeIsEPort(StudyANativeLocationPort p, StudyAPortDirection direction, Vector2Int anchor) =>
            p.Direction == direction && p.AnchorPx == anchor && p.MouthWidthPx == 64 && p.AlongMinOffsetPx == -32 &&
            p.Terrain == StudyATerrain.Soil && p.Height == 0 && p.CompatibilityKey == NativeEPortCompatibilityKey;

        private static string NativeLayoutPortKey(StudyANativeLocationPort p)
        {
            var text = new StringBuilder();
            foreach (var value in new[] { p.Id, ((int)p.Direction).ToString(CultureInfo.InvariantCulture),
                p.AnchorPx.x.ToString(CultureInfo.InvariantCulture), p.AnchorPx.y.ToString(CultureInfo.InvariantCulture),
                p.MouthWidthPx.ToString(CultureInfo.InvariantCulture),p.AlongMinOffsetPx.ToString(CultureInfo.InvariantCulture),
                ((int)p.Terrain).ToString(CultureInfo.InvariantCulture),p.Height.ToString(CultureInfo.InvariantCulture),p.CompatibilityKey })
                NativeHashField(text,value);
            return text.ToString();
        }

        private static bool NativeLayoutSHA(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        private static string NativeLayoutHash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(b => b.ToString("x2",CultureInfo.InvariantCulture)));
        }
    }
}
