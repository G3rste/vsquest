using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VSQuest.Model.Server.Data
{
	public interface IData { }
	//Raw data containing bare json
	public record RawData(JObject Json) : IData;

	public record QuestData(string? Id, long? GiverId) : IData;

	public record ItemData(string Code, int Amount = 0) : IData;

	public record SoundData(string Path) : IData;

	public record DelayData(int Milliseconds = 0) : IData;

	public record EntityData(List<string> Codes) : IData;

	public record TraitsData(List<string> Labels) : IData;

	//TODO : turn this into a record? Is record a good idea for those?
	[method: JsonConstructor]
	public class ParticlesData(float minQty, float maxQty, byte[] color) : IData
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

	public record HealData(float Amount = 0) : IData;
	public record DamageData(float Amount = 0) : IData;

	public record AttributeData(string Name) : IData;
	public record AttributeData<T>(string Name, T Value) : AttributeData(Name);

	public interface IRuntimeData : IData
	{
		ICoreServerAPI Api { get; }
	}
	public record RuntimeData(ICoreServerAPI Api) : IRuntimeData;

	#region ObjectiveData

	public interface IObjectiveData : IData;
	public record ObjectiveData(string Id) : IObjectiveData;

	public interface IObjectiveData<T> : IObjectiveData
	{
		T Goal { get; }
	}
	public record ObjectiveData<T>(T Goal) : IObjectiveData<T>;

	#endregion

	public record KillData(int Goal, string[] Codes) : IObjectiveData<int>;

	public record FlowersNearbyData(int Goal, string[] Codes) : IObjectiveData<int>;
}