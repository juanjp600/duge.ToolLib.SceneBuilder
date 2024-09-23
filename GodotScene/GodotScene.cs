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
using System.Text;

namespace duge.ToolLib.SceneBuilder;

public sealed partial class GodotScene
{
    public readonly string Uid = GenerateId(length: 12);

    public readonly List<Component> Components = new List<Component>();

    public void WriteToStream(Stream stream)
    {
        void WriteLine(string line)
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            stream.Write(bytes);
        }

        WriteLine($"[gd_scene load_steps={(Components.Count(c => c is ExtResource or SubResource) + 1)} format=4 uid=\"uid://{Uid}\"]");

        foreach (var component in Components)
        {
            WriteLine("");
            WriteLine($"[{component.HeadingName} {string.Join(" ", component.HeadingProperties.Select(kvp => $"{kvp.Key}={kvp.Value.StringRepresentation}"))}]");
            foreach (var kvp in component.BodyProperties)
            {
                WriteLine($"{kvp.Key}={kvp.Value.StringRepresentation}");
            }
        }
    }
}