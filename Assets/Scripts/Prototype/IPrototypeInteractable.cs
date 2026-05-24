namespace FantasyRoyale.Prototype
{
    /// <summary>
    /// プレイヤーのEキー操作から起動できる仮オブジェクトの共通口。
    /// </summary>
    public interface IPrototypeInteractable
    {
        void Interact(PrototypePlayerController2D player);
    }
}
