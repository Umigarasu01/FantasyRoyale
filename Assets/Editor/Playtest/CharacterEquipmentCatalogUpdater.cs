using System;
using System.Collections.Generic;
using System.Linq;
using FantasyRoyale.Gameplay.Characters.Unity;
using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.Editor.Playtest
{
    /// <summary>
    /// 定義Assetが増減するたびに、全装備の一覧を集め直す。idを持つ定義は、それだけで一覧に載る。
    /// </summary>
    public sealed class CharacterEquipmentCatalogUpdater : AssetPostprocessor
    {
        public const string CatalogPath = "Assets/Data/Gameplay/Equipment/EquipmentCatalog.asset";

        private const string DataFolder = "Assets/Data/Gameplay/";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Concat(deleted).Concat(moved).Any(IsDefinition))
            {
                // 取り込みの途中ではAssetを保存できないので、終わってから集める。
                EditorApplication.delayCall -= RefreshLater;
                EditorApplication.delayCall += RefreshLater;
            }
        }

        /// <summary>
        /// idを持つ定義を集めて一覧を更新する。敵のようにidのない絵の定義は載せない。
        /// </summary>
        public static CharacterEquipmentCatalog Refresh()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterEquipmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterEquipmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var armour = LoadAll<CharacterSpriteSetDefinition>()
                .Where(part => !string.IsNullOrEmpty(part.Id))
                .OrderBy(part => part.Id, StringComparer.Ordinal)
                .ToArray();
            var weapons = LoadAll<CharacterEquipmentDefinition>()
                .Where(HasId)
                .OrderBy(weapon => weapon.Id, StringComparer.Ordinal)
                .ToArray();
            if (!armour.SequenceEqual(catalog.Armour) || !weapons.SequenceEqual(catalog.Weapons))
            {
                catalog.Configure(armour, weapons);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            foreach (var problem in catalog.FindIdProblems())
            {
                Debug.LogError(problem, catalog);
            }

            return catalog;
        }

        private static void RefreshLater()
        {
            Refresh();
        }

        private static bool IsDefinition(string path)
        {
            return path.StartsWith(DataFolder, StringComparison.Ordinal)
                && path.EndsWith(".asset", StringComparison.Ordinal)
                && path != CatalogPath;
        }

        private static bool HasId(CharacterEquipmentDefinition weapon)
        {
            if (!string.IsNullOrEmpty(weapon.Id))
            {
                return true;
            }

            Debug.LogWarning($"{weapon.name}: idがないので全装備の一覧に載りません。", weapon);
            return false;
        }

        private static IEnumerable<T> LoadAll<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(definition => definition != null);
        }
    }
}
