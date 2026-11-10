using System.Text;

namespace UntilYouFallArchipelago;

internal readonly record struct ApLogoColor(float R, float G, float B, float A);

internal sealed class ApLogoMeshData
{
    public const string ResourceName = "UntilYouFall.Crushables.ApLogo.meshbin";
    private const int Version = 1;
    private const int SubmeshCount = 6;
    private const int MaximumVertices = 100000;
    private const int MaximumIndices = 600000;

    private ApLogoMeshData(
        float[] positions,
        float[] normals,
        float[] uvs,
        int[][] submeshTriangles,
        ApLogoColor[] colors
    )
    {
        Positions = positions;
        Normals = normals;
        Uvs = uvs;
        SubmeshTriangles = submeshTriangles;
        Colors = colors;
    }

    public int VertexCount => Positions.Length / 3;
    public float[] Positions { get; }
    public float[] Normals { get; }
    public float[] Uvs { get; }
    public int[][] SubmeshTriangles { get; }
    public ApLogoColor[] Colors { get; }

    public static ApLogoMeshData Read(Stream stream, string assetName)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (!reader.ReadBytes(4).SequenceEqual(Encoding.ASCII.GetBytes("APM1")))
                throw new InvalidDataException($"{assetName}: invalid magic");
            if (reader.ReadInt32() != Version)
                throw new InvalidDataException($"{assetName}: unsupported version");
            var vertexCount = reader.ReadInt32();
            if (vertexCount is < 1 or > MaximumVertices)
                throw new InvalidDataException($"{assetName}: invalid vertex count {vertexCount}");
            var submeshCount = reader.ReadInt32();
            if (submeshCount != SubmeshCount)
                throw new InvalidDataException($"{assetName}: expected {SubmeshCount} submeshes, found {submeshCount}");

            var positions = new float[checked(vertexCount * 3)];
            var normals = new float[checked(vertexCount * 3)];
            var uvs = new float[checked(vertexCount * 2)];
            for (var vertex = 0; vertex < vertexCount; vertex++)
            {
                for (var axis = 0; axis < 3; axis++)
                    positions[vertex * 3 + axis] = reader.ReadSingle();
                for (var axis = 0; axis < 3; axis++)
                    normals[vertex * 3 + axis] = reader.ReadSingle();
                for (var axis = 0; axis < 2; axis++)
                    uvs[vertex * 2 + axis] = reader.ReadSingle();
            }
            var submeshes = new int[SubmeshCount][];
            var colors = new ApLogoColor[SubmeshCount];
            for (var submesh = 0; submesh < SubmeshCount; submesh++)
            {
                colors[submesh] = new ApLogoColor(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                var indexCount = reader.ReadInt32();
                if (indexCount is < 3 or > MaximumIndices || indexCount % 3 != 0)
                    throw new InvalidDataException($"{assetName}: invalid index count {indexCount}");
                var indices = new int[indexCount];
                for (var index = 0; index < indexCount; index++)
                {
                    indices[index] = reader.ReadInt32();
                    if (indices[index] < 0 || indices[index] >= vertexCount)
                        throw new InvalidDataException(
                            $"{assetName}: index {indices[index]} outside {vertexCount} vertices"
                        );
                }
                submeshes[submesh] = indices;
            }

            return new ApLogoMeshData(positions, normals, uvs, submeshes, colors);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"{assetName}: truncated mesh data", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{assetName}: mesh data is too large", exception);
        }
    }
}
