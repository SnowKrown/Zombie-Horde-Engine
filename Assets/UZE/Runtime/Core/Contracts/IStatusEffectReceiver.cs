namespace UZE.Core.Contracts
{
    public interface IStatusEffectReceiver
    {
        void ApplyStatusEffect(StatusEffectType type, float duration, float magnitude);
    }
}
