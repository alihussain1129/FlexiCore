namespace FlexiCore.Licensing;

/// <summary>A trusted ECDSA P-256 public key: id plus DER SubjectPublicKeyInfo bytes.</summary>
public sealed record TrustedKey(string KeyId, byte[] SubjectPublicKeyInfo);