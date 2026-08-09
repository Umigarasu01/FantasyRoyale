# HealingFountainEventHandler

対象: `Assets/Scripts/Gameplay/Events/Runtime/HealingFountainEventHandler.cs`

状態: **実装・Unity検証済み**

## 役割

`HealingFountainEventDefinition`の回復量を本番`ICharacterHealthTarget`へ適用する、回復の泉専用Handler。

- 実回復量が1以上なら`Succeeded`を返す。
- HP満タンなら`Rejected`を返し、OneShotを消費しない。
- 撃破状態なら通常回復を`Rejected`として返し、OneShotを消費しない。復活処理は担当しない。
- HP対象がなければ`Invalid`を返す。
- 成功時は回復Message Key、Effect Cue ID、Audio Cue IDを返す。

Handlerは日本語文言を生成せず、EffectやSEも再生しない。Previewでは`PreviewDebugMapEventPresenter`が意味IDをHUD文言へ変換する。正式Effect / Audio Presenterは未実装。
