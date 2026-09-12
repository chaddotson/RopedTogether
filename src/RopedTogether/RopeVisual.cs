using UnityEngine;

namespace RopedTogether;

internal sealed class RopeVisual
{
    private const int SegmentCount = 14;

    private readonly GameObject _root;
    private readonly LineRenderer _line;
    private readonly GameObject _clipA;
    private readonly GameObject _clipB;

    public RopeVisual(string name)
    {
        _root = new GameObject(name);
        _line = _root.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = SegmentCount;
        _line.numCornerVertices = 4;
        _line.numCapVertices = 2;
        _line.startWidth = Plugin.RopeWidth.Value;
        _line.endWidth = Plugin.RopeWidth.Value;
        _line.material = Plugin.RopeMaterial;
        _line.startColor = Plugin.RopeColor;
        _line.endColor = Plugin.RopeColor;

        _clipA = CreateClip($"{name}.ClipA");
        _clipB = CreateClip($"{name}.ClipB");
    }

    public void Update(Vector3 start, Vector3 end, float nominalLength)
    {
        float distance = Vector3.Distance(start, end);
        float slack = Mathf.Max(0f, nominalLength - distance);
        float sag = Mathf.Min(0.8f, slack * 0.22f);

        for (int i = 0; i < SegmentCount; i++)
        {
            float t = i / (float)(SegmentCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point += Vector3.down * (Mathf.Sin(Mathf.PI * t) * sag);
            _line.SetPosition(i, point);
        }

        float width = Plugin.RopeWidth.Value;
        _line.startWidth = width;
        _line.endWidth = width;

        _clipA.transform.position = start;
        _clipB.transform.position = end;
    }

    public void Destroy()
    {
        if (_clipA != null)
            Object.Destroy(_clipA);
        if (_clipB != null)
            Object.Destroy(_clipB);
        if (_root != null)
            Object.Destroy(_root);
    }

    private static GameObject CreateClip(string name)
    {
        GameObject clip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        clip.name = name;
        clip.transform.localScale = Vector3.one * 0.11f;

        Collider? collider = clip.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        Renderer? renderer = clip.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = Plugin.ConnectorMaterial;

        return clip;
    }
}
