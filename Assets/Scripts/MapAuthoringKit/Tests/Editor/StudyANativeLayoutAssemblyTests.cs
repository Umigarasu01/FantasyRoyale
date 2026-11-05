using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FantasyRoyale.MapAuthoringKit.OverheadStudyA;
using NUnit.Framework;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Tests.Editor
{
    /// <summary>合成したmetadataだけでrecipe選択と旧API保全を確認する。実素材受入れやUnity描画の代用ではない。</summary>
    public sealed class StudyANativeLayoutAssemblyTests
    {
        [Test]
        public void PendingEIsExcludedAndItsExplicitRequestIsRejected()
        {
            var set = LayoutSet(false);
            var accepted = Evidence(set, false);
            Assert.That(StudyABlockGenerator.GetAvailableNativeLayoutRecipeIds(set, accepted), Is.EqualTo(new[] { "S0" }));
            Assert.That(StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 10).RecipeId, Is.EqualTo("S0"));
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 10, "E"));
        }

        [Test]
        public void MissingSourceEvidenceCannotBeReplacedByARecipeFlag()
        {
            var set = LayoutSet(true);
            Assert.That(StudyABlockGenerator.GetAvailableNativeLayoutRecipeIds(set, null), Is.Empty);
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GenerateNativeLayoutAssembly(set, null, 1, "S0"));
            var v = set.Variants.First(x => x.Id == "B_E");
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.CreateNativeVariantPreflightReceipt(v,
                v.Definition.PrefabSHA256, Hash("wrong actual ground bytes"), v.PortBandVersion, v.MaskSHA256, Hash("receipt")));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(20261006)]
        public void S0CallsTheOriginalCatalogAndKeepsItsNativeHash(int seed)
        {
            var set = LayoutSet(false);
            var before = JsonUtility.ToJson(set.BaseCatalog);
            var original = StudyABlockGenerator.GenerateNativeAssembly(set.BaseCatalog, seed, 4, 4, 4);
            var wrapped = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, Evidence(set, false), seed, "S0");
            Assert.That(ReferenceEquals(wrapped.EffectiveCatalog, set.BaseCatalog), Is.True);
            Assert.That(wrapped.Assembly.ContentHash, Is.EqualTo(original.ContentHash));
            Assert.That(wrapped.Assembly.FixedFullCatalogLayoutQualification, Is.EqualTo(original.FixedFullCatalogLayoutQualification));
            Assert.That(wrapped.Assembly.Seed, Is.EqualTo(seed));
            Assert.That(JsonUtility.ToJson(set.BaseCatalog), Is.EqualTo(before));
        }

        [Test]
        public void AcceptedEHasExactlyOneLogicalDefinitionAndTheCertifiedNewGeometry()
        {
            var set = LayoutSet(true);
            var before = JsonUtility.ToJson(set);
            var e = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, Evidence(set, true), 42, "E");
            Assert.That(e.Assembly.NormalizedBoundsPx, Is.EqualTo(new RectInt(0, 0, 1792, 896)));
            Assert.That(e.Assembly.Blocks.OrderBy(b => b.LocationId).Select(b => b.OriginPx), Is.EqualTo(new[] {
                new Vector2Int(0,256),new Vector2Int(768,256),new Vector2Int(1152,0),new Vector2Int(1152,384) }));
            Assert.That(e.EffectiveCatalog.Locations.Select(d => d.Id), Is.EqualTo(new[] { "A", "B", "C", "D" }));
            Assert.That(e.EffectiveCatalog.Locations.SelectMany(d => d.SourceOwnerIds).Distinct().Count(), Is.EqualTo(4));
            Assert.That(StudyABlockGenerator.ValidateNativeAssembly(e.EffectiveCatalog, e.Assembly), Is.Empty);
            Assert.That(JsonUtility.ToJson(set), Is.EqualTo(before));
            e.EffectiveCatalog.Locations.First(d => d.Id == "B").Ports[0].Id = "changed result only";
            Assert.That(JsonUtility.ToJson(set), Is.EqualTo(before), "E must deep-copy definitions rather than edit base or variant metadata");
        }

        [TestCase("ground")]
        [TestCase("prefab")]
        [TestCase("mask")]
        [TestCase("version")]
        [TestCase("receipt")]
        [TestCase("definition")]
        public void MismatchedSourceBoundEvidenceExcludesOnlyTheAffectedRecipe(string field)
        {
            var set = LayoutSet(true);
            var accepted = Evidence(set, true);
            var r = accepted.VariantReceipts.First(x => x.VariantId == "B_E");
            if (field == "ground") r.GroundSHA256 = Hash("other ground");
            if (field == "prefab") r.PrefabSHA256 = Hash("other prefab");
            if (field == "mask") r.MaskSHA256 = Hash("other mask");
            if (field == "version") r.PortBandVersion++;
            if (field == "receipt") r.PreflightReceiptSHA256 = string.Empty;
            if (field == "definition") r.DefinitionSHA256 = Hash("other port metadata");
            Assert.That(StudyABlockGenerator.GetAvailableNativeLayoutRecipeIds(set, accepted), Is.EqualTo(new[] { "S0" }));
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 1, "E"));
        }

        [Test]
        public void CanonicalMaterialHashIgnoresSeedAndPreflightRunButIncludesPortBandVersion()
        {
            var set = LayoutSet(true);
            var a = Evidence(set, true);
            var first = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, a, 0, "E");
            foreach (var r in a.VariantReceipts) r.PreflightReceiptSHA256 = Hash("different successful run");
            var second = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, a, 109, "E");
            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
            Assert.That(second.Assembly.ContentHash, Is.EqualTo(first.Assembly.ContentHash));
            set.Variants.First(x => x.Id == "B_E").PortBandVersion++;
            var revised = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, Evidence(set, true), 0, "E");
            Assert.That(revised.Assembly.ContentHash, Is.EqualTo(first.Assembly.ContentHash));
            Assert.That(revised.ContentHash, Is.Not.EqualTo(first.ContentHash));
        }

        [Test]
        public void SameFourLocationsActuallyProduceTwoDifferentGeometryHashes()
        {
            var set = LayoutSet(true);
            var accepted = Evidence(set, true);
            var s0 = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 0, "S0");
            var e = StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 0, "E");
            Assert.That(e.Assembly.ContentHash, Is.Not.EqualTo(s0.Assembly.ContentHash));
            Assert.That(e.ContentHash, Is.Not.EqualTo(s0.ContentHash));
            Assert.That(e.Assembly.Blocks.Select(b => b.LocationId).OrderBy(x => x), Is.EqualTo(s0.Assembly.Blocks.Select(b => b.LocationId).OrderBy(x => x)));
        }

        [Test]
        public void UnchangedGroundCannotUseItsImageHashAsTheNoRepaintMask()
        {
            var set = LayoutSet(false);
            set.Variants.First(x => x.Id == "A_S0").MaskSHA256 = set.BaseCatalog.Locations[0].GroundSHA256;
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GetAvailableNativeLayoutRecipeIds(set, Evidence(set, false)));
        }

        [Test]
        public void ERejectsLegacySharedRulesAndChangedOwnerRegistration()
        {
            var set = LayoutSet(true);
            var accepted = Evidence(set, true);
            var e = set.Recipes.First(r => r.Id == "E");
            e.SharedEdgeMatches = e.SharedEdgeMatches.Concat(new[] { Match("B","D",StudyAPortDirection.East,512,0,128) }).ToArray();
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GenerateNativeLayoutAssembly(set, accepted, 0, "E"));
            set = LayoutSet(true);
            set.Variants.First(v => v.Id == "B_E").Definition.SourceOwnerIds = new[] { "owner_A" };
            Assert.Throws<ArgumentException>(() => StudyABlockGenerator.GenerateNativeLayoutAssembly(set, Evidence(set, true), 0, "E"));
        }

        private static StudyANativeLocationLayoutSet LayoutSet(bool deliveredE)
        {
            var c = new StudyANativeLocationCatalog { Locations=new[] {
                Location("A",768,640,Port("east",StudyAPortDirection.East,768,297,64,-32,"AB")),
                Location("B",384,640,Port("west",StudyAPortDirection.West,0,297,64,-32,"AB"),Port("east",StudyAPortDirection.East,384,297,64,-32,"BC")),
                Location("C",512,384,Port("west",StudyAPortDirection.West,0,169,64,-32,"BC"),Port("north",StudyAPortDirection.North,384,384,96,-48,"CD")),
                Location("D",640,512,Port("south",StudyAPortDirection.South,384,0,96,-48,"CD")) },
                SharedEdgeMatches=new[] { Match("A","B",StudyAPortDirection.East,0,0,640), Match("B","C",StudyAPortDirection.East,128,0,384),
                    Match("C","D",StudyAPortDirection.North,0,0,512),Match("B","D",StudyAPortDirection.East,512,0,128) } };
            var originals=c.Locations.Select(d => Variant(d.Id+"_S0",d.Id,d,d.GroundSHA256,StudyABlockGenerator.NativeNoRepaintGroundMaskSHA256)).ToArray();
            var b=Copy(c.Locations[1]); b.GroundPath="Assets/TestOnly/B_E.png";b.GroundSHA256=deliveredE?Hash("delivered fixture B_E"):string.Empty;
            b.Ports[1].CompatibilityKey=StudyABlockGenerator.NativeEPortCompatibilityKey;
            var d=Copy(c.Locations[3]);d.GroundPath="Assets/TestOnly/D_W.png";d.GroundSHA256=deliveredE?Hash("delivered fixture D_W"):string.Empty;
            d.Ports=d.Ports.Concat(new[] { Port("west",StudyAPortDirection.West,0,169,64,-32,StudyABlockGenerator.NativeEPortCompatibilityKey) }).ToArray();
            return new StudyANativeLocationLayoutSet { BaseCatalog=c,
                Variants=originals.Concat(new[] { Variant("B_E","B",b,c.Locations[1].GroundSHA256,Hash("B actual edit-mask fixture")),
                    Variant("D_W","D",d,c.Locations[3].GroundSHA256,Hash("D actual edit-mask fixture")) }).ToArray(),
                Recipes=new[] {
                    new StudyANativeLocationLayoutRecipe { Id="S0",UseBaseCatalog=true,CatalogId=c.Id,CatalogVersion=c.Version,
                        Bindings=new[] { Bind("A","A_S0",0,0),Bind("B","B_S0",768,0),Bind("C","C_S0",1152,128),Bind("D","D_S0",1152,512) },
                        Connections=new[] { Link("A","east","B","west"),Link("B","east","C","west"),Link("C","north","D","south") },SharedEdgeMatches=c.SharedEdgeMatches },
                    new StudyANativeLocationLayoutRecipe { Id="E",CatalogId="native-E-test-only",
                        Bindings=new[] { Bind("A","A_S0",0,256),Bind("B","B_E",768,256),Bind("C","C_S0",1152,0),Bind("D","D_W",1152,384) },
                        Connections=new[] { Link("A","east","B","west"),Link("B","east","D","west"),Link("C","north","D","south") },
                        SharedEdgeMatches=new[] { Match("A","B",StudyAPortDirection.East,0,0,640),Match("B","D",StudyAPortDirection.East,128,0,512),
                            Match("C","D",StudyAPortDirection.North,0,0,512),Match("B","C",StudyAPortDirection.East,0,256,128) } } } };
        }

        private static StudyANativeLayoutAcceptedCatalog Evidence(StudyANativeLocationLayoutSet set,bool includeE) =>
            new StudyANativeLayoutAcceptedCatalog { LayoutSetId=set.Id,LayoutSetVersion=set.Version,
                VariantReceipts=set.Variants.Where(v => v.Id.EndsWith("_S0",StringComparison.Ordinal) || includeE)
                    .Where(v => !string.IsNullOrEmpty(v.Definition.GroundSHA256)).Select(v =>
                        StudyABlockGenerator.CreateNativeVariantPreflightReceipt(v,v.Definition.PrefabSHA256,v.Definition.GroundSHA256,
                            v.PortBandVersion,v.MaskSHA256,Hash("successful test metadata preflight"))).ToArray() };
        private static StudyANativeLocationVariant Variant(string id,string logical,StudyANativeLocationDefinition d,string source,string mask) =>
            new StudyANativeLocationVariant { Id=id,LogicalLocationId=logical,Definition=d,SourceGroundSHA256=source,MaskSHA256=mask };
        private static StudyANativeLocationDefinition Location(string id,int w,int h,params StudyANativeLocationPort[] ports) =>
            new StudyANativeLocationDefinition { Id=id,PrefabPath="Assets/TestOnly/"+id+".prefab",GroundPath="Assets/TestOnly/"+id+".png",
                PrefabSHA256=Hash("source prefab "+id),GroundSHA256=Hash("source ground "+id),ExtentPx=new Vector2Int(w,h),
                SpawnPx=id=="A"?new Vector2(41.6f,297):Vector2.zero,Ports=ports,SourceOwnerIds=new[] { "owner_"+id },
                FootUnits=new[] { new StudyANativeFootUnit { OwnerId="owner_"+id,PartPath="PresentOwned/Foot" } } };
        private static StudyANativeLocationDefinition Copy(StudyANativeLocationDefinition d) => JsonUtility.FromJson<StudyANativeLocationDefinition>(JsonUtility.ToJson(d));
        private static StudyANativeLocationPort Port(string id,StudyAPortDirection dir,int x,int y,int width,int offset,string key) =>
            new StudyANativeLocationPort { Id=id,Direction=dir,AnchorPx=new Vector2Int(x,y),MouthWidthPx=width,AlongMinOffsetPx=offset,CompatibilityKey=key };
        private static StudyANativeSharedEdgeMatch Match(string a,string b,StudyAPortDirection dir,int af,int bf,int len) =>
            new StudyANativeSharedEdgeMatch { FromLocationId=a,ToLocationId=b,FromDirection=dir,FromAlongMinPx=af,ToAlongMinPx=bf,LengthPx=len };
        private static StudyANativeLocationVariantBinding Bind(string l,string v,int x,int y) =>
            new StudyANativeLocationVariantBinding { LocationId=l,VariantId=v,OriginPx=new Vector2Int(x,y) };
        private static StudyANativeLocationRecipeConnection Link(string a,string ap,string b,string bp) =>
            new StudyANativeLocationRecipeConnection { FromLocationId=a,FromPortId=ap,ToLocationId=b,ToPortId=bp };
        private static string Hash(string value)
        {
            using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
        }
    }
}
