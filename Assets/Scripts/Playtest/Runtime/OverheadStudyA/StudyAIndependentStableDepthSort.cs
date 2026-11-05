using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FantasyRoyale.Playtest.OverheadStudyA
{
    /// <summary>Explicit opt-in ordering for one independent study; exact foot Y precedes persistent authored ties.</summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class StudyAIndependentStableDepthSort : MonoBehaviour
    {
        [Serializable]
        public sealed class Unit
        {
            public string OwnerId;
            public string PartPath;
            public int TieOrder;
        }

        public sealed class UnitObservation
        {
            public string Key { get; private set; }
            public string OwnerId { get; private set; }
            public string PartPath { get; private set; }
            public int TieOrder { get; private set; }
            public SortingGroup Group { get; private set; }
            public Transform Foot { get; private set; }
            public float FootWorldY { get; private set; }
            public int AssignedOrder { get; private set; }
            public int OriginalOrder { get; private set; }
            public bool ActiveInHierarchy { get; private set; }
            public bool GroupEnabled { get; private set; }

            internal UnitObservation(Binding unit, float y, int order)
            {
                Key = unit.Key; OwnerId = unit.OwnerId; PartPath = unit.PartPath;
                TieOrder = unit.TieOrder; Group = unit.Group; Foot = unit.Group.transform;
                FootWorldY = y; AssignedOrder = order; OriginalOrder = unit.OriginalOrder;
                ActiveInHierarchy = unit.Group.gameObject.activeInHierarchy;
                GroupEnabled = unit.Group.enabled;
            }
        }

        public StudyABlockMovementDiagnostic Diagnostic;
        public string ExpectedMapScenePath;
        public bool EnableForExperiment;
        public Unit[] Units = Array.Empty<Unit>();
        public string HeroStableKey = "actor:hero";
        public int HeroTieOrder;

        public bool Applied { get; private set; }
        public string Failure { get; private set; } = string.Empty;
        public int BoundUnitCount => bindings == null ? 0 : bindings.Length;
        public int RefreshCount { get; private set; }
        public IReadOnlyList<UnitObservation> Observations => observations;

        internal sealed class Binding
        {
            public string Key, OwnerId, PartPath;
            public int TieOrder, OriginalOrder;
            public StudyAIndependentObjectState Owner;
            public SortingGroup Group;
            public SpriteRenderer[] Renderers;
            public int[] InnerOrders;
        }

        private sealed class RankedUnit
        {
            public Binding Binding;
            public float Y;
        }

        private const string WorldLayer = "WorldObjects";
        private const int SupportedTransparentQueue = 3000;
        private Binding[] bindings;
        private ReadOnlyCollection<UnitObservation> observations = Array.AsReadOnly(Array.Empty<UnitObservation>());
        private Scene boundScene;
        private GameObject boundActor;
        private bool hooksAttached;
        private bool refreshing;

        private void OnEnable()
        {
            if (!Application.isPlaying || hooksAttached) return;
            Camera.onPreCull += BeforeCameraCull;
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
            hooksAttached = true;
        }

        private void LateUpdate() { RefreshNow(); }

        private void BeforeCameraCull(Camera camera)
        {
            if (IsActualCamera(camera)) RefreshNow();
        }

        private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera)
        {
            if (IsActualCamera(camera)) RefreshNow();
        }

        private bool IsActualCamera(Camera camera) => isActiveAndEnabled && EnableForExperiment &&
            Diagnostic != null && Diagnostic.Ready && camera != null && camera == Diagnostic.GameplayCamera;

        /// <summary>Returns false until the existing diagnostic and actor render setup are ready. Never moves a foot or graphic.</summary>
        public bool RefreshNow()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return false;
            if (!EnableForExperiment) { RestoreOriginalOrders(); return false; }
            if (refreshing || Failure.Length != 0) return false;
            if (Diagnostic == null) return Fail("An explicit same-Entry diagnostic reference is required.");
            if (!Diagnostic.Ready) return false;
            refreshing = true;
            try
            {
                Scene scene; StudyAIndependentNavigation navigation; GameObject actor;
                if (!ReadyContext(out scene, out navigation, out actor)) return false;
                var candidates = bindings ?? Bind(scene, navigation, actor);
                Validate(candidates, scene, navigation, actor);
                var ranked = candidates.Select(unit => new RankedUnit { Binding = unit, Y = unit.Group.transform.position.y }).ToArray();
                Array.Sort(ranked, Compare);
                var next = new UnitObservation[ranked.Length];
                for (var i = 0; i < ranked.Length; ++i) next[i] = new UnitObservation(ranked[i].Binding, ranked[i].Y, i);

                // Validation and a complete plan precede every write. Roll back the frame if an engine setter fails.
                var previous = candidates.Select(unit => unit.Group.sortingOrder).ToArray();
                try
                {
                    for (var i = 0; i < ranked.Length; ++i)
                        if (ranked[i].Binding.Group.sortingOrder != i) ranked[i].Binding.Group.sortingOrder = i;
                    SortingGroup.UpdateAllSortingGroups();
                }
                catch
                {
                    for (var i = 0; i < candidates.Length; ++i)
                        if (candidates[i].Group != null) candidates[i].Group.sortingOrder = previous[i];
                    SortingGroup.UpdateAllSortingGroups();
                    throw;
                }
                if (bindings == null) { bindings = candidates; boundScene = scene; boundActor = actor; }
                observations = Array.AsReadOnly(next); Applied = true; RefreshCount++;
                return true;
            }
            catch (Exception error) { return Fail(error.Message); }
            finally { refreshing = false; }
        }

        private bool ReadyContext(out Scene scene, out StudyAIndependentNavigation navigation, out GameObject actor)
        {
            scene = default(Scene); navigation = null; actor = null;
            Require(ValidIdentity(ExpectedMapScenePath) && ExpectedMapScenePath.StartsWith("Assets/Scenes/MapAuthoring/OverheadStudyA/", StringComparison.Ordinal), "An explicit independent map Scene path is required.");
            Require(Diagnostic.gameObject.scene == gameObject.scene && Diagnostic.MapScenePath == ExpectedMapScenePath,
                "Diagnostic must belong to this Entry and select the exact expected map.");
            var setups = Diagnostic.GetComponents<StudyAIndependentActorRenderSetup>();
            Require(setups.Length == 1 && setups[0].Diagnostic == Diagnostic && setups[0].ExpectedMapScenePath == ExpectedMapScenePath,
                "The existing exact-map actor render setup is required.");
            if (!setups[0].Applied)
            {
                Require(setups[0].Failure.Length == 0, "Existing actor render setup failed: " + setups[0].Failure);
                return false;
            }
            Require(Diagnostic.Actor != null && Diagnostic.GameplayCamera != null, "Ready diagnostic Actor/camera references are missing.");
            actor = Diagnostic.Actor.gameObject; scene = actor.scene;
            Require(scene.IsValid() && scene.isLoaded && scene.path == ExpectedMapScenePath && Diagnostic.GameplayCamera.gameObject.scene == scene,
                "Actor and actual gameplay camera must belong to the exact loaded map.");
            Require(Diagnostic.GameplayCamera.transparencySortMode == TransparencySortMode.CustomAxis && Diagnostic.GameplayCamera.transparencySortAxis.Equals(Vector3.up),
                "Only the existing exact world-Y camera domain is supported.");
            var maps = scene.GetRootGameObjects().Where(root => root.name == "MapRoot").ToArray();
            Require(maps.Length == 1, "Exactly one scene-bound MapRoot is required.");
            navigation = maps[0].GetComponent<StudyAIndependentNavigation>();
            Require(navigation != null && navigation.Owners != null, "The existing explicit navigation-owner registry is required.");
            if (bindings != null) Require(boundScene == scene && boundActor == actor, "Bound map/Actor identity changed; disable the experiment before rebinding.");
            return true;
        }

        private Binding[] Bind(Scene scene, StudyAIndependentNavigation navigation, GameObject actor)
        {
            var owners = Owners(navigation, scene);
            Require(Units != null && Units.Length != 0 && Units.Length + 1 <= short.MaxValue, "A nonempty bounded authored unit registry is required.");
            Require(HeroStableKey == "actor:hero", "The authored actual-Hero key must be actor:hero.");
            var result = new List<Binding>();
            foreach (var unit in Units)
            {
                Require(unit != null && ValidIdentity(unit.OwnerId) && ValidPath(unit.PartPath), "Each unit needs an exact persistent owner ID and owned part path.");
                StudyAIndependentObjectState owner;
                Require(owners.TryGetValue(unit.OwnerId, out owner), "Authored owner is not in the exact map registry: " + unit.OwnerId);
                var foot = owner.transform.Find(unit.PartPath);
                Require(foot != null && foot != owner.transform && foot.IsChildOf(owner.transform), "Authored foot must be an owned descendant: " + unit.PartPath);
                var group = foot.GetComponent<SortingGroup>();
                Require(group != null, "Authored foot SortingGroup is missing: " + unit.PartPath);
                result.Add(Capture(group, owner, unit.OwnerId, unit.PartPath, unit.TieOrder, MapKey(unit.OwnerId, unit.PartPath)));
            }
            var heroGroup = actor.GetComponent<SortingGroup>();
            Require(heroGroup != null, "Existing actor render setup must create the actual Hero foot SortingGroup first.");
            result.Add(Capture(heroGroup, null, "actor", "hero", HeroTieOrder, HeroStableKey));
            return result.ToArray();
        }

        private static Binding Capture(SortingGroup group, StudyAIndependentObjectState owner, string ownerId, string path, int tieOrder, string key)
        {
            Require(group.sortingOrder == 0, "Initial outer SortingGroup order must retain the authored order0 domain: " + key);
            var renderers = group.GetComponentsInChildren<SpriteRenderer>(true);
            Require(renderers.Length != 0, "Each bound foot unit needs its existing graphics: " + key);
            return new Binding { Key = key, OwnerId = ownerId, PartPath = path, TieOrder = tieOrder, OriginalOrder = group.sortingOrder,
                Owner = owner, Group = group, Renderers = renderers, InnerOrders = renderers.Select(renderer => renderer.sortingOrder).ToArray() };
        }

        private void Validate(Binding[] units, Scene scene, StudyAIndependentNavigation navigation, GameObject actor)
        {
            Require(Units != null && Units.Length + 1 == units.Length && units.Length <= short.MaxValue && HeroStableKey == "actor:hero", "Authored registry size/Hero key changed.");
            var owners = Owners(navigation, scene); var configured = new Dictionary<string, Unit>(StringComparer.Ordinal);
            foreach (var unit in Units)
            {
                Require(unit != null && ValidIdentity(unit.OwnerId) && ValidPath(unit.PartPath), "Authored identity/path became invalid.");
                var key = MapKey(unit.OwnerId, unit.PartPath);
                Require(!configured.ContainsKey(key), "Duplicate persistent map-part key: " + key); configured.Add(key, unit);
            }
            var keys = new HashSet<string>(StringComparer.Ordinal); var groups = new HashSet<SortingGroup>();
            foreach (var unit in units)
            {
                Require(unit != null && unit.Group != null && keys.Add(unit.Key) && groups.Add(unit.Group), "Null/duplicate persistent unit or SortingGroup reference.");
                var group = unit.Group;
                Require(group.gameObject.scene == scene && group.enabled && group.sortingLayerName == WorldLayer && !group.sortAtRoot,
                    "Every mapped group must stay enabled, scene-bound and in the unchanged WorldObjects outer domain: " + unit.Key);
                Require(Finite(group.transform.position.y), "Nonfinite actual foot Y: " + unit.Key);
                for (var ancestor = group.transform.parent; ancestor != null; ancestor = ancestor.parent)
                    Require(ancestor.GetComponent<SortingGroup>() == null, "Nested outer SortingGroups are unsupported: " + unit.Key);
                if (unit.Owner == null)
                    Require(group.gameObject == actor && unit.Key == HeroStableKey && unit.TieOrder == HeroTieOrder, "Actual Hero binding/authored tie changed.");
                else
                {
                    Unit config; StudyAIndependentObjectState owner;
                    Require(configured.TryGetValue(unit.Key, out config) && config.OwnerId == unit.OwnerId && config.PartPath == unit.PartPath && config.TieOrder == unit.TieOrder,
                        "Authored persistent tie configuration changed: " + unit.Key);
                    Require(owners.TryGetValue(unit.OwnerId, out owner) && owner == unit.Owner && owner.transform.Find(unit.PartPath) == group.transform,
                        "Bound owner identity/owned foot reference changed: " + unit.Key);
                }
                var current = group.GetComponentsInChildren<SpriteRenderer>(true);
                Require(current.Length == unit.Renderers.Length && current.All(renderer => unit.Renderers.Contains(renderer)), "Bound unit graphic membership changed: " + unit.Key);
                for (var i = 0; i < unit.Renderers.Length; ++i)
                {
                    var renderer = unit.Renderers[i];
                    Require(renderer != null && NearestGroup(renderer.transform) == group && renderer.sortingLayerName == WorldLayer && renderer.sortingOrder == unit.InnerOrders[i],
                        "Graphics must retain their exact nearest foot group and internal layer/order: " + unit.Key);
                    Require(renderer.sprite != null && renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue == SupportedTransparentQueue,
                        "Only the existing transparent queue3000 sprite-material domain is supported: " + unit.Key);
                }
            }
            var worldGraphics = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(renderer => renderer.sortingLayerName == WorldLayer).ToArray();
            var mappedGraphics = units.SelectMany(unit => unit.Renderers).ToArray();
            Require(mappedGraphics.Distinct().Count() == mappedGraphics.Length && worldGraphics.Length == mappedGraphics.Length && worldGraphics.All(renderer => mappedGraphics.Contains(renderer)),
                "Every active or inactive scene WorldObjects SpriteRenderer must belong to exactly one explicit mapped unit.");
            var worldGroups = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SortingGroup>(true))
                .Where(group => group.sortingLayerName == WorldLayer).ToArray();
            Require(worldGroups.Length == units.Length && worldGroups.All(group => groups.Contains(group)), "Unregistered/nested WorldObjects SortingGroup in the exact map.");
            // Read-only global visibility guard: ranks are still assigned only to the explicitly bound map units.
            // Include every loaded scene (also DontDestroyOnLoad); never bind or write an outsider.
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var outside = renderer.gameObject.scene;
                if (!outside.IsValid() || !outside.isLoaded || outside == scene || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy || renderer.sprite == null || renderer.sortingLayerName != WorldLayer) continue;
                Require((Diagnostic.GameplayCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0,
                    "An active camera-layer-visible WorldObjects SpriteRenderer outside the bound map is unsupported: " + outside.path + "/" + renderer.name);
            }
        }

        private static Dictionary<string, StudyAIndependentObjectState> Owners(StudyAIndependentNavigation navigation, Scene scene)
        {
            var result = new Dictionary<string, StudyAIndependentObjectState>(StringComparer.Ordinal);
            foreach (var owner in navigation.Owners)
            {
                Require(owner != null && owner.gameObject.scene == scene && ValidIdentity(owner.ObjectId) && !result.ContainsKey(owner.ObjectId), "Null/duplicate/wrong-scene persistent owner identity.");
                result.Add(owner.ObjectId, owner);
            }
            return result;
        }

        private static int Compare(RankedUnit a, RankedUnit b)
        {
            var y = b.Y.CompareTo(a.Y); if (y != 0) return y;
            var tie = a.Binding.TieOrder.CompareTo(b.Binding.TieOrder); if (tie != 0) return tie;
            return StringComparer.Ordinal.Compare(a.Binding.Key, b.Binding.Key);
        }

        private static SortingGroup NearestGroup(Transform graphic)
        {
            for (var current = graphic; current != null; current = current.parent)
            { var group = current.GetComponent<SortingGroup>(); if (group != null) return group; }
            return null;
        }

        private static string MapKey(string ownerId, string partPath) => "owner:" + ownerId + "/" + partPath;
        private static bool ValidIdentity(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
        private static bool ValidPath(string value) => ValidIdentity(value) && !value.StartsWith("/", StringComparison.Ordinal) &&
            !value.EndsWith("/", StringComparison.Ordinal) && value.Split('/').All(part => part.Length != 0 && part != "." && part != "..");
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        private bool Fail(string message)
        {
            // Invalid domains fall back to captured authored orders instead of leaving a stale Hero rank.
            // Failure remains latched; no unknown renderer/group is adopted or modified.
            try { RestoreOriginalOrders(); }
            catch (Exception restoration) { message += " Original tracked-order restoration failed: " + restoration.Message; }
            Applied = false; Failure = message; return false;
        }

        private void OnDisable() { DetachHooks(); RestoreOriginalOrders(); }
        private void OnDestroy() { DetachHooks(); RestoreOriginalOrders(); }

        private void DetachHooks()
        {
            if (!hooksAttached) return;
            Camera.onPreCull -= BeforeCameraCull;
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            hooksAttached = false;
        }

        private void RestoreOriginalOrders()
        {
            if (bindings == null) return;
            foreach (var unit in bindings)
                if (unit.Group != null && unit.Group.sortingOrder != unit.OriginalOrder) unit.Group.sortingOrder = unit.OriginalOrder;
            SortingGroup.UpdateAllSortingGroups();
            bindings = null; boundActor = null; boundScene = default(Scene);
            observations = Array.AsReadOnly(Array.Empty<UnitObservation>()); Applied = false;
        }
    }
}
