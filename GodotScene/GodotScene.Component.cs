/*
    duge.ToolLibs.SceneBuilder

    Copyright (C) 2024 Juan Pablo Arce

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, see <https://www.gnu.org/licenses/>.
*/
namespace duge.ToolLib.SceneBuilder;

public sealed partial class GodotScene
{
    public abstract record Component
    {
        public abstract string HeadingName { get; }
        public abstract IEnumerable<KeyValuePair<string, Value>> HeadingProperties { get; }
        public abstract IEnumerable<KeyValuePair<string, Value>> BodyProperties { get; }
    }

    public abstract record Resource(string ResourceType, string Id) : Component;

    public sealed record ExtResource(string ResourceType, string Path) : Resource(
        ResourceType: ResourceType,
        Id: GenerateId(length: 7))
    {
        public override string HeadingName => "ext_resource";

        public override IEnumerable<KeyValuePair<string, Value>> HeadingProperties
            =>
            [
                new("type", new StringValue(ResourceType)),
                new("path", new StringValue(Path)),
                new("id", new StringValue(Id))
            ];

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get { yield break; }
        }
    }

    public abstract record SubResource(string ResourceType) : Resource(
        ResourceType: ResourceType,
        Id: $"{ResourceType}_{GenerateId(length: 5)}")
    {
        public sealed override string HeadingName => "sub_resource";

        public sealed override IEnumerable<KeyValuePair<string, Value>> HeadingProperties
            =>
            [
                new("type", new StringValue(ResourceType)),
                new("id", new StringValue(Id))
            ];
    }

    public sealed record ArrayMesh(ArrayMesh.Surface[] Surfaces, ArrayMesh? ShadowMesh) : SubResource(ResourceType: "ArrayMesh")
    {
        public sealed record Surface(
            Surface.Vertex[] Vertices,
            Surface.Triangle[] Triangles,
            Material? Material,
            Godot.Mesh.ArrayFormat Format)
        {
            public readonly record struct Vertex(
                Godot.Vector3 Position,
                Godot.Vector3 Normal,
                Godot.Vector3 Tangent,
                float BinormalDirection,
                Godot.Color Color,
                Godot.Vector2 Uv0,
                Godot.Vector2 Uv1);

            public readonly record struct Triangle(
                int Index0,
                int Index1,
                int Index2);

            public DictionaryValue ToDictionaryValue()
            {
                var returnValue = new Dictionary<string, Value>();

                Godot.Vector3 CollectExtents(Func<float, float, float> picker)
                {
                    Godot.Vector3 returnValue = Vertices[0].Position;
                    foreach (var vertex in Vertices)
                    {
                        returnValue.X = picker(returnValue.X, vertex.Position.X);
                        returnValue.Y = picker(returnValue.Y, vertex.Position.Y);
                        returnValue.Z = picker(returnValue.Z, vertex.Position.Z);
                    }
                    return returnValue;
                }

                returnValue["aabb"] = new AabbValue(
                    Min: CollectExtents(Math.Min),
                    Max: CollectExtents(Math.Max));
                returnValue["format"] = new IntValue((long)Format
                     // RenderingServer::ArrayFormat::ARRAY_FLAG_FORMAT_VERSION_2
                     // We include this flag to tell Godot that we're using 4.2's surface format.
                     // See https://github.com/godotengine/godot/pull/81138
                     // If we don't include this, the editor will interpret the vertex data differently
                     // and request a surface version upgrade.
                     | 0x800000000);

                if (Material != null)
                {
                    returnValue["material"] = new ResourceReferenceValue(Material);
                }

                returnValue["primitive"] = new IntValue(3);

                byte[] IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat flag, byte[] bytes)
                    => Format.HasFlag(flag) ? bytes : [];

                Godot.Vector2 OctahedronEncode(Godot.Vector3 normal)
                {
                    normal /= MathF.Abs(normal.X) + MathF.Abs(normal.Y) + MathF.Abs(normal.Z);
                    Godot.Vector2 output;
                    if (normal.Z >= 0.0f) {
                        output.X = normal.X;
                        output.Y = normal.Y;
                    } else {
                        output.X = (1.0f - MathF.Abs(normal.Y)) * (normal.X >= 0.0f ? 1.0f : -1.0f);
                        output.Y = (1.0f - MathF.Abs(normal.X)) * (normal.Y >= 0.0f ? 1.0f : -1.0f);
                    }
                    output.X = output.X * 0.5f + 0.5f;
                    output.Y = output.Y * 0.5f + 0.5f;
                    return output;
                }

                byte[] GetBytesForNormalVector(Godot.Vector3 normal)
                {
                    Godot.Vector2 output = OctahedronEncode(normal);
                    return [
                        .. BitConverter.GetBytes((UInt16)Math.Clamp(output.X * 65535f, 0f, 65535f)),
                        .. BitConverter.GetBytes((UInt16)Math.Clamp(output.Y * 65535f, 0f, 65535f))
                    ];
                }

                Godot.Vector2 OctahedronTangentEncode(Godot.Vector3 tangent, float binormalDirection) {
                    const float bias = 1.0f / 32767.0f;
                    Godot.Vector2 res = OctahedronEncode(tangent);
                    res.Y = MathF.Max(res.Y, bias);
                    res.Y = res.Y * 0.5f + 0.5f;
                    res.Y = binormalDirection >= 0.0f ? res.Y : 1f - res.Y;
                    return res;
                }

                byte[] GetBytesForTangentVector(Godot.Vector3 tangent, float binormalDirection)
                {
                    Godot.Vector2 output = OctahedronTangentEncode(tangent, binormalDirection);
                    return [
                        .. BitConverter.GetBytes((UInt16)Math.Clamp(output.X * 65535f, 0f, 65535f)),
                        .. BitConverter.GetBytes((UInt16)Math.Clamp(output.Y * 65535f, 0f, 65535f))
                    ];
                }

                byte[] vertexData
                    = [.. Vertices.SelectMany<Vertex, byte>(v =>
                        [
                            .. BitConverter.GetBytes(v.Position.X),
                            .. BitConverter.GetBytes(v.Position.Y),
                            .. BitConverter.GetBytes(v.Position.Z)
                        ]),
                        .. Vertices.SelectMany<Vertex, byte>(v => 
                            IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat.FormatNormal, [
                            .. GetBytesForNormalVector(v.Normal),
                            .. IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat.FormatTangent, GetBytesForTangentVector(v.Tangent, v.BinormalDirection))
                        ]))];
                byte[] attributeData
                    = Vertices.SelectMany<Vertex, byte>(v =>
                        [
                            .. IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat.FormatColor, BitConverter.GetBytes(v.Color.ToAbgr32())),
                            .. IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat.FormatTexUV, [
                                .. BitConverter.GetBytes(v.Uv0.X),
                                .. BitConverter.GetBytes(v.Uv0.Y)
                            ]),
                            .. IncludeIfFormatFlagIsSet(Godot.Mesh.ArrayFormat.FormatTexUV2, [
                                .. BitConverter.GetBytes(v.Uv1.X),
                                .. BitConverter.GetBytes(v.Uv1.Y)
                            ]),
                        ])
                        .ToArray();

                byte[] GetBytesForIndex(int index)
                    => Triangles.Length * 3 <= 65535
                        ? BitConverter.GetBytes((UInt16)index)
                        : BitConverter.GetBytes(index);

                byte[] indexData
                    = Triangles.SelectMany<Triangle, byte>(t =>
                        [
                            .. GetBytesForIndex(t.Index0),
                            .. GetBytesForIndex(t.Index1),
                            .. GetBytesForIndex(t.Index2),
                        ])
                        .ToArray();

                returnValue["vertex_count"] = new IntValue(Vertices.Length);
                returnValue["vertex_data"] = new PackedByteArrayValue(vertexData);
                if (attributeData.Length > 0)
                {
                    returnValue["attribute_data"] = new PackedByteArrayValue(attributeData);
                }

                returnValue["index_count"] = new IntValue(Triangles.Length * 3);
                returnValue["index_data"] = new PackedByteArrayValue(indexData);

                return new DictionaryValue(returnValue);
            }
        }

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                yield return new("_surfaces",
                    new VariantArrayValue(Surfaces.Select(surface => (Value)surface.ToDictionaryValue()).ToArray()));
                if (ShadowMesh != null)
                {
                    yield return new("shadow_mesh", new ResourceReferenceValue(ShadowMesh));
                }
            }
        }
    }

    public abstract record Material(string ResourceType) : SubResource(ResourceType);

    public sealed record ShaderMaterial(Resource Shader, Dictionary<string, Value> ShaderParameters) : Material(ResourceType: "ShaderMaterial")
    {
        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                yield return new("render_priority", new IntValue(0));
                yield return new("shader", new ResourceReferenceValue(Shader));
                foreach (var kvp in ShaderParameters)
                {
                    yield return new($"shader_parameter/{kvp.Key}", kvp.Value);
                }
            }
        }
    }

    public abstract record Node(string Name, Node? ParentNode) : Component
    {
        
        public sealed override string HeadingName => "node";
        public abstract string NodeType { get; }

        public string ParentPath
        {
            get
            {
                if (ParentNode == null) { return ""; }
                if (ParentNode.ParentNode == null) { return "."; }

                string retVal = ParentNode.Name;
                for (Node? currentAncestor = ParentNode.ParentNode;
                     currentAncestor != null;
                     currentAncestor = currentAncestor.ParentNode)
                {
                    retVal = currentAncestor.Name + "/" + retVal;
                }

                return retVal;
            }
        }

        public sealed override IEnumerable<KeyValuePair<string, Value>> HeadingProperties
        {
            get
            {
                yield return new("name", new StringValue(Name));
                yield return new("type", new StringValue(NodeType));
                if (ParentNode != null)
                {
                    yield return new("parent", new StringValue(ParentPath));
                }
            }
        }
    }

    public record Node3d(string Name, Transform3dValue Transform, Node? ParentNode) : Node(Name, ParentNode)
    {
        public override string NodeType => "Node3D";

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                if (Transform != Transform3dValue.Identity)
                {
                    yield return new("transform", Transform);
                }
            }
        }
    }

    public record MeshInstance3d(string Name, Transform3dValue Transform, Node? ParentNode, ArrayMesh MeshResource) : Node3d(Name, Transform, ParentNode)
    {
        public override string NodeType => "MeshInstance3D";

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                foreach (var kvp in base.BodyProperties)
                {
                    yield return kvp;
                }
                yield return new("mesh", new ResourceReferenceValue(MeshResource));
                yield return new("skeleton", new NodePathValue(""));
            }
        }
    }

    public record OmniLight3d(
        string Name,
        Transform3dValue Transform,
        Node? ParentNode,
        Godot.Color LightColor,
        float LightEnergy,
        float LightSpecular,
        bool ShadowEnabled,
        float ShadowBias,
        float ShadowNormalBias,
        float OmniRange) : Node3d(Name, Transform, ParentNode)
    {
        public override string NodeType => "OmniLight3D";

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                foreach (var kvp in base.BodyProperties)
                {
                    yield return kvp;
                }
                yield return new("light_color", new ColorValue(LightColor));
                yield return new("light_energy", new FloatValue(LightEnergy));
                yield return new("light_specular", new FloatValue(LightSpecular));
                yield return new("shadow_enabled", new BoolValue(ShadowEnabled));
                yield return new("shadow_bias", new FloatValue(ShadowBias));
                yield return new("shadow_normal_bias", new FloatValue(ShadowNormalBias));
                yield return new("shadow_bias", new FloatValue(ShadowBias));
                yield return new("omni_range", new FloatValue(OmniRange));
            }
        }
    }

    public record SpotLight3d(
        string Name,
        Transform3dValue Transform,
        Node? ParentNode,
        Godot.Color LightColor,
        float LightEnergy,
        float LightSpecular,
        bool ShadowEnabled,
        float ShadowBias,
        float ShadowNormalBias,
        float SpotRange,
        float SpotAngle,
        float SpotAngleAttenuation) : Node3d(Name, Transform, ParentNode)
    {
        public override string NodeType => "SpotLight3D";

        public override IEnumerable<KeyValuePair<string, Value>> BodyProperties
        {
            get
            {
                foreach (var kvp in base.BodyProperties)
                {
                    yield return kvp;
                }
                yield return new("light_color", new ColorValue(LightColor));
                yield return new("light_energy", new FloatValue(LightEnergy));
                yield return new("light_specular", new FloatValue(LightSpecular));
                yield return new("shadow_enabled", new BoolValue(ShadowEnabled));
                yield return new("shadow_bias", new FloatValue(ShadowBias));
                yield return new("shadow_normal_bias", new FloatValue(ShadowNormalBias));
                yield return new("shadow_bias", new FloatValue(ShadowBias));
                yield return new("spot_range", new FloatValue(SpotRange));
                yield return new("spot_angle", new FloatValue(SpotAngle));
                yield return new("spot_angle_attenuation", new FloatValue(SpotAngleAttenuation));
            }
        }
    }

    public static string GenerateId(int length)
    {
        Random rng = new Random();
        Span<char> chrs = stackalloc char[length];
        for (int i = 0; i < chrs.Length; i++)
        {
            chrs[i] = (char)((rng.Next() % (10 + ('z' - 'a') + 1)) switch
            {
                var x when x is >= 0 and <= 9
                    => x + '0',
                var x
                    => (x - 10) + 'a'
            });
        }
        return new string(chrs);
    }
}