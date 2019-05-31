using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Title("Input", "Geometry", "Absolute World Position")]
    class AbsoluteWorldPositionNode : AbstractMaterialNode, IGeneratesBodyCode, IMayRequirePosition
    {
        public const int OutputSlotId = 0;
        public const int PositionInputId = 1;
        const string kOutputSlotName = "Out";
        const string kPositionInputName = "Position";
        public override bool hasPreview { get { return true; } }
        public override PreviewMode previewMode
        {
            get { return PreviewMode.Preview3D; }
        }
        public AbsoluteWorldPositionNode()
        {
            name = "Absolute World Position";
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new Vector3MaterialSlot(OutputSlotId, kOutputSlotName, kOutputSlotName, SlotType.Output, Vector3.zero));
            AddSlot(new PositionMaterialSlot(PositionInputId, kPositionInputName, kPositionInputName, CoordinateSpace.World));
            RemoveSlotsNameNotMatching(new[] { OutputSlotId, PositionInputId });
        }

        public virtual void GenerateNodeCode(ShaderStringBuilder sb, GraphContext graphContext, GenerationMode generationMode)
        {
            sb.AppendLine("$precision3 {0} = GetAbsolutePositionWS({1});", GetVariableNameForSlot(OutputSlotId),
                GetSlotValue(PositionInputId, generationMode));
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability)
        {
            return CoordinateSpace.World.ToNeededCoordinateSpace();
        }
    }
}
