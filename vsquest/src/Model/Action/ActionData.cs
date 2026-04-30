using Newtonsoft.Json;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSQuest.Model.Action
{
	public class QuestData : IActionData
	{
		public string? Id { get; set; }
		public long? GiverId { get; set; }
	}

	public class ItemData : IActionData
	{
		public required string Code { get; set; }
		public int Amount { get; set; } = 0;
	}

	public class SoundData : IActionData
	{
		public required string Path { get; set; }
	}

	public class DelayData : IActionData
	{
		public int Milliseconds { get; set; } = 0;
	}

	public class EntityData : IActionData
	{
		public required List<string> Codes { get; set; }
	}

	public class TraitsData : IActionData
	{
		public required List<string> Labels { get; set; }
	}

	[method: JsonConstructor]
	public class ParticlesData(float minQty, float maxQty, byte[] color) : IActionData
	{
		public float MinQty { get; set; } = minQty;
		public float MaxQty { get; set; } = maxQty;
		public byte[] Color { get; set; } = color;
		public double[] MinPos { get; set; } = new double[3];
		public double[] MaxPos { get; set; } = new double[3];
		public float[] MinSpeed { get; set; } = new float[3];
		public float[] MaxSpeed { get; set; } = new float[3];
		public float Duration { get; set; } = 1;
		public float Gravity { get; set; } = 1;
		public float MinSize { get; set; } = 1;
		public float MaxSize { get; set; } = 1;
		public EnumParticleModel Model { get; set; } = EnumParticleModel.Cube;

		public Vec3d MaxPosAsVec => new(MaxPos[0], MaxPos[1], MaxPos[2]);
		public Vec3f MinSpeedAsVec => new(MinSpeed[0], MinSpeed[1], MinSpeed[2]);
		public Vec3f MaxSpeedAsVec => new(MaxSpeed[0], MaxSpeed[1], MaxSpeed[2]);
	}

	public class SmokeData : ParticlesData
	{
		[JsonConstructor]
		public SmokeData(byte[] color) : base(40, 60, color)
		{
			MaxPos = [2, 1, 2];
			MinSpeed = [-0.25f, 0f, -0.25f];
			MaxSpeed = [0.25f, 0f, 0.25f];
			Duration = 0.6f;
			Gravity = -0.075f;
			MinSize = 0.5f;
			MaxSize = 3f;
			Model = EnumParticleModel.Quad;
		}
	}

	public class GreySmokeData() : SmokeData([100, 100, 100, 80]) { }

	public class HealData : IActionData
	{
		public float Amount { get; set; } = 0;
	}

	public class DamageData : IActionData
	{
		public float Amount { get; set; } = 0;
	}

	public class AttributeData : IActionData
	{
		public required string Name { get; set; }
	}

	public class AttributeData<T> : AttributeData
	{
		public required T Value { get; set; }
	}
}