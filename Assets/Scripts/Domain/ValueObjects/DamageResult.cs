namespace SwDreams.Domain.ValueObjects
{
    /// <summary>
    /// 데미지 계산 결과 값 객체.
    /// Phase 4에서 IsCritical 활용 (치명타 데미지 증가 패시브).
    /// </summary>
    public readonly struct DamageResult
    {
        public readonly int FinalDamage;
        public readonly bool IsCritical;

        public DamageResult(int finalDamage, bool isCritical = false)
        {
            FinalDamage = finalDamage;
            IsCritical = isCritical;
        }
    }
}
