using UnityEditor;
using UnityEngine;

namespace FantasyRoyale.MapAuthoringKit.Editor
{
    /// <summary>
    /// EventSocketの位置と論理接近範囲を、Scene Viewで別々に調整・確認するEditor表示。
    /// Transformを位置の正本とし、半径Handleだけを配置固有設定へ保存する。
    /// </summary>
    [CustomEditor(typeof(MapSocketMarker))]
    public sealed class MapSocketMarkerEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 通常のInspector項目に、位置と半径の編集規則および標準半径へ戻す操作を追加する。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var marker = (MapSocketMarker)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Socket位置はTransform Positionです。Scene Viewで自由に移動できます。"
                + "Eventの円は物理Colliderではなく論理接近範囲です。",
                MessageType.Info);

            if (marker.SocketKind != MapSocketKind.Event
                || marker.InteractionRadiusOverride <= 0f)
            {
                return;
            }

            if (GUILayout.Button("接近半径をEvent定義の標準値へ戻す"))
            {
                Undo.RecordObject(marker, "Reset Event Socket Radius");
                marker.SetInteractionRadiusOverride(0f);
                EditorUtility.SetDirty(marker);
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// 選択中EventSocketへ識別Labelと半径Handleを描き、中心位置と範囲をScene上で直接編集する。
        /// </summary>
        private void OnSceneGUI()
        {
            var marker = (MapSocketMarker)target;
            if (marker.SocketKind != MapSocketKind.Event)
            {
                return;
            }

            var radius = marker.InteractionRadius;
            if (radius <= 0f)
            {
                radius = MapEventDefinition.DefaultInteractionRadiusValue;
            }

            var definitionName = marker.EventDefinition != null
                ? marker.EventDefinition.DisplayName
                : marker.EventDefinitionId;
            Handles.Label(
                marker.transform.position + Vector3.up * 0.3f,
                $"{marker.SocketId}\n{definitionName}\nRadius {radius:0.00}");

            var previousColor = Handles.color;
            Handles.color = new Color(1f, 0.45f, 0.8f, 0.95f);
            EditorGUI.BeginChangeCheck();
            var updatedRadius = Handles.RadiusHandle(
                Quaternion.identity,
                marker.transform.position,
                radius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(marker, "Adjust Event Socket Radius");
                marker.SetInteractionRadiusOverride(Mathf.Max(0.01f, updatedRadius));
                EditorUtility.SetDirty(marker);
            }

            Handles.color = previousColor;
        }
    }
}
