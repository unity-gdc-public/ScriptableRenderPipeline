using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;

namespace UnityEditor.Rendering.HighDefinition
{
    class RayTracingNode
    {
        static ShaderKeyword Keyword = new ShaderKeyword(ShaderKeywordType.Boolean, false)
        {
            displayName = "Raytracing",
            overrideReferenceName = "RAYTRACING_SHADER_GRAPH",
            isEditable = false,
            keywordDefinition = ShaderKeywordDefinition.Predefined,
        };

        [CustomKeywordNodeProvider]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        static IEnumerable<ShaderKeyword> GetRayTracingKeyword() => Enumerable.Repeat(Keyword, 1);
    }
}
