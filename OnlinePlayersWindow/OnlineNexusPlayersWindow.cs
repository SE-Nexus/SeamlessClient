using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.GameSystems.Trading;
using Sandbox.Game.Gui;
using Sandbox.Game.GUI;
using Sandbox.Game.VoiceChat;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using VRage.Audio;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage;
using VRage.GameServices;
using VRage.Utils;
using VRageMath;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Utils;
using Sandbox.Game.Localization;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.SessionComponents;
using Sandbox.Game;
using Sandbox.Graphics;
using VRage.Game.News;
using VRage.Network;
using VRage.Serialization;
using VRage.Input;
using SeamlessClient.Utilities;
using System.Reflection;
using HarmonyLib;
using SeamlessClient.Messages;

namespace SeamlessClient.OnlinePlayersWindow
{
    [HarmonyPatch]
    public class OnlineNexusPlayersWindow : MyGuiScreenBase
    {

        protected const string OWNER_MARKER = "*****";

        protected const string SERVICE_XBL = "Xbox Live";

        protected const string SERVICE_Steam = "Steam";

        protected int PlayerNameColumn = 0;

        protected int PlayerFactionNameColumn = 1;

        protected int GameAdminColumn = 2;

        protected int GamePingColumn = 3;

        protected int PlayerMutedColumn = 4;

        protected int PlayerServerColumn = 5;


        protected int PlayerTableColumnsCount = 6;

        private int m_warfareUpdate_frameCount = 30;

        private int m_warfareUpdate_frameCount_current = 30;

        private bool m_getPingAndRefresh = true;

        protected MyGuiControlButton m_profileButton;

        protected MyGuiControlButton m_promoteButton;

        protected MyGuiControlButton m_demoteButton;

        protected MyGuiControlButton m_kickButton;

        protected MyGuiControlButton m_banButton;

        protected MyGuiControlButton m_inviteButton;

        protected MyGuiControlButton m_tradeButton;

        protected bool m_isScenarioRunning;

        protected MyGuiControlLabel m_maxPlayersValueLabel;

        protected MyGuiControlTable m_playersTable;

        protected MyGuiControlCombobox m_lobbyTypeCombo;

        protected MyGuiControlSlider m_maxPlayersSlider;

        protected Dictionary<ulong, short> pings = new Dictionary<ulong, short>();

        private MyGuiControlLabel m_warfare_timeRemainting_time;

        private MyGuiControlLabel m_warfare_timeRemainting_label;

        protected ulong m_lastSelected;

        private MyGuiControlLabel m_caption;

        private readonly MyGuiControlButton.StyleDefinition m_buttonSizeStyleMuted = new MyGuiControlButton.StyleDefinition
        {
            NormalFont = "White",
            HighlightFont = "White",
            NormalTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_MUTED,
            HighlightTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_MUTED
        };

        private readonly MyGuiControlButton.StyleDefinition m_buttonSizeStyleTalking = new MyGuiControlButton.StyleDefinition
        {
            NormalFont = "White",
            HighlightFont = "White",
            NormalTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_TALKING,
            HighlightTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_TALKING
        };

        private readonly MyGuiControlButton.StyleDefinition m_buttonSizeStyleSilent = new MyGuiControlButton.StyleDefinition
        {
            NormalFont = "White",
            HighlightFont = "White",
            NormalTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_SILENT,
            HighlightTexture = MyGuiConstants.TEXTURE_HUD_VOICE_CHAT_SILENT
        };

        private bool m_waitingTradeResponse;

        private int m_maxPlayers;

        private int m_lastMuteIndicatorsUpdate;

        private bool SelectedProfileEnabled
        {
            get
            {
                if (m_playersTable?.SelectedRow != null)
                {
                    return MyMultiplayer.Static.GetMemberServiceName((ulong)m_playersTable.SelectedRow.UserData) == MyGameService.Service.ServiceName;
                }
                return false;
            }
        }



        public OnlineNexusPlayersWindow()
        : base(null, size: new Vector2(0.87f, 0.813f), backgroundColor: MyGuiConstants.SCREEN_BACKGROUND_COLOR, isTopMostScreen: false, backgroundTexture: MyGuiConstants.TEXTURE_SCREEN_BACKGROUND.Texture, backgroundTransition: MySandboxGame.Config.UIBkOpacity, guiTransition: MySandboxGame.Config.UIOpacity)
        {
            base.EnabledBackgroundFade = true;


            MyMultiplayer.Static.ClientJoined += Multiplayer_PlayerJoined;
            MyMultiplayer.Static.ClientLeft += Multiplayer_PlayerLeft;
            MySession.Static.Factions.FactionCreated += OnFactionCreated;
            MySession.Static.Factions.FactionEdited += OnFactionEdited;
            MySession.Static.Factions.FactionStateChanged += OnFactionStateChanged;
            MySession.Static.OnUserPromoteLevelChanged += OnUserPromoteLevelChanged;
            if (MyMultiplayer.Static is MyMultiplayerLobby myMultiplayerLobby)
            {
                myMultiplayerLobby.OnLobbyDataUpdated += Matchmaking_LobbyDataUpdate;
            }
            if (MyMultiplayer.Static is MyMultiplayerLobbyClient myMultiplayerLobbyClient)
            {
                myMultiplayerLobbyClient.OnLobbyDataUpdated += Matchmaking_LobbyDataUpdate;
            }

            RecreateControls(true);
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(MyGuiScreenPlayers), "SendPingsAndRefresh")]
        private static void SendPingsAndRefresh1(SerializableDictionary<ulong, short> dictionary)
        {
            //Seamless.TryShow("Hello World!");

            if (Sandbox.Engine.Platform.Game.IsDedicated)
            {
                return;
            }

            OnlineNexusPlayersWindow firstScreenOfType = MyScreenManager.GetFirstScreenOfType<OnlineNexusPlayersWindow>();
            if (firstScreenOfType == null)
            {
                return;
            }

            firstScreenOfType.pings.Clear();
            foreach (KeyValuePair<ulong, short> item in dictionary.Dictionary)
            {
                firstScreenOfType.pings[item.Key] = item.Value;
            }

            firstScreenOfType.RecreateControls(false);
        }



