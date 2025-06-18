namespace Khod;

enum PresetTypes
{
    Charge,
    Ground
}

internal class Preset(PresetTypes type)
{

    private readonly PresetTypes _type = type;

    public (int, int) Anchor { get; set; }
    public int Rotation { get; set; }

    public string GetSVG()
    {
        return _type switch
        {
            PresetTypes.Charge => GetCharge(),
            PresetTypes.Ground => GetGround(),
            _ => throw new NotImplementedException(),
        };
    }

    private string GetCharge()
    {
        return $"Charge at {Anchor} {Rotation}";
    }

    private string GetGround()
    {
        return $"Ground at {Anchor} {Rotation}";
    }

}
