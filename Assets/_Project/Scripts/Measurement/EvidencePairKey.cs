// Assets/_Project/Scripts/Measurement/EvidencePairKey.cs
using System;

/// <summary>
/// Normalised identity of an unordered evidence pair: the two stable evidence ids sorted
/// ordinally, so (BODY-A, EVD-014) and (EVD-014, BODY-A) are the same pair and the same
/// Key ("BODY-A|EVD-014"). One pair is measured once, in either direction.
/// </summary>
public readonly struct EvidencePairKey : IEquatable<EvidencePairKey>
{
    public readonly string evidenceA;
    public readonly string evidenceB;

    private EvidencePairKey(string a, string b)
    {
        evidenceA = a;
        evidenceB = b;
    }

    public static EvidencePairKey Of(string x, string y)
    {
        return string.CompareOrdinal(x, y) <= 0 ? new EvidencePairKey(x, y) : new EvidencePairKey(y, x);
    }

    public string Key => evidenceA + "|" + evidenceB;

    public bool IsSameEvidence => evidenceA == evidenceB;

    public bool Contains(string evidenceId) => evidenceA == evidenceId || evidenceB == evidenceId;

    /// <summary>The other end of the pair from evidenceId (or null if evidenceId is not in it).</summary>
    public string Other(string evidenceId) => evidenceId == evidenceA ? evidenceB : evidenceId == evidenceB ? evidenceA : null;

    public bool Equals(EvidencePairKey other) => evidenceA == other.evidenceA && evidenceB == other.evidenceB;

    public override bool Equals(object obj) => obj is EvidencePairKey other && Equals(other);

    public override int GetHashCode() => Key.GetHashCode();

    public override string ToString() => Key;
}
