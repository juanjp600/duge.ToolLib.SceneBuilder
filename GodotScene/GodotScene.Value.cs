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
using System.Globalization;

namespace duge.ToolLib.SceneBuilder;

public sealed partial class GodotScene
{
    public abstract record Value
    {
        private static readonly string FloatFormat = "0." + new string('#', 339);
        public abstract string StringRepresentation { get; }
    }
     public sealed record StringValue(string Str) : Value
    {
        public override string StringRepresentation => $"\"{Str}\"";
    }

    public sealed record AabbValue(Godot.Vector3 Min, Godot.Vector3 Max) : Value
    {
        private static readonly string Format = "0." + new string('#', 339);
        public override string StringRepresentation
            => "AABB(" +
               $"{Min.X.ToString(Format)}, {Min.Y.ToString(Format)}, {Min.Z.ToString(Format)}, " +
               $"{(Max.X - Min.X).ToString(Format)}, {(Max.Y - Min.Y).ToString(Format)}, {(Max.Z - Min.Z).ToString(Format)})";
    }

    public sealed record ColorValue(Godot.Color Color) : Value
    {
        public override string StringRepresentation
            => $"Color({Color.R}, {Color.G}, {Color.B}, {Color.A})";
    }

    public sealed record IntValue(long Number) : Value
    {
        public override string StringRepresentation
            => Number.ToString();
    }

    public sealed record FloatValue(float Number) : Value
    {
        public override string StringRepresentation
            => Number.ToString(CultureInfo.InvariantCulture);
    }

    public sealed record BoolValue(bool Boolean) : Value
    {
        public override string StringRepresentation
            => Boolean ? "true" : "false";
    }

    public sealed record PackedByteArrayValue(byte[] Bytes) : Value
    {
        public override string StringRepresentation
            => $"PackedByteArray(\"{Convert.ToBase64String(Bytes)}\")";
    }

    public sealed record VariantArrayValue(Value[] Elements) : Value
    {
        public override string StringRepresentation
            => $"[{string.Join(", ", Elements.Select(e => e.StringRepresentation))}]";
    }

    public sealed record DictionaryValue(Dictionary<string, Value> Dictionary) : Value
    {
        public override string StringRepresentation
            => Dictionary.Count == 0
                ? "{}"
                : $"{{\n{
                    string.Join(",\n", Dictionary.Select(kvp
                        => $"\"{kvp.Key}\": {kvp.Value.StringRepresentation}"))
                }\n}}";
    }

    public sealed record ResourceReferenceValue(Resource Resource) : Value
    {
        public override string StringRepresentation
            => Resource switch
               {
                   ExtResource => "ExtResource",
                   SubResource => "SubResource",
                   _ => throw new InvalidOperationException()
               }
               + $"(\"{Resource.Id}\")";
    }

    public sealed record NodePathValue(string Path) : Value
    {
        public override string StringRepresentation
            => $"NodePath(\"{Path}\")";
    }

    public sealed record Transform3dValue(
        Godot.Transform3D Transform) : Value
    {
        public static Transform3dValue Identity = new Transform3dValue(Godot.Transform3D.Identity);

        public override string StringRepresentation
            => "Transform3D(" +
               $"{Transform[0][0].FloatToString()}, {Transform[0][1].FloatToString()}, {Transform[0][2].FloatToString()}, " +
               $"{Transform[1][0].FloatToString()}, {Transform[1][1].FloatToString()}, {Transform[1][2].FloatToString()}, " +
               $"{Transform[2][0].FloatToString()}, {Transform[2][1].FloatToString()}, {Transform[2][2].FloatToString()}, " +
               $"{Transform[3][0].FloatToString()}, {Transform[3][1].FloatToString()}, {Transform[3][2].FloatToString()})";
    }
}