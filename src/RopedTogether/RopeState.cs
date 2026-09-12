using System.Collections.Generic;
using System.Linq;

namespace RopedTogether;

internal static class RopeState
{
    private static readonly HashSet<RopeLink> Links = new();

    public static IReadOnlyCollection<RopeLink> Snapshot()
    {
        return Links.ToArray();
    }

    public static bool IsLinked(int actorA, int actorB)
    {
        if (actorA <= 0 || actorB <= 0 || actorA == actorB)
            return false;
        return Links.Contains(new RopeLink(actorA, actorB));
    }

    public static IEnumerable<RopeLink> ForActor(int actorNumber)
    {
        return Links.Where(link => link.Contains(actorNumber)).ToArray();
    }

    public static int CountForActor(int actorNumber)
    {
        return Links.Count(link => link.Contains(actorNumber));
    }

    public static void Set(int actorA, int actorB, bool enabled)
    {
        if (actorA <= 0 || actorB <= 0 || actorA == actorB)
            return;

        RopeLink link = new(actorA, actorB);
        if (enabled)
            Links.Add(link);
        else
            Links.Remove(link);
    }

    public static void Clear()
    {
        Links.Clear();
    }
}
