namespace NLightning.Domain.Bitcoin.Enums;

[Flags]
public enum AddressType : byte
{
    P2Tr = 1,
    P2Wpkh = 2
}