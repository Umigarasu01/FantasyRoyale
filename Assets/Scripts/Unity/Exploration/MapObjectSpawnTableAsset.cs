using System;
using System.Collections.Generic;
using FantasyRoyale.Core.Map;
using UnityEngine;

namespace FantasyRoyale.Unity.Exploration
{
    /// <summary>
    /// ランダム配置候補をまとめるテーブル。
    /// BuilderがCore用候補リストとPrefab検索の両方に使う。
    /// </summary>
    [CreateAssetMenu(menuName = "FantasyRoyale/Map Object Spawn Table")]
    public sealed class MapObjectSpawnTableAsset : ScriptableObject
    {
        [SerializeField] private MapObjectDefinitionAsset[] objects = Array.Empty<MapObjectDefinitionAsset>();

        public MapObjectDefinitionAsset[] Objects => objects;

        public IReadOnlyList<FR_MapObjectDefinition> ToCoreDefinitions()
        {
            var list = new List<FR_MapObjectDefinition>();
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    list.Add(objects[i].ToCoreDefinition());
                }
            }

            return list;
        }

        public GameObject FindPrefab(string objectId)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].ObjectId == objectId)
                {
                    return objects[i].Prefab;
                }
            }

            return null;
        }

        public void Configure(MapObjectDefinitionAsset[] newObjects)
        {
            objects = newObjects ?? Array.Empty<MapObjectDefinitionAsset>();
        }
    }
}
