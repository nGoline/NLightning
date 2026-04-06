namespace NLightning.Transport.Ipc;

public enum IpcEnvelopeKind : byte
{
    Request = 0,
    Response = 1,
    Error = 2
}