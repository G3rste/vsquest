using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VSQuest.Client
{
	public class QuestConfig
	{
		public bool CloseGuiAfterAcceptingAndCompleting { get; set; } = true;

		public static QuestConfig Get()
		{
			const string FILENAME = "vsquest-config.json";
			QuestConfig? config;
			try
			{
				config = ApiModHelper.LoadConfig(FILENAME);
				if (config == null)
				{
					config = new();
					ApiModHelper.Notify("Config file not found : falling back to default settings");
				}
				else
				{
					ApiModHelper.Notify("Config file successfully loaded");
				}
			}
			catch (Exception ex)
			{
				ApiModHelper.Error($"Config file parsing failed due to : {ex.Message}");
				ApiModHelper.Warn("Falling back to default settings");

				return new();
			}
			ApiModHelper.StoreConfig(config, FILENAME);

			return config;
		}
	}

	public class ClientSide : IDisposable
	{
		readonly QuestConfig _config;

		QuestGui? _gui;

		public ClientSide(ICoreClientAPI api, Mod mod)
		{
			ApiModHelper.Api = api;
			ApiModHelper.Mod = mod;

			api.Network.GetChannel(mod.Info.ModID).SetMessageHandler<QuestGiverInfoResponse>(OnQuestGiverInfoMessage);

			_config = QuestConfig.Get();
		}

		void OnQuestGiverInfoMessage(QuestGiverInfoResponse message)
		{
			_gui = QuestGui.Show(message, _config);
			//_gui.OnClosed += Gui_OnClosed;
			//_gui.OnQuestAccepted += Gui_OnQuestAccepted;
			//_gui.OnQuestCompleted += Gui_OnQuestCompleted;
		}

		public void Dispose()
		{

			GC.SuppressFinalize(this);
		}
	}

	public static class ApiModHelper
	{
		static ICoreClientAPI? _api;
		public static ICoreClientAPI Api
		{
			get => _api ?? throw new Exception("Api is null");
			set => _api = value;
		}

		static Mod? _mod;
		public static Mod Mod
		{
			get => _mod ?? throw new Exception("Mod is null");
			set => _mod = value;
		}

		public static IClientPlayer Player => Api.World.Player;

		public static IClientNetworkChannel GetChannel() => Api.Network.GetChannel(Mod.Info.ModID);
		public static QuestConfig? LoadConfig(string filename) => Api.LoadModConfig<QuestConfig>(filename);
		public static void StoreConfig(QuestConfig config, string filename) => Api.StoreModConfig(config, filename);
		public static void Notify(string message) => Mod.Logger.Notification(message);
		public static void Warn(string message) => Mod.Logger.Warning(message);
		public static void Error(string message) => Mod.Logger.Error(message);
	}
}