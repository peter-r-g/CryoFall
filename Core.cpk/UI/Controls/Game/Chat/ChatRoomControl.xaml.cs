﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.Chat
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AtomicTorch.CBND.CoreMod.Helpers;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Chat;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Chat.Data;
    using AtomicTorch.CBND.CoreMod.UI.Services;
    using AtomicTorch.CBND.GameApi.Data.Logic;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;
    using AtomicTorch.GameEngine.Common.Extensions;

    public partial class ChatRoomControl : BaseUserControl
    {
        public const double DelayOnChatCloseSeconds = 1.5;

        private const double DefaultChatHistoryInitialHideDelaySeconds = 8.0;

        private const double DefaultNewEntryHideDelaySeconds = 15.0;

        private const int MaxChatEntriesCount = 120;

        private static readonly SoundResource SoundResourceActivity
            = new SoundResource("UI/Chat/Activity");

        private static readonly SoundResource SoundResourceGenericMessageReceived
            = new SoundResource("UI/Chat/Received");

        private static readonly SoundResource SoundResourceMessageSend
            = new SoundResource("UI/Chat/Send");

        private static readonly SoundResource SoundResourcePrivateMessageReceived
            = new SoundResource("UI/Chat/Private");

        private static uint lastMessageReceivedSoundPlayerFrameNumber;

        private uint activatedOnFrameNumber;

        private bool? isActive;

        private bool isExpanded;

        private ScrollViewer scrollViewerChatLog;

        private UIElementCollection stackPanelChatLogChildren;

        private TextBox textBoxChatInput;

        private ViewModelChatRoom viewModelChatRoom;

        public ChatPanel ChatPanel { get; set; }

        public bool IsActive
        {
            get => this.isActive ?? false;
            set
            {
                if (this.isActive == value)
                {
                    return;
                }

                this.isActive = value;

                if (this.isActive.Value)
                {
                    this.activatedOnFrameNumber = Api.Client.CurrentGame.ServerFrameNumber;
                    this.SetIsExpanded(true);
                    this.ShowEntries();

                    this.textBoxChatInput.Visibility = Visibility.Visible;
                    this.textBoxChatInput.Focusable = true;

                    this.scrollViewerChatLog.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    this.scrollViewerChatLog.Focusable = true;

                    this.ScrollToBottom(force: true);

                    this.textBoxChatInput.KeyDown += this.InputKeyDownHandler;
                    this.textBoxChatInput.PreviewTextInput += this.InputPreviewTextInputHandler;
                    this.textBoxChatInput.PreviewKeyDown += this.InputPreviewKeyDownHandler;
                    this.scrollViewerChatLog.MouseLeftButtonUp += this.ScrollViewerChatLogMouseLeftButtonUpHandler;

                    this.viewModelChatRoom.IsOpened = true;

                    if (this.viewModelChatRoom.IsSelected)
                    {
                        this.FocusInput();
                    }
                }
                else
                {
                    // not active
                    this.SetIsExpanded(false);
                    this.textBoxChatInput.Visibility = Visibility.Hidden;
                    this.textBoxChatInput.Focusable = false;

                    this.HideEntries();

                    this.scrollViewerChatLog.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    this.scrollViewerChatLog.Focusable = false;

                    Api.Client.UI.BlurFocus();

                    this.textBoxChatInput.KeyDown -= this.InputKeyDownHandler;
                    this.textBoxChatInput.PreviewTextInput -= this.InputPreviewTextInputHandler;
                    this.textBoxChatInput.PreviewKeyDown -= this.InputPreviewKeyDownHandler;
                    this.scrollViewerChatLog.MouseLeftButtonUp -= this.ScrollViewerChatLogMouseLeftButtonUpHandler;

                    this.viewModelChatRoom.IsOpened = false;
                }
            }
        }

        public ViewModelChatRoom ViewModelChatRoom
        {
            get => this.viewModelChatRoom;
            set
            {
                if (this.viewModelChatRoom != null)
                {
                    throw new InvalidOperationException();
                }

                this.viewModelChatRoom = value;
                this.PopulateEntriesFromRoomLog();

                this.viewModelChatRoom.SubscribePropertyChange(
                    _ => _.IsSelected,
                    isSelected =>
                    {
                        if (isSelected && this.IsActive)
                        {
                            this.FocusInput();
                        }
                    });
            }
        }

        public void AddMention(string from)
        {
            if (string.IsNullOrEmpty(from))
            {
                return;
            }

            this.IsActive = true;

            from = "@" + from + ", ";
            if (this.textBoxChatInput.Text.Length == 0)
            {
                this.textBoxChatInput.Text = from;
                this.textBoxChatInput.CaretIndex = from.Length;
            }
            else
            {
                var insertPosition = this.textBoxChatInput.CaretIndex;
                this.textBoxChatInput.Text = this.textBoxChatInput.Text.Insert(insertPosition, " " + from);
                this.textBoxChatInput.CaretIndex = insertPosition + 1 + from.Length;
            }
        }

        public void FocusInput()
        {
            // set focus on the next frame
            ClientTimersSystem.AddAction(
                0,
                () =>
                {
                    if (this.IsActive
                        && this.viewModelChatRoom.IsSelected)
                    {
                        this.textBoxChatInput.Focus();
                        Keyboard.Focus(this.textBoxChatInput);
                    }
                });
        }

        protected override void InitControl()
        {
            this.textBoxChatInput = this.GetByName<TextBox>("TextBoxChatInput");
            this.textBoxChatInput.Visibility = Visibility.Hidden;
            this.textBoxChatInput.Focusable = false;

            this.scrollViewerChatLog = this.GetByName<ScrollViewer>("ScrollViewerChatLog");
            this.stackPanelChatLogChildren = this.GetByName<StackPanel>("StackPanelChatLog").Children;

            this.LimitScrollViewerHeight();
        }

        protected override void OnLoaded()
        {
            if (this.IsActive)
            {
                this.SetIsExpanded(true);
            }

            ChatSystem.ClientChatRoomMessageReceived += this.ClientChatRoomMessageReceivedHandler;
            ClientChatBlockList.CharacterBlockStatusChanged += this.CharacterBlockStatusChangedHandler;
            this.PopulateEntriesFromRoomLog();
        }

        protected override void OnUnloaded()
        {
            ChatSystem.ClientChatRoomMessageReceived -= this.ClientChatRoomMessageReceivedHandler;
            ClientChatBlockList.CharacterBlockStatusChanged -= this.CharacterBlockStatusChangedHandler;
        }

        private void AddChatEntry(ChatEntry chatEntry)
        {
            var chatEntryControl = this.CreateChatEntryControl(chatEntry);

            this.stackPanelChatLogChildren.Add(chatEntryControl);
            if (this.stackPanelChatLogChildren.Count > MaxChatEntriesCount)
            {
                this.stackPanelChatLogChildren.RemoveAt(0);
            }

            this.ScrollToBottom(force: false);

            if (this.isLoaded
                && !this.isExpanded)
            {
                chatEntryControl.Hide(delaySeconds: DefaultNewEntryHideDelaySeconds);
            }
        }

        private void CharacterBlockStatusChangedHandler((string name, bool isBlocked) blockedInfo)
        {
            if (!blockedInfo.isBlocked)
            {
                // unblocked
                return;
            }

            // blocked - find and remove chat entries from this player
            var blockedName = blockedInfo.name;

            for (var index = 0; index < this.stackPanelChatLogChildren.Count; index++)
            {
                var entry = (ChatEntryControl)this.stackPanelChatLogChildren[index];
                var chatEntryFrom = entry.ViewModel.ChatEntry.From;

                if (blockedName.Equals(chatEntryFrom))
                {
                    // found a chat entry from this user, remove it
                    this.stackPanelChatLogChildren.RemoveAt(index--);
                }
            }
        }

        private void ClientChatRoomMessageReceivedHandler(BaseChatRoom chatRoom, in ChatEntry chatEntry)
        {
            if (this.viewModelChatRoom.ChatRoom != chatRoom)
            {
                // addressed to different chat room
                return;
            }

            this.AddChatEntry(chatEntry);
            this.ClientPlaySoundMessageReceived(chatEntry);
        }

        private void ClientPlaySoundMessageReceived(in ChatEntry chatEntry)
        {
            if (!this.ViewModelChatRoom.IsSelected)
            {
                return;
            }

            var frameNumber = Api.Client.CurrentGame.ServerFrameNumber;
            if (lastMessageReceivedSoundPlayerFrameNumber == frameNumber)
            {
                return;
            }

            lastMessageReceivedSoundPlayerFrameNumber = frameNumber;

            Api.Client.Audio.PlayOneShot(this.GetMessageReceivedSound(chatEntry),
                                         volume: SoundConstants.VolumeUIChat);
        }

        private ChatEntryControl CreateChatEntryControl(ChatEntry chatEntry)
        {
            var control = new ChatEntryControl();
            control.Setup(this, chatEntry);
            return control;
        }

        private SoundResource GetMessageReceivedSound(ChatEntry chatEntry)
        {
            if (chatEntry.IsService)
            {
                return SoundResourceActivity;
            }

            return this.viewModelChatRoom.ChatRoom is ChatRoomPrivate
                       ? SoundResourcePrivateMessageReceived
                       : SoundResourceGenericMessageReceived;
        }

        private void HideEntries()
        {
            if (!this.isLoaded
                || this.IsActive
                || this.isExpanded)
            {
                return;
            }

            var count = this.stackPanelChatLogChildren.Count;

            for (var i = 0; i < count; i++)
            {
                var entry = (ChatEntryControl)this.stackPanelChatLogChildren[i];
                entry.Hide(delaySeconds: DelayOnChatCloseSeconds);
            }
        }

        private void InputKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (this.activatedOnFrameNumber == Api.Client.CurrentGame.ServerFrameNumber)
            {
                // ignore for now
                return;
            }

            if (e.Key.IsModifier()
                || e.Key == Key.PrintScreen)
            {
                // ignore
                return;
            }

            this.ScrollToBottom(force: true);

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    var message = this.textBoxChatInput.Text;
                    this.SendMessage(message);
                    this.textBoxChatInput.Text = string.Empty;

                    this.IsActive = false;
                    this.ChatPanel.Close();
                    break;

                case Key.Escape:
                    e.Handled = true;
                    this.IsActive = false;
                    this.ChatPanel.Close();
                    break;
            }
        }

        private void InputPreviewKeyDownHandler(object sender, KeyEventArgs e)
        {
            var isCtrlHeld = Api.Client.Input.IsKeyHeld(InputKey.Control, evenIfHandled: true);
            switch (e.Key)
            {
                case Key.Up:
                    if (!isCtrlHeld)
                    {
                        return;
                    }

                    e.Handled = true;
                    this.ChatPanel.SelectPreviousTab();
                    break;

                case Key.Down:
                    if (!isCtrlHeld)
                    {
                        return;
                    }

                    e.Handled = true;
                    this.ChatPanel.SelectNextTab();
                    break;

                case Key.Tab:
                    e.Handled = true;
                    if (!isCtrlHeld)
                    {
                        return;
                    }

                    if (Api.Client.Input.IsKeyHeld(InputKey.Shift, evenIfHandled: true))
                    {
                        this.ChatPanel.SelectPreviousTab();
                    }
                    else
                    {
                        this.ChatPanel.SelectNextTab();
                    }

                    break;
            }
        }

        private void InputPreviewTextInputHandler(object sender, TextCompositionEventArgs e)
        {
            if (this.textBoxChatInput.Text.Length != 0)
            {
                return;
            }

            switch (e.Text)
            {
                case "!":
                    e.Handled = true;
                    this.ChatPanel.SelectTab<ChatRoomLocal>();
                    break;

                case "$":
                    e.Handled = true;
                    this.ChatPanel.SelectTab<ChatRoomGlobal>();
                    break;

                case "#":
                    this.ChatPanel.SelectTab<ChatRoomParty>();
                    e.Handled = true;
                    break;
            }
        }

        private void LimitScrollViewerHeight()
        {
            this.scrollViewerChatLog.MaxHeight = 110;
        }

        private void PopulateEntriesFromRoomLog()
        {
            if (!this.isLoaded
                || this.viewModelChatRoom == null)
            {
                return;
            }

            this.stackPanelChatLogChildren.Clear();
            var chatEntries = ChatSystem.SharedGetChatRoom((ILogicObject)this.viewModelChatRoom.ChatRoom.GameObject)
                                        .ChatLog
                                        .ToList();

            chatEntries.RemoveAll(e => ClientChatBlockList.IsBlocked(e.From));

            if (chatEntries.Count == 0)
            {
                return;
            }

            chatEntries.Reverse();
            foreach (var chatEntry in chatEntries)
            {
                if (this.stackPanelChatLogChildren.Count > MaxChatEntriesCount)
                {
                    break;
                }

                var chatEntryControl = this.CreateChatEntryControl(chatEntry);
                this.stackPanelChatLogChildren.Insert(0, chatEntryControl);

                if (!this.isExpanded)
                {
                    chatEntryControl.Hide(delaySeconds: DefaultChatHistoryInitialHideDelaySeconds);
                }
            }

            this.ScrollToBottom(force: true);
        }

        private void ScrollToBottom(bool force)
        {
            this.scrollViewerChatLog.UpdateLayout();
            AdvancedScrollViewerService.ScrollToBottom(this.scrollViewerChatLog, force);
        }

        private void ScrollViewerChatLogMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
        {
            this.FocusInput();
        }

        private void SendMessage(string message)
        {
            message = message.TrimNewLinesAndSpaces();
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Api.Client.Audio
               .PlayOneShot(SoundResourceMessageSend,
                            volume: SoundConstants.VolumeUIChat);

            this.AddChatEntry(new ChatEntry(ClientCurrentCharacterHelper.Character.Name,
                                            message,
                                            isService: false,
                                            DateTime.Now,
                                            hasSupporterPack: Api.Client.MasterServer.IsSupporterPackOwner));

            ChatSystem.ClientSendMessageToRoom(this.ViewModelChatRoom.ChatRoom, message);
        }

        private void SetIsExpanded(bool value)
        {
            if (!this.isLoaded)
            {
                return;
            }

            if (!value
                && this.scrollViewerChatLog.IsFocused)
            {
                value = true;
            }

            if (this.isExpanded == value)
            {
                return;
            }

            this.isExpanded = value;

            if (this.isExpanded)
            {
                this.scrollViewerChatLog.ClearValue(MaxHeightProperty);
                this.ShowEntries();
            }
            else
            {
                this.LimitScrollViewerHeight();
                this.ScrollToBottom(force: true);
                this.HideEntries();
            }

            this.IsHitTestVisible = this.isExpanded;
            this.scrollViewerChatLog.IsHitTestVisible = this.isExpanded;

            foreach (ChatEntryControl entry in this.stackPanelChatLogChildren)
            {
                entry.IsHitTestVisible = this.isExpanded;
            }
        }

        private void ShowEntries()
        {
            for (var i = 0; i < this.stackPanelChatLogChildren.Count; i++)
            {
                var entry = this.stackPanelChatLogChildren[i] as ChatEntryControl;
                entry?.Show();
            }
        }
    }
}