        public override string GetFriendlyName()
        {
            return "NexusOnlinePlayers";
        }

        protected override void OnClosed()
        {
            if (m_waitingTradeResponse)
            {
                MyTradingManager.Static.TradeCancel_Client();
                m_waitingTradeResponse = false;
            }
            base.OnClosed();
            if (MyMultiplayer.Static != null)
            {
                MyMultiplayer.Static.ClientJoined -= Multiplayer_PlayerJoined;
                MyMultiplayer.Static.ClientLeft -= Multiplayer_PlayerLeft;
            }
            if (MySession.Static != null)
            {
                MySession.Static.Factions.FactionCreated -= OnFactionCreated;
                MySession.Static.Factions.FactionEdited -= OnFactionEdited;
                MySession.Static.Factions.FactionStateChanged -= OnFactionStateChanged;
                MySession.Static.OnUserPromoteLevelChanged -= OnUserPromoteLevelChanged;
            }
            if (MyMultiplayer.Static is MyMultiplayerLobby myMultiplayerLobby)
            {
                myMultiplayerLobby.OnLobbyDataUpdated -= Matchmaking_LobbyDataUpdate;
            }
            if (MyMultiplayer.Static is MyMultiplayerLobbyClient myMultiplayerLobbyClient)
            {
                myMultiplayerLobbyClient.OnLobbyDataUpdated -= Matchmaking_LobbyDataUpdate;
            }
            MyVoiceChatSessionComponent.Static.OnPlayerMutedStateChanged -= OnPlayerMutedStateChanged;
            MyGuiScreenGamePlay.ActiveGameplayScreen = null;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            base.CloseButtonEnabled = true;
            Vector2 vector = base.Size.Value / MyGuiConstants.TEXTURE_SCREEN_BACKGROUND.SizeGui;
            _ = -0.5f * base.Size.Value + vector * MyGuiConstants.TEXTURE_SCREEN_BACKGROUND.PaddingSizeGui * 1.1f;

            m_caption = AddCaption(MyCommonTexts.ScreenCaptionPlayers, null, new Vector2(0f, 0.003f));

            float LeftX = -0.4f;



            MyGuiControlSeparatorList myGuiControlSeparatorList = new MyGuiControlSeparatorList();
            myGuiControlSeparatorList.AddHorizontal(new Vector2(LeftX, -0.331f), 0.79f);

            Vector2 start = new Vector2(LeftX, 0.358f);
            myGuiControlSeparatorList.AddHorizontal(start, 0.79f);
            myGuiControlSeparatorList.AddHorizontal(new Vector2(LeftX, 0.05f), 0.17f);
            Controls.Add(myGuiControlSeparatorList);


            Vector2 vector2 = new Vector2(0f, 0.057f);
            Vector2 vector3 = new Vector2(LeftX, -0.304f);

            m_profileButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, new Vector2(.4f, .2f), null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Profile));
            m_profileButton.ButtonClicked += profileButton_ButtonClicked;
            Controls.Add(m_profileButton);


            vector3 += vector2;
            m_promoteButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Promote));
            m_promoteButton.ButtonClicked += promoteButton_ButtonClicked;
            Controls.Add(m_promoteButton);
            vector3 += vector2;
            m_demoteButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Demote));
            m_demoteButton.ButtonClicked += demoteButton_ButtonClicked;
            Controls.Add(m_demoteButton);
            vector3 += vector2;
            m_kickButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Kick));
            m_kickButton.ButtonClicked += kickButton_ButtonClicked;
            Controls.Add(m_kickButton);
            vector3 += vector2;
            m_banButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Ban));
            m_banButton.ButtonClicked += banButton_ButtonClicked;
            Controls.Add(m_banButton);
            vector3 += vector2;
            m_tradeButton = new MyGuiControlButton(vector3, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MySpaceTexts.PlayersScreen_TradeBtn));
            m_tradeButton.SetTooltip(MyTexts.GetString(MySpaceTexts.PlayersScreen_TradeBtn_TTP));
            m_tradeButton.ButtonClicked += tradeButton_ButtonClicked;
            Controls.Add(m_tradeButton);
            bool num = MyMultiplayer.Static != null && MyMultiplayer.Static.IsLobby;
            Vector2 vector4 = vector3 + new Vector2(-0.0f, m_tradeButton.Size.Y + 0.03f);

            MyGuiControlLabel control = new MyGuiControlLabel(vector4, null, MyTexts.GetString(MySpaceTexts.PlayersScreen_LobbyType), null, 0.8f, "Blue", MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            if (num)
            {
                Controls.Add(control);
            }
            vector4 += new Vector2(0f, 0.033f);
            m_lobbyTypeCombo = new MyGuiControlCombobox(vector4, null, null, null, 3);
            m_lobbyTypeCombo.Size = new Vector2(0.175f, 0.04f);
            m_lobbyTypeCombo.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            m_lobbyTypeCombo.AddItem(0L, MyCommonTexts.ScreenPlayersLobby_Private);
            m_lobbyTypeCombo.AddItem(1L, MyCommonTexts.ScreenPlayersLobby_Friends);
            m_lobbyTypeCombo.AddItem(2L, MyCommonTexts.ScreenPlayersLobby_Public);
            m_lobbyTypeCombo.SelectItemByKey((long)MyMultiplayer.Static.GetLobbyType());
            if (num)
            {
                Controls.Add(m_lobbyTypeCombo);
            }
            Vector2 vector5 = vector4 + new Vector2(0f, 0.05f);
            MyGuiControlLabel control2 = new MyGuiControlLabel(vector5 + new Vector2(0.001f, 0f), null, MyTexts.GetString(MyCommonTexts.MaxPlayers) + ":", null, 0.8f, "Blue", MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            if (num)
            {
                Controls.Add(control2);
            }
            m_maxPlayersValueLabel = new MyGuiControlLabel(vector5 + new Vector2(0.169f, 0f), null, null, null, 0.8f, "Blue", MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP);
            if (num)
            {
                Controls.Add(m_maxPlayersValueLabel);
            }
            vector5 += new Vector2(0f, 0.03f);
            m_maxPlayers = (Sync.IsServer ? MyMultiplayerLobby.MAX_PLAYERS : 16);
            m_maxPlayersSlider = new MyGuiControlSlider(vector5, 2f, Math.Max(m_maxPlayers, 3), 0.177f, Sync.IsServer ? MySession.Static.MaxPlayers : MyMultiplayer.Static.MemberLimit, null, null, 1, 0.8f, 0f, "White", null, MyGuiControlSliderStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, intValue: true);
            m_isScenarioRunning = MyCampaignManager.Static.IsScenarioRunning;
            m_maxPlayersSlider.Enabled = !m_isScenarioRunning;
            m_maxPlayersValueLabel.Text = m_maxPlayersSlider.Value.ToString();
            m_maxPlayersSlider.ValueChanged = MaxPlayersSlider_Changed;
            if (num)
            {
                Controls.Add(m_maxPlayersSlider);
            }
            m_inviteButton = new MyGuiControlButton(new Vector2(LeftX, 0.28500003f), MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, MyTexts.Get(MyCommonTexts.ScreenPlayers_Invite));
            m_inviteButton.ButtonClicked += inviteButton_ButtonClicked;
            Controls.Add(m_inviteButton);


            Vector2 vector6 = new Vector2(0.4f, -0.306f);
            Vector2 size = new Vector2(0.62f, 0.813f);
            int num2 = 18;
            float num3 = 0f;

            m_playersTable = new MyGuiControlTable
            {
                Position = vector6,
                Size = size,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                ColumnsCount = PlayerTableColumnsCount
            };
            m_playersTable.GamepadHelpTextId = MySpaceTexts.PlayersScreen_Help_PlayersList;
            m_playersTable.VisibleRowsCount = num2;

            float num6 = 0.25f;
            float num7 = 0.25f;
            float num8 = 0.12f;
            float num9 = 0.08f;
            float num10 = 0.08f;
            float num11 = 1 - num6 - num7 - num8 - num9 - num10;


            m_playersTable.SetCustomColumnWidths(new float[6]
            {
            num6,
            num7,
            num8,
            num9,
            num10,
            num11
            });

            m_playersTable.SetColumnComparison(PlayerNameColumn, (MyGuiControlTable.Cell a, MyGuiControlTable.Cell b) => a.Text.CompareToIgnoreCase(b.Text));
            m_playersTable.SetColumnName(PlayerNameColumn, MyTexts.Get(MyCommonTexts.ScreenPlayers_PlayerName));
            m_playersTable.SetColumnComparison(PlayerFactionNameColumn, (MyGuiControlTable.Cell a, MyGuiControlTable.Cell b) => a.Text.CompareToIgnoreCase(b.Text));
            m_playersTable.SetColumnName(PlayerFactionNameColumn, MyTexts.Get(MyCommonTexts.ScreenPlayers_FactionName));
            m_playersTable.SetColumnName(PlayerMutedColumn, new StringBuilder("VC"));
            m_playersTable.SetColumnComparison(GameAdminColumn, GameAdminCompare);
            m_playersTable.SetColumnName(GameAdminColumn, MyTexts.Get(MyCommonTexts.ScreenPlayers_Rank));
            m_playersTable.SetColumnComparison(GamePingColumn, GamePingCompare);
            m_playersTable.SetColumnName(GamePingColumn, MyTexts.Get(MyCommonTexts.ScreenPlayers_Ping));
            m_playersTable.SetColumnName(PlayerServerColumn, new StringBuilder("OnServer"));


            m_playersTable.ItemSelected += playersTable_ItemSelected;
            m_playersTable.UpdateTableSortHelpText();
            Controls.Add(m_playersTable);


            foreach (MyPlayer onlinePlayer in Sync.Players.GetOnlinePlayers())
            {
                if (onlinePlayer.Id.SerialId != 0)
                {
                    continue;
                }
                for (int j = 0; j < m_playersTable.RowsCount; j++)
                {
                    MyGuiControlTable.Row row = m_playersTable.GetRow(j);
                    if (row.UserData is ulong)
                    {
                        _ = (ulong)row.UserData;
                        _ = onlinePlayer.Id.SteamId;
                    }
                }

                AddPlayer(onlinePlayer.Id.SteamId, $"{MyMultiplayer.Static.HostName ?? "thisServer"}*");
            }


            foreach(var server in PlayersWindowComponent.allServers)
            {

                foreach(var player in server.Players)
                    AddPlayerFromOtherServer(player, server.ServerName);
            }


            m_lobbyTypeCombo.ItemSelected += lobbyTypeCombo_OnSelect;
            if (m_lastSelected != 0L)
            {
                MyGuiControlTable.Row row2 = m_playersTable.Find((MyGuiControlTable.Row r) => (ulong)r.UserData == m_lastSelected);
                if (row2 != null)
                {
                    m_playersTable.SelectedRow = row2;
                }
            }
            UpdateButtonsEnabledState();
            UpdateCaption();
            Vector2 minSizeGui = MyGuiControlButton.GetVisualStyle(MyGuiControlButtonStyleEnum.Default).NormalTexture.MinSizeGui;
            MyGuiControlLabel myGuiControlLabel = new MyGuiControlLabel(new Vector2(start.X, start.Y + minSizeGui.Y / 2f));
            myGuiControlLabel.Name = MyGuiScreenBase.GAMEPAD_HELP_LABEL_NAME;
            Controls.Add(myGuiControlLabel);
            base.GamepadHelpTextId = MySpaceTexts.PlayersScreen_Help_Screen;
            base.FocusedControl = m_playersTable;
        }

        private void profileButton_ButtonClicked(MyGuiControlButton obj)
        {
            if (SelectedProfileEnabled)
            {
                MyGameService.OpenOverlayUser((ulong)m_playersTable.SelectedRow.UserData);
            }
        }

        private void OnPlayerMutedStateChanged(ulong playerId, bool isMuted)
        {
            RefreshMuteIcons();
        }

        private void tradeButton_ButtonClicked(MyGuiControlButton obj)
        {
            ulong num = ((m_playersTable.SelectedRow != null) ? ((ulong)m_playersTable.SelectedRow.UserData) : 0);
            if (num != 0L && num != MyGameService.OnlineUserId)
            {
                m_waitingTradeResponse = true;
                MyTradingManager.Static.TradeRequest_Client(num, OnAnswerRecieved);
                if (m_waitingTradeResponse)
                {
                    m_tradeButton.Enabled = false;
                    m_tradeButton.Text = MyTexts.GetString(MySpaceTexts.PlayersScreen_TradeBtn_Waiting);
                }
            }
        }

        private void OnAnswerRecieved(MyTradeResponseReason reason)
        {
            m_waitingTradeResponse = false;
            UpdateTradeButton();
            m_tradeButton.Text = MyTexts.GetString(MySpaceTexts.PlayersScreen_TradeBtn);
        }

        private int GameAdminCompare(MyGuiControlTable.Cell a, MyGuiControlTable.Cell b)
        {
            ulong steamId = (ulong)a.Row.UserData;
            ulong steamId2 = (ulong)b.Row.UserData;
            int userPromoteLevel = (int)MySession.Static.GetUserPromoteLevel(steamId);
            int userPromoteLevel2 = (int)MySession.Static.GetUserPromoteLevel(steamId2);
            return userPromoteLevel.CompareTo(userPromoteLevel2);
        }

        private int GamePingCompare(MyGuiControlTable.Cell a, MyGuiControlTable.Cell b)
        {
            if (!int.TryParse(a.Text.ToString(), out var result))
            {
                result = -1;
            }
            if (!int.TryParse(b.Text.ToString(), out var result2))
            {
                result2 = -1;
            }
            return result.CompareTo(result2);
        }

        private int PlatformCompare(MyGuiControlTable.Cell a, MyGuiControlTable.Cell b)
        {
            return a.Text.CompareTo(b.Text);
        }

        protected void OnToggleMutePressed(MyGuiControlButton button)
        {
            ulong num = (ulong)button.UserData;
            bool muted = button.CustomStyle != m_buttonSizeStyleMuted;
            string memberServiceName = MyMultiplayer.Static.GetMemberServiceName(num);
            if (MyGameService.Service.OpenProfileForMute && memberServiceName == MyGameService.Service.ServiceName)
            {
                profileButton_ButtonClicked(null);
                return;
            }
            MyVoiceChatSessionComponent.Static.SetPlayerMuted(num, muted);
            RefreshMuteIcons();
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            base.HandleInput(receivedFocusInThisUpdate);
            if (MyInput.Static.IsNewKeyPressed(MyKeys.F3))
            {
                MyGuiAudio.PlaySound(MyGuiSounds.HudMouseClick);
                CloseScreen();
            }
            if (base.FocusedControl == m_playersTable && MyControllerHelper.IsControl(MySpaceBindingCreator.CX_GUI, MyControlsGUI.BUTTON_X))
            {
                MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
                tradeButton_ButtonClicked(null);
            }
            if (base.FocusedControl == m_playersTable && MyControllerHelper.IsControl(MySpaceBindingCreator.CX_GUI, MyControlsGUI.ACCEPT))
            {
                MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
                profileButton_ButtonClicked(null);
            }
            if (base.FocusedControl == m_playersTable && MyControllerHelper.IsControl(MySpaceBindingCreator.CX_GUI, MyControlsGUI.BUTTON_Y) && m_playersTable.GetInnerControlsFromCurrentCell(PlayerMutedColumn) is MyGuiControlButton myGuiControlButton)
            {
                MyGuiSoundManager.PlaySound(GuiSounds.MouseClick);
                myGuiControlButton.PressButton();
            }
        }



        public void AddPlayerFromOtherServer(OnlinePlayer player, string ServerName)
        {

            string memberName = player.PlayerName;
            ulong userId = player.SteamID;

            MyGuiControlTable.Row row = new MyGuiControlTable.Row(userId);
            StringBuilder text = new StringBuilder();


            MyGuiControlTable.Cell cell = new MyGuiControlTable.Cell(memberName, memberName);
            cell.IsAutoScaleEnabled = true;
            cell.MinTextScale = 0.3f;
            row.AddCell(cell);

            long playerId = player.IdentityID;
            MyFaction playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            string text2 = "";
            StringBuilder stringBuilder = new StringBuilder();
            if (playerFaction != null)
            {
                text2 += playerFaction.Name;
                text2 = text2 + " | " + memberName;
                foreach (KeyValuePair<long, MyFactionMember> member in playerFaction.Members)
                {
                    if ((member.Value.IsLeader || member.Value.IsFounder) && MySession.Static.Players.TryGetPlayerId(member.Value.PlayerId, out var result) && MySession.Static.Players.TryGetPlayerById(result, out var playera))
                    {
                        text2 = text2 + " | " + playera.DisplayName;
                        break;
                    }
                }
                stringBuilder.Append(MyStatControlText.SubstituteTexts(playerFaction.Name));
                if (playerFaction.IsLeader(playerId))
                {
                    stringBuilder.Append(" (").Append(MyTexts.Get(MyCommonTexts.Leader)).Append(")");
                }
                if (!string.IsNullOrEmpty(playerFaction.Tag))
                {
                    stringBuilder.Insert(0, "[" + playerFaction.Tag + "] ");
                }
            }
            row.AddCell(new MyGuiControlTable.Cell(stringBuilder, null, text2));
            StringBuilder stringBuilder2 = new StringBuilder();
            MyPromoteLevel userPromoteLevel = MySession.Static.GetUserPromoteLevel(userId);
            for (int i = 0; i < (int)userPromoteLevel; i++)
            {
                stringBuilder2.Append("*");
            }
            row.AddCell(new MyGuiControlTable.Cell(stringBuilder2));
            if (pings.ContainsKey(userId))
            {
                row.AddCell(new MyGuiControlTable.Cell(new StringBuilder(pings[userId].ToString())));
            }
            else
            {
                row.AddCell(new MyGuiControlTable.Cell(new StringBuilder("----")));
            }
            MyGuiControlTable.Cell cell2 = new MyGuiControlTable.Cell(new StringBuilder(""));
            row.AddCell(cell2);
            if (userId != Sync.MyId)
            {
                MyGuiControlButton myGuiControlButton = new MyGuiControlButton();
                myGuiControlButton.CustomStyle = m_buttonSizeStyleSilent;
                myGuiControlButton.Size = new Vector2(0.03f, 0.04f);
                myGuiControlButton.CueEnum = GuiSounds.None;
                myGuiControlButton.ButtonClicked += OnToggleMutePressed;
                myGuiControlButton.UserData = userId;
                cell2.Control = myGuiControlButton;
                m_playersTable.Controls.Add(myGuiControlButton);
                RefreshMuteIcons();
            }


            row.AddCell(new MyGuiControlTable.Cell(ServerName));


            m_playersTable.Add(row);
            UpdateCaption();
        }




        protected void AddPlayer(ulong userId, string ServerName)
        {
            string memberName = MyMultiplayer.Static.GetMemberName(userId);
            if (string.IsNullOrEmpty(memberName))
            {
                return;
            }


            MyGuiControlTable.Row row = new MyGuiControlTable.Row(userId);
            string memberServiceName = MyMultiplayer.Static.GetMemberServiceName(userId);
            StringBuilder text = new StringBuilder();

            MyGuiControlTable.Cell cell = new MyGuiControlTable.Cell(memberName, memberName);
            cell.IsAutoScaleEnabled = true;
            cell.MinTextScale = 0.3f;
            row.AddCell(cell);

            long playerId = Sync.Players.TryGetIdentityId(userId);
            MyFaction playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
            string text2 = "";
            StringBuilder stringBuilder = new StringBuilder();
            if (playerFaction != null)
            {
                text2 += playerFaction.Name;
                text2 = text2 + " | " + memberName;
                foreach (KeyValuePair<long, MyFactionMember> member in playerFaction.Members)
                {
                    if ((member.Value.IsLeader || member.Value.IsFounder) && MySession.Static.Players.TryGetPlayerId(member.Value.PlayerId, out var result) && MySession.Static.Players.TryGetPlayerById(result, out var player))
                    {
                        text2 = text2 + " | " + player.DisplayName;
                        break;
                    }
                }
                stringBuilder.Append(MyStatControlText.SubstituteTexts(playerFaction.Name));
                if (playerFaction.IsLeader(playerId))
                {
                    stringBuilder.Append(" (").Append(MyTexts.Get(MyCommonTexts.Leader)).Append(")");
                }
                if (!string.IsNullOrEmpty(playerFaction.Tag))
                {
                    stringBuilder.Insert(0, "[" + playerFaction.Tag + "] ");
                }
            }
            row.AddCell(new MyGuiControlTable.Cell(stringBuilder, null, text2));
            StringBuilder stringBuilder2 = new StringBuilder();
            MyPromoteLevel userPromoteLevel = MySession.Static.GetUserPromoteLevel(userId);
            for (int i = 0; i < (int)userPromoteLevel; i++)
            {
                stringBuilder2.Append("*");
            }
            row.AddCell(new MyGuiControlTable.Cell(stringBuilder2));
            if (pings.ContainsKey(userId))
            {
                row.AddCell(new MyGuiControlTable.Cell(new StringBuilder(pings[userId].ToString())));
            }
            else
            {
                row.AddCell(new MyGuiControlTable.Cell(new StringBuilder("----")));
            }
            MyGuiControlTable.Cell cell2 = new MyGuiControlTable.Cell(new StringBuilder(""));
            row.AddCell(cell2);
            if (userId != Sync.MyId)
            {
                MyGuiControlButton myGuiControlButton = new MyGuiControlButton();
                myGuiControlButton.CustomStyle = m_buttonSizeStyleSilent;
                myGuiControlButton.Size = new Vector2(0.03f, 0.04f);
                myGuiControlButton.CueEnum = GuiSounds.None;
                myGuiControlButton.ButtonClicked += OnToggleMutePressed;
                myGuiControlButton.UserData = userId;
                cell2.Control = myGuiControlButton;
                m_playersTable.Controls.Add(myGuiControlButton);
                RefreshMuteIcons();
            }


            row.AddCell(new MyGuiControlTable.Cell(ServerName));


            m_playersTable.Add(row);
            UpdateCaption();
        }

        protected void RemovePlayer(ulong userId)
        {
            m_playersTable.Remove((MyGuiControlTable.Row row) => (ulong)row.UserData == userId);
            UpdateButtonsEnabledState();
            if (MySession.Static != null)
            {
                UpdateCaption();
            }
        }

        private void UpdateCaption()
        {
            string text = string.Empty;


            MyMultiplayerBase mpBase = MyMultiplayer.Static;


            if (mpBase.GetType() == Types.MyMultiplayerClient)
            {
                MyGameServerItem server = (MyGameServerItem)Types.MyMultiplayerClient.GetProperty("Server", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetValue(mpBase);
                if (server != null)
                {
                    text = server.Name;
                }
            }
            else if (MyMultiplayer.Static is MyMultiplayerLobbyClient myMultiplayerLobbyClient)
            {
                text = myMultiplayerLobbyClient.HostName;
            }
            if (string.IsNullOrEmpty(text))
            {
                m_caption.Text = string.Concat(MyTexts.Get(MyCommonTexts.ScreenCaptionPlayers), " (", m_playersTable.RowsCount, " / ", MySession.Static.MaxPlayers, ")");
            }
            else
            {
                m_caption.Text = string.Concat(MyTexts.Get(MyCommonTexts.ScreenCaptionServerName), text, "  -  ", MyTexts.Get(MyCommonTexts.ScreenCaptionPlayers), " (", m_playersTable.RowsCount, " / ", MySession.Static.MaxPlayers, ")");
            }
        }

        protected void UpdateButtonsEnabledState()
        {
            if (MyMultiplayer.Static != null)
            {
                bool num = m_playersTable.SelectedRow != null;
                ulong onlineUserId = MyGameService.OnlineUserId;
                ulong owner = MyMultiplayer.Static.GetOwner();
                ulong num2 = (num ? ((ulong)m_playersTable.SelectedRow.UserData) : 0);
                bool flag = onlineUserId == num2;
                bool flag2 = MySession.Static.IsUserAdmin(onlineUserId);
                bool flag3 = onlineUserId == owner;
                bool flag4 = num && MySession.Static.CanPromoteUser(Sync.MyId, num2);
                bool enabled = num && MySession.Static.CanDemoteUser(Sync.MyId, num2);
                if (num && !flag)
                {
                    m_promoteButton.Enabled = flag4;
                    m_demoteButton.Enabled = enabled;
                    m_kickButton.Enabled = flag4 && flag2;
                    m_banButton.Enabled = flag4 && flag2;
                }
                else
                {
                    m_promoteButton.Enabled = false;
                    m_demoteButton.Enabled = false;
                    m_kickButton.Enabled = false;
                    m_banButton.Enabled = false;
                }


                m_banButton.Enabled &= MyMultiplayer.Static.GetType() == Types.MyMultiplayerClient;
                m_inviteButton.Enabled = MyGameService.IsInviteSupported();
                m_lobbyTypeCombo.Enabled = flag3;
                m_maxPlayersSlider.Enabled = flag3 && m_maxPlayers > 2 && !m_isScenarioRunning;
                m_profileButton.Enabled = SelectedProfileEnabled;
                UpdateTradeButton();
            }
        }

        private void UpdateTradeButton()
        {
            bool num = m_playersTable.SelectedRow != null;
            ulong onlineUserId = MyGameService.OnlineUserId;
            ulong num2 = (num ? ((ulong)m_playersTable.SelectedRow.UserData) : 0);
            bool flag = onlineUserId == num2;
            bool flag2 = MyTradingManager.ValidateTradeProssible(onlineUserId, num2, out var _, out var _) == MyTradeResponseReason.Ok;
            flag2 = !flag && flag2;
            m_tradeButton.Enabled = flag2;
        }

        protected void Multiplayer_PlayerJoined(ulong userId, string userName)
        {
            AddPlayer(userId, PlayersWindowComponent.onlineServer.ServerName);
        }

        protected void Multiplayer_PlayerLeft(ulong userId, MyChatMemberStateChangeEnum arg2)
        {
            RemovePlayer(userId);
        }

        protected void Matchmaking_LobbyDataUpdate(bool success, IMyLobby lobby, ulong memberOrLobby)
        {
            if (success)
            {
                ulong newOwnerId = lobby.OwnerId;
                MyGuiControlTable.Row row2 = m_playersTable.Find((MyGuiControlTable.Row row) => row.GetCell(GameAdminColumn).Text.Length == "*****".Length);
                MyGuiControlTable.Row row3 = m_playersTable.Find((MyGuiControlTable.Row row) => (ulong)row.UserData == newOwnerId);
                row2?.GetCell(GameAdminColumn).Text.Clear();
                row3?.GetCell(GameAdminColumn).Text.Clear().Append("*****");
                MyLobbyType lobbyType = lobby.LobbyType;
                m_lobbyTypeCombo.SelectItemByKey((long)lobbyType, sendEvent: false);
                MySession.Static.Settings.OnlineMode = GetOnlineMode(lobbyType);
                UpdateButtonsEnabledState();
                if (!Sync.IsServer)
                {
                    m_maxPlayersSlider.ValueChanged = null;
                    MySession.Static.Settings.MaxPlayers = (short)MyMultiplayer.Static.MemberLimit;
                    m_maxPlayersSlider.Value = MySession.Static.MaxPlayers;
                    m_maxPlayersSlider.ValueChanged = MaxPlayersSlider_Changed;
                    m_maxPlayersValueLabel.Text = m_maxPlayersSlider.Value.ToString();
                    UpdateCaption();
                }
            }
        }

        protected MyOnlineModeEnum GetOnlineMode(MyLobbyType lobbyType)
        {
            switch (lobbyType)
            {
                case MyLobbyType.Private:
                    return MyOnlineModeEnum.PRIVATE;
                case MyLobbyType.FriendsOnly:
                    return MyOnlineModeEnum.FRIENDS;
                case MyLobbyType.Public:
                    return MyOnlineModeEnum.PUBLIC;
                default:
                    return MyOnlineModeEnum.PUBLIC;
            }
        }

        protected void playersTable_ItemSelected(MyGuiControlTable table, MyGuiControlTable.EventArgs args)
        {
            UpdateButtonsEnabledState();
            if (m_playersTable.SelectedRow != null)
            {
                m_lastSelected = (ulong)m_playersTable.SelectedRow.UserData;
            }
        }

        protected void inviteButton_ButtonClicked(MyGuiControlButton obj)
        {
            int memberLimit = MyMultiplayer.Static.MemberLimit;
            int memberCount = MyMultiplayer.Static.MemberCount;
            MyGameService.OpenInviteOverlay(Math.Max(1, memberLimit - memberCount));
        }

        protected void promoteButton_ButtonClicked(MyGuiControlButton obj)
        {
            MyGuiControlTable.Row selectedRow = m_playersTable.SelectedRow;
            if (selectedRow != null && MySession.Static.CanPromoteUser(Sync.MyId, (ulong)selectedRow.UserData))
            {
                MyMultiplayer.RaiseStaticEvent((IMyEventOwner x) => MyGuiScreenPlayers.Promote, (ulong)selectedRow.UserData, true);
            }
        }

        protected void demoteButton_ButtonClicked(MyGuiControlButton obj)
        {
            MyGuiControlTable.Row selectedRow = m_playersTable.SelectedRow;
            if (selectedRow != null && MySession.Static.CanDemoteUser(Sync.MyId, (ulong)selectedRow.UserData))
            {
                MyMultiplayer.RaiseStaticEvent((IMyEventOwner x) => MyGuiScreenPlayers.Promote, (ulong)selectedRow.UserData, false);
            }
        }

        protected void kickButton_ButtonClicked(MyGuiControlButton obj)
        {
            MyGuiControlTable.Row selectedRow = m_playersTable.SelectedRow;
            if (selectedRow != null)
            {
                MyMultiplayer.RaiseStaticEvent((IMyEventOwner x) => MyMultiplayerBase.KickUser, (ulong)selectedRow.UserData, arg3: true, arg4: true);
            }
        }

        protected void banButton_ButtonClicked(MyGuiControlButton obj)
        {
            MyGuiControlTable.Row selectedRow = m_playersTable.SelectedRow;
            if (selectedRow != null)
            {
                MyMultiplayer.RaiseStaticEvent((IMyEventOwner x) => MyMultiplayerBase.BanUser, (ulong)selectedRow.UserData, arg3: true);
            }
        }

        protected void lobbyTypeCombo_OnSelect()
        {
            MyLobbyType lobbyType = (MyLobbyType)m_lobbyTypeCombo.GetSelectedKey();
            m_lobbyTypeCombo.SelectItemByKey((long)MyMultiplayer.Static.GetLobbyType(), sendEvent: false);
            MyMultiplayer.Static.SetLobbyType(lobbyType);
        }

        protected void MaxPlayersSlider_Changed(MyGuiControlSlider control)
        {
            MySession.Static.Settings.MaxPlayers = (short)m_maxPlayersSlider.Value;
            MyMultiplayer.Static.SetMemberLimit(MySession.Static.MaxPlayers);
            m_maxPlayersValueLabel.Text = m_maxPlayersSlider.Value.ToString();
            UpdateCaption();
        }

        private void OnFactionCreated(long insertedId)
        {
            MyGuiScreenPlayers.RefreshPlusPings();
        }

        private void OnFactionEdited(long editedId)
        {
            MyGuiScreenPlayers.RefreshPlusPings();
        }

        private void OnFactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId)
        {
            MyGuiScreenPlayers.RefreshPlusPings();
        }

        private void OnUserPromoteLevelChanged(ulong steamId, MyPromoteLevel promotionLevel)
        {
            for (int i = 0; i < m_playersTable.RowsCount; i++)
            {
                MyGuiControlTable.Row row = m_playersTable.GetRow(i);
                if (row.UserData is ulong && (ulong)row.UserData == steamId)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    for (int j = 0; j < (int)promotionLevel; j++)
                    {
                        stringBuilder.Append("*");
                    }
                    MyGuiControlTable.Cell cell = row.GetCell(GameAdminColumn);
                    cell.Text.Clear();
                    cell.Text.Append(stringBuilder);
                    break;
                }
            }
            UpdateButtonsEnabledState();
            if (!m_promoteButton.Enabled && m_promoteButton.HasFocus)
            {
                if (m_demoteButton.Enabled)
                {
                    base.FocusedControl = m_demoteButton;
                }
            }
            else if (!m_demoteButton.Enabled && m_demoteButton.HasFocus && m_promoteButton.Enabled)
            {
                base.FocusedControl = m_promoteButton;
            }
        }

        public static void RefreshPlusPings()
        {
            Seamless.TryShow("Requesting Refresh Pings!");
            MyMultiplayer.RaiseStaticEvent((IMyEventOwner s) => MyGuiScreenPlayers.RequestPingsAndRefresh);
            
        }





        public override bool Draw()
        {
            bool result = base.Draw();

            if (m_getPingAndRefresh)
            {
                m_getPingAndRefresh = false;
                RefreshPlusPings();
            }

            return result;
        }

        private void RefreshMuteIcons()
        {
            m_lastMuteIndicatorsUpdate = 0;
        }

        public override bool Update(bool hasFocus)
        {
            MySessionComponentMatch component = MySession.Static.GetComponent<MySessionComponentMatch>();
            if (component.IsEnabled && m_warfareUpdate_frameCount_current >= m_warfareUpdate_frameCount)
            {
                m_warfareUpdate_frameCount_current = 0;
                foreach (MyGuiControlBase control in Controls)
                {
                    if (!(control.GetType() == typeof(MyGuiScreenPlayersWarfareTeamScoreTable)))
                    {
                        continue;
                    }
                    IMyFaction myFaction = MySession.Static.Factions.TryGetFactionById(((MyGuiScreenPlayersWarfareTeamScoreTable)control).FactionId);
                    if (myFaction != null && myFaction.FactionId == ((MyGuiScreenPlayersWarfareTeamScoreTable)control).FactionId)
                    {
                        MyMultiplayer.RaiseStaticEvent((IMyEventOwner s) => MyFactionCollection.RequestFactionScoreAndPercentageUpdate, myFaction.FactionId, MyEventContext.Current.Sender);
                    }
                }
            }
            m_warfareUpdate_frameCount_current++;
            if (component != null && component.IsEnabled)
            {
                TimeSpan timeSpan = TimeSpan.FromMinutes(component.RemainingMinutes);
                string text = timeSpan.ToString((timeSpan.TotalHours >= 1.0) ? "hh\\:mm\\:ss" : "mm\\:ss");
                if (m_warfare_timeRemainting_time.Text != text)
                {
                    m_warfare_timeRemainting_time.Text = text;
                }
            }
            foreach (MyGuiControlBase control2 in Controls)
            {
                if (control2.GetType() == typeof(MyGuiScreenPlayersWarfareTeamScoreTable))
                {
                    IMyFaction myFaction2 = MySession.Static.Factions.TryGetFactionById(((MyGuiScreenPlayersWarfareTeamScoreTable)control2).FactionId);
                    if (myFaction2 != null && myFaction2.FactionId == ((MyGuiScreenPlayersWarfareTeamScoreTable)control2).FactionId)
                    {
                        ((MyGuiScreenPlayersWarfareTeamScoreTable)control2).ScorePoints = myFaction2.Score;
                        ((MyGuiScreenPlayersWarfareTeamScoreTable)control2).ObjectiveFinishedPercentage = myFaction2.ObjectivePercentageCompleted;
                    }
                }
            }
            m_tradeButton.Visible = !MyInput.Static.IsJoystickLastUsed;
            m_profileButton.Visible = !MyInput.Static.IsJoystickLastUsed;
            if (MyGuiManager.TotalTimeInMilliseconds - m_lastMuteIndicatorsUpdate > 1000)
            {
                for (int i = 0; i < m_playersTable.RowsCount; i++)
                {
                    if (m_playersTable.GetRow(i).GetCell(PlayerMutedColumn).Control is MyGuiControlButton myGuiControlButton)
                    {
                        ulong playerId = (ulong)myGuiControlButton.UserData;
                        switch (MyGameService.GetPlayerMutedState(playerId))
                        {
                            case MyPlayerChatState.Silent:
                            case MyPlayerChatState.Talking:
                                myGuiControlButton.CustomStyle = ((MyVoiceChatSessionComponent.Static != null && MyVoiceChatSessionComponent.Static.PlayerTalking(playerId)) ? m_buttonSizeStyleTalking : m_buttonSizeStyleSilent);
                                break;
                            case MyPlayerChatState.Muted:
                                myGuiControlButton.CustomStyle = m_buttonSizeStyleMuted;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }
            return base.Update(hasFocus);
        }

    }
}
