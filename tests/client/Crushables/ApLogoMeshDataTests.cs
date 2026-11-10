using System.Text;

namespace UntilYouFallArchipelago;

internal static class ApLogoMeshDataTests
{
    public static void Run()
    {
        using var resource =
            typeof(ApLogoMeshDataTests).Assembly.GetManifestResourceStream(ApLogoMeshData.ResourceName)
            ?? throw new Exception("embedded AP logo mesh is missing");
        var mesh = ApLogoMeshData.Read(resource, "embedded AP logo");
        Assert(mesh.VertexCount == 7824, "exported vertex count is stable");
        Assert(
            mesh.SubmeshTriangles.Length == 6 && mesh.Colors.Length == 6,
            "the AP logo retains six colored submeshes"
        );
        Assert(
            mesh.Positions.Length == mesh.VertexCount * 3
                && mesh.Normals.Length == mesh.VertexCount * 3
                && mesh.Uvs.Length == mesh.VertexCount * 2,
            "vertex attributes have matching lengths"
        );
        Assert(
            mesh.SubmeshTriangles.All(indices => indices.Length > 0 && indices.Length % 3 == 0),
            "every submesh contains complete triangles"
        );
        Assert(
            mesh.SubmeshTriangles.SelectMany(indices => indices).All(index => index >= 0 && index < mesh.VertexCount),
            "every triangle index points at an exported vertex"
        );
        var size = Bounds(mesh.Positions);
        Assert(size <= 0.321f && size >= 0.319f, "the runtime logo maximum dimension is 0.32 metres");

        AssertInvalid(BuildHeader("BAD!", 1, 1, 6), "bad magic is rejected");
        AssertInvalid(BuildHeader("APM1", 1, 100001, 6), "unbounded vertex allocation is rejected");
        AssertInvalid(BuildInvalidIndexMesh(), "triangle indices outside the vertex range are rejected");
        Console.WriteLine("PASS: AP logo embedded mesh format, bounds and defensive validation");
    }

    private static float Bounds(float[] positions)
    {
        var minimum = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
        var maximum = new[] { float.MinValue, float.MinValue, float.MinValue };
        for (var index = 0; index < positions.Length; index += 3)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], positions[index + axis]);
                maximum[axis] = Math.Max(maximum[axis], positions[index + axis]);
            }
        }
        return Enumerable.Range(0, 3).Max(axis => maximum[axis] - minimum[axis]);
    }

    private static MemoryStream BuildHeader(string magic, int version, int vertices, int submeshes)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            writer.Write(Encoding.ASCII.GetBytes(magic));
            writer.Write(version);
            writer.Write(vertices);
            writer.Write(submeshes);
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildInvalidIndexMesh()
    {
        var stream = BuildHeader("APM1", 1, 1, 6);
        stream.Position = stream.Length;
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            for (var value = 0; value < 8; value++)
                writer.Write(0f);
            for (var submesh = 0; submesh < 6; submesh++)
            {
                for (var channel = 0; channel < 4; channel++)
                    writer.Write(1f);
                writer.Write(3);
                writer.Write(submesh == 0 ? 1 : 0);
                writer.Write(0);
                writer.Write(0);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static void AssertInvalid(Stream stream, string message)
    {
        using (stream)
        {
            try
            {
                ApLogoMeshData.Read(stream, "invalid test mesh");
            }
            catch (InvalidDataException)
            {
                return;
            }
        }
        throw new Exception("AP logo mesh: " + message);
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("AP logo mesh: " + message);
    }
}
