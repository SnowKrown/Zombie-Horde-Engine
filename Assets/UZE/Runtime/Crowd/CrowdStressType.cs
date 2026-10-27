namespace UZE.Crowd
{
    [System.Flags]
    public enum CrowdStressType : byte
    {
        None = 0,
        Surging = 1 << 0,
        Congested = 1 << 1,
        FrontalStumble = 1 << 2,
        CrowdCrush = 1 << 3,
        LateralInstability = 1 << 4
    }
}
