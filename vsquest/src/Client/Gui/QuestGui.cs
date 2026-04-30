using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace VSQuest.Client
{
	public class QuestGui : GuiDialog
	{
		public override string ToggleKeyCombinationCode => string.Empty;

		readonly bool _closeGuiAfterAcceptingAndCompleting;
		readonly long _questGiverId;

		readonly List<string> _availableQuestIds;
		readonly Dictionary<string, bool> _activeQuestData;

		string _selectedAvailableQuestId = string.Empty;
		string _selectedActiveQuestId = string.Empty;

		int _curTab = 0;


		public QuestGui(QuestGiverInfoMessage message, QuestConfig config) : base(ApiModHelper.Api)
		{
			_questGiverId = message.GiverId;
			_availableQuestIds = [.. message.AvailableQuestIds];
			_activeQuestData = message.ActiveQuestData.ToDictionary();

			_closeGuiAfterAcceptingAndCompleting = config.CloseGuiAfterAcceptingAndCompleting;
			Recompose();
		}

		public static QuestGui Show(QuestGiverInfoMessage questInfo, QuestConfig config)
		{
			var gui = new QuestGui(questInfo, config);
			gui.TryOpen(true);
			return gui;
		}

		private void Recompose()
		{
			var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
			var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
			var questTextBounds = ElementBounds.Fixed(0, 60, 400, 500);
			var scrollbarBounds = questTextBounds.CopyOffsetedSibling(questTextBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(questTextBounds.fixedHeight);
			var clippingBounds = questTextBounds.ForkBoundingParent();
			var bottomLeftButtonBounds = ElementBounds.Fixed(10, 570, 200, 20);
			var bottomRightButtonBounds = ElementBounds.Fixed(220, 570, 200, 20);

			GuiTab[] tabs = [
				new GuiTab() { Name = Lang.Get("vsquest:tab-available-quests"), DataInt = 0 },
				new GuiTab() { Name = Lang.Get("vsquest:tab-active-quests"), DataInt = 1 }
			];

			bgBounds.BothSizing = ElementSizing.FitToChildren;
			SingleComposer = capi.Gui.CreateCompo("QuestSelectDialog-", dialogBounds)
							.AddShadedDialogBG(bgBounds)
							.AddDialogTitleBar(Lang.Get("vsquest:quest-select-title"), () => TryClose())
							.AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 200), OnTabClicked, "tabs")
							.BeginChildElements(bgBounds);
			SingleComposer.GetVerticalTab("tabs").ActiveElement = _curTab;

			if (_curTab == 0)
			{
				if (_availableQuestIds.Count > 0)
				{
					_selectedAvailableQuestId = _availableQuestIds[0];
					SingleComposer.AddDropDown([.. _availableQuestIds], [.. _availableQuestIds.Select(id => Lang.Get($"{id}-title"))], 0, OnAvailableQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30))
						.AddButton(Lang.Get("vsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
						.AddButton(Lang.Get("vsquest:button-accept"), AcceptQuest, bottomRightButtonBounds)
						.BeginClip(clippingBounds)
							.AddRichtext(QuestText(_availableQuestIds[0]), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
						.EndClip()
						.AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
				}
				else
				{
					SingleComposer
						.AddStaticText(Lang.Get("vsquest:no-quest-available-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
						.AddButton(Lang.Get("vsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
				}
			}
			else
			{
				if (_activeQuestData.Count > 0)
				{
					if (_selectedActiveQuestId == string.Empty) _selectedActiveQuestId = _activeQuestData.Keys.First();

					int selected = _activeQuestData.Keys.IndexOf(id => id == _selectedActiveQuestId);
					SingleComposer.AddDropDown(
						[.. _activeQuestData.Keys],
						[.. _activeQuestData.Keys.Select(id => Lang.Get($"{id}-title"))],
						selected,
						OnActiveQuestSelectionChanged,
						ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30))
						.AddButton(Lang.Get("vsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
						.AddIf(_activeQuestData[_selectedActiveQuestId])
							.AddButton(Lang.Get("vsquest:button-complete"), CompleteQuest, bottomRightButtonBounds)
						.EndIf()
						.BeginClip(clippingBounds)
							.AddRichtext(Lang.Get($"{_selectedActiveQuestId}-desc"), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
						.EndClip()
						.AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
				}
				else
				{
					SingleComposer.AddStaticText(Lang.Get("vsquest:no-quest-active-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
						.AddButton(Lang.Get("vsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
				}
			}
			;
			SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)questTextBounds.fixedHeight, (float)questTextBounds.fixedHeight);
			SingleComposer.EndChildElements()
					.Compose();
			SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
			SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
		}

		void OnNewScrollbarvalue(float value)
		{
			var textArea = SingleComposer.GetRichtext("questtext");

			textArea.Bounds.fixedY = -value;
			textArea.Bounds.CalcWorldBounds();
		}

		void OnTabClicked(int id, GuiTab tab)
		{
			_curTab = id;
			Recompose();
		}

		static string QuestText(string questId)
		{
			return Lang.Get($"{questId}-desc");
		}

		static string ActiveQuestText(Quest quest)
		{
			string progress = Lang.Get($"{quest.Info.Id}-obj", [..quest.Progress(ApiModHelper.Player).Select(x => x.ToString())]);
			if (string.IsNullOrEmpty(progress))
			{
				return QuestText(quest.Info.Id);
			}
			else
			{
				return $"{QuestText(quest.Info.Id)}<br><br><strong>Progress</strong><br>{progress}";
			}
		}

		bool AcceptQuest()
		{
			var message = new QuestAcceptedMessage(_selectedAvailableQuestId, _questGiverId);
			capi.Network.GetChannel("vsquest").SendPacket(message);
			if (_closeGuiAfterAcceptingAndCompleting)
			{
				TryClose();
			}
			else
			{
				_availableQuestIds.Remove(_selectedAvailableQuestId);
				Recompose();
			}
			return true;
		}

		bool CompleteQuest()
		{
			var message = new QuestCompletedMessage(_selectedActiveQuestId, _questGiverId);
			capi.Network.GetChannel("vsquest").SendPacket(message);
			if (_closeGuiAfterAcceptingAndCompleting)
			{
				TryClose();
			}
			else
			{
				_activeQuestData.Remove(_selectedActiveQuestId);
				Recompose();
			}
			return true;
		}
		void OnAvailableQuestSelectionChanged(string questId, bool selected)
		{
			if (selected)
			{
				_selectedAvailableQuestId = questId;
				SingleComposer.GetRichtext("questtext").SetNewText(QuestText(questId), CairoFont.WhiteSmallishText());
				SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
			}
		}

		void OnActiveQuestSelectionChanged(string questId, bool selected)
		{
			if (selected)
			{
				_selectedActiveQuestId = questId;
				SingleComposer.GetRichtext("questtext").SetNewText(QuestText(questId), CairoFont.WhiteSmallishText());
				Recompose();
			}
		}
	}
}