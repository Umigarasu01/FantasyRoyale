# MapSocketMarkerEditor

## 役割

`MapSocketMarkerEditor`は、EventSocketの位置と論理接近範囲をScene Viewで別々に編集・確認するEditor専用表示。

- Script: `Assets/Scripts/MapAuthoringKit/Editor/MapSocketMarkerEditor.cs`
- 対象: `MapSocketMarker`

Socket中心はGameObjectの`Transform Position`を正本とし、通常の移動Toolで自由配置する。選択中のEventSocketにはSocket ID、Event名、実効半径のLabelとRadius Handleを表示する。Handle操作は`InteractionRadiusOverride`だけを更新する。

Inspectorの「接近半径をEvent定義の標準値へ戻す」は上書きを0へ戻し、共有定義の`DefaultInteractionRadius`を再び使用する。表示円はPhysics ColliderやTriggerではない。
