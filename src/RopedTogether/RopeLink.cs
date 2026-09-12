using System;

namespace RopedTogether;

internal readonly struct RopeLink : IEquatable<RopeLink>
{
    public RopeLink(int actorA, int actorB)
    {
        if (actorA <= actorB)
        {
            A = actorA;
            B = actorB;
        }
        else
        {
            A = actorB;
            B = actorA;
        }
    }

    public int A { get; }
    public int B { get; }

    public bool Contains(int actorNumber) => A == actorNumber || B == actorNumber;

    public int Other(int actorNumber)
    {
        if (A == actorNumber)
            return B;
        if (B == actorNumber)
            return A;
        return -1;
    }

    public bool Equals(RopeLink other) => A == other.A && B == other.B;
    public override bool Equals(object? obj) => obj is RopeLink other && Equals(other);
    public override int GetHashCode() => (A * 397) ^ B;
    public override string ToString() => $"{A}-{B}";
}
