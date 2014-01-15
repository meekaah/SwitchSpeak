using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Windows;
using TS3QueryLib.Core;
using TS3QueryLib.Core.Client;
using TS3QueryLib.Core.Client.Entities;
using TS3QueryLib.Core.Client.Notification.EventArgs;
using TS3QueryLib.Core.Client.Responses;
using TS3QueryLib.Core.Common;
using TS3QueryLib.Core.Common.Responses;
using TS3QueryLib.Core.Communication;
using Sharparam.SharpBlade.Razer;
using System.Threading;
using System.Windows.Controls;
using Sharparam.SharpBlade.Razer.Events;
using Sharparam.SharpBlade.Native;
using System.Windows.Media;
using System.Diagnostics;

namespace SwitchSpeak
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AsyncTcpDispatcher AsyncQueryDispatcher;
        private SyncTcpDispatcher SyncQueryDispatcher;
        private TS3QueryLib.Core.Client.QueryRunner AsyncClientQueryRunner;
        private TS3QueryLib.Core.Server.QueryRunner AsyncServerQueryRunner;
        private QueryRunner SyncQueryRunner;
        private WhoAmIResponse me;
        private ObservableCollection<ChannelListEntry> Channels;

        private DragScrollViewerAdaptor scrollViewerAdaptor;
        private ScrollViewer scroller;

        public MainWindow()
        {
            InitializeComponent();
            TextWriterTraceListener td = new TextWriterTraceListener("debug.log");
            Debug.Listeners.Add(td);
            System.AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            RazerProvider.Razer.Touchpad.Gesture += TouchpadOnGesture;
            RazerProvider.Razer.Touchpad.EnableGesture(RazerAPI.GestureType.Tap);
            RazerProvider.Razer.EnableDynamicKey(RazerAPI.DynamicKeyType.DK10, (s, x) => Connect(), @"Default\refresh.png", @"Default\refresh.png", true);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine(((Exception)e.ExceptionObject));
        }

        private void TouchpadOnGesture(object sender, GestureEventArgs args)
        {
            var pos = new Point(args.X, args.Y);

            switch (args.GestureType)
            {
                case RazerAPI.GestureType.Move:
                    scrollViewerAdaptor.MouseMove(pos);
                    break;
                case RazerAPI.GestureType.Press:
                    scrollViewerAdaptor.MouseLeftButtonDown(pos);
                    break;
                case RazerAPI.GestureType.Release:
                    scrollViewerAdaptor.MouseLeftButtonUp();
                    break;
                case RazerAPI.GestureType.Tap:
                    HitTestResult result = VisualTreeHelper.HitTest(treeSpeak, pos);
                    if (result != null)
                    {
                        TreeViewItem tvi = result.VisualHit.FindParent<TreeViewItem>();
                        if (tvi != null)
                        {
                            if (tvi.DataContext is TS3QueryLib.Core.Server.Entities.ClientListEntry)
                            {
                                //we tapped on a client
                                var client = (TS3QueryLib.Core.Server.Entities.ClientListEntry)tvi.DataContext;
                                Debug.WriteLine(string.Format("Tapped on client : {0}", client.Nickname));
                            }
                            if (tvi.DataContext is ChannelListEntry)
                            {
                                //we tapped on a channel
                                var channel = (ChannelListEntry)tvi.DataContext;
                                Debug.WriteLine(string.Format("Tapped on channel : {0}", channel.Name));
                                AsyncServerQueryRunner.MoveClient(me.ClientId, channel.ChannelId);
                            }
                        }
                    }
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowHelper.ExtendWindowStyleWithTool(this);
            scroller = (ScrollViewer)treeSpeak.Template.FindName("Scroller", treeSpeak);
            scrollViewerAdaptor = new DragScrollViewerAdaptor(scroller);

            RazerProvider.Razer.Touchpad.SetWindow(this, Touchpad.RenderMethod.Polling, new TimeSpan(0, 0, 0, 0, 42));

            Connect();
        }

        private void Connect()
        {
            try
            {
                Disconnect();

                SyncQueryDispatcher = new SyncTcpDispatcher("localhost", 25639);
                SyncQueryRunner = new QueryRunner(SyncQueryDispatcher);

                me = SyncQueryRunner.SendWhoAmI();
                if (me.IsErroneous)
                {
                    Debug.WriteLine("Not a TS3 client!");
                    Disconnect();
                    return;
                }

                GetChannelList();
                treeSpeak.ItemsSource = Channels;

                AsyncQueryDispatcher = new AsyncTcpDispatcher("localhost", 25639);
                AsyncQueryDispatcher.BanDetected += QueryDispatcher_BanDetected;
                AsyncQueryDispatcher.ReadyForSendingCommands += QueryDispatcher_ReadyForSendingCommands;
                AsyncQueryDispatcher.ServerClosedConnection += QueryDispatcher_ServerClosedConnection;
                AsyncQueryDispatcher.SocketError += QueryDispatcher_SocketError;
                AsyncQueryDispatcher.NotificationReceived += QueryDispatcher_NotificationReceived;
                AsyncQueryDispatcher.Connect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Disconnect();
            }
        }

        private void QueryDispatcher_ReadyForSendingCommands(object sender, System.EventArgs e)
        {
            // you can only run commands on the queryrunner when this event has been raised first!
            AsyncClientQueryRunner = new TS3QueryLib.Core.Client.QueryRunner(AsyncQueryDispatcher);
            AsyncClientQueryRunner.Notifications.ChannelTalkStatusChanged += Notifications_ChannelTalkStatusChanged;
            AsyncClientQueryRunner.RegisterForNotifications(ClientNotifyRegisterEvent.Any);

            AsyncServerQueryRunner = new TS3QueryLib.Core.Server.QueryRunner(AsyncQueryDispatcher);
            AsyncServerQueryRunner.Notifications.ClientConnectionLost += Notifications_ClientConnectionLost;
            AsyncServerQueryRunner.Notifications.ClientDisconnect += Notifications_ClientDisconnect;
            AsyncServerQueryRunner.Notifications.ClientJoined += Notifications_ClientJoined;
            AsyncServerQueryRunner.Notifications.ClientKick += Notifications_ClientKick;
            AsyncServerQueryRunner.Notifications.ClientMoved += Notifications_ClientMoved;
            AsyncServerQueryRunner.Notifications.ClientMovedByTemporaryChannelCreate += Notifications_ClientMovedByTemporaryChannelCreate;
            AsyncServerQueryRunner.Notifications.ClientMoveForced += Notifications_ClientMoveForced;
            AsyncServerQueryRunner.RegisterForNotifications(TS3QueryLib.Core.Server.Entities.ServerNotifyRegisterEvent.Server | TS3QueryLib.Core.Server.Entities.ServerNotifyRegisterEvent.Channel);

            GetClientList();
        }

        private void GetChannelList()
        {
            var channels = SyncQueryRunner.GetChannelList(true);
            if (channels.IsErroneous)
                throw new Exception("Unable to get the list of channels");

            for (int i = channels.Values.Count - 1; i >= 0; i--)
            {
                if (channels.Values[i].ParentChannelId != 0)
                {
                    for (int j = channels.Values.Count - 1; j >= 0; j--)
                    {
                        if (channels.Values[j].ChannelId == channels.Values[i].ParentChannelId)
                        {
                            //we found the parent, we need to remove the child from list and add it to the parent
                            var channel = channels.Values[i];
                            channels.Values.RemoveAt(i);
                            channels.Values[j].Subchannels.Add(channel);
                            channels.Values[j].Subchannels = new ObservableCollection<ChannelListEntry>(channels.Values[j].Subchannels.OrderBy(c => c.Order));
                            break;
                        }
                    }
                }
            }

            Channels = new ObservableCollection<ChannelListEntry>(channels.Values.OrderBy(c => c.Order));
        }

        private void GetClientList()
        {
            var clients = AsyncServerQueryRunner.GetClientList(true);
            if (clients.IsErroneous)
                throw new Exception("Unable to get the list of clients");

            foreach (var channel in Channels)
            {
                foreach (var client in clients.Values)
                {
                    if (client.ClientId == me.ClientId)
                    {
                        client.IsMe = true;
                        me.ChannelId = client.ChannelId;
                    }

                    if (client.ChannelId == channel.ChannelId)
                    {
                        channel.Clients.Add(client);
                        continue;
                    }

                    foreach (var subchannel in channel.Subchannels)
                    {
                        if (client.ChannelId == subchannel.ChannelId)
                        {
                            subchannel.Clients.Add(client);
                            break;
                        }
                    }
                }
            }
        }

        private void RefreshChannelsAndClients()
        {
            Debug.WriteLine("Refreshing channel and client list");
            try
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    GetChannelList();
                    GetClientList();
                    treeSpeak.ItemsSource = Channels;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("An error occured while refreshing channels and clients: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        private void Notifications_ClientMoveForced(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientMovedByClientEventArgs e)
        {
            Debug.WriteLine("Client move forced");
            RefreshChannelsAndClients();
        }

        private void Notifications_ClientMovedByTemporaryChannelCreate(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientMovedEventArgs e)
        {
            Debug.WriteLine("Client move to temp channel");
            RefreshChannelsAndClients();
        }

        private void Notifications_ClientMoved(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientMovedEventArgs e)
        {
            Debug.WriteLine("Client move");
            RefreshChannelsAndClients();
        }

        private void Notifications_ClientKick(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientKickEventArgs e)
        {
            Debug.WriteLine("Client kick");
            if (e.VictimClientId == me.ClientId)
            {
                Disconnect();
                return;
            }

            RefreshChannelsAndClients();
        }

        private void Notifications_ClientJoined(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientJoinedEventArgs e)
        {
            Debug.WriteLine("Client join");
            RefreshChannelsAndClients();
        }

        private void Notifications_ClientDisconnect(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientDisconnectEventArgs e)
        {
            Debug.WriteLine("Client disconnect");
            if (e.ClientId == me.ClientId)
            {
                Disconnect();
                return;
            }

            RefreshChannelsAndClients();
        }

        private void Notifications_ClientConnectionLost(object sender, TS3QueryLib.Core.Server.Notification.EventArgs.ClientConnectionLostEventArgs e)
        {
            Debug.WriteLine("Client connection lost");
            if (e.ClientId == me.ClientId)
            {
                Disconnect();
                return;
            }

            RefreshChannelsAndClients();
        }

        private void QueryDispatcher_NotificationReceived(object sender, EventArgs<string> e)
        {
            Debug.WriteLine(e.Value);
        }

        private void Notifications_ChannelTalkStatusChanged(object sender, TalkStatusEventArgsBase e)
        {
            UpdateClientTalkStatus(Channels, e.ClientId, e.TalkStatus);
        }

        private void QueryDispatcher_ServerClosedConnection(object sender, System.EventArgs e)
        {
            // this event is raised when the connection to the server is lost.
            Debug.WriteLine("Connection to server closed/lost.");

            // dispose
            Connect();
        }

        private void QueryDispatcher_BanDetected(object sender, EventArgs<SimpleResponse> e)
        {
            Debug.WriteLine(string.Format("You're account was banned!\nError-Message: {0}\nBan-Message:{1}", e.Value.ErrorMessage, e.Value.BanExtraMessage));

            // force disconnect
            Disconnect();
        }

        private void QueryDispatcher_SocketError(object sender, SocketErrorEventArgs e)
        {
            // do not handle connection lost errors because they are already handled by QueryDispatcher_ServerClosedConnection
            if (e.SocketError == SocketError.ConnectionReset)
                return;

            // this event is raised when a socket exception has occured
            Debug.WriteLine("Socket error!! Error Code: " + e.SocketError);

            // force disconnect
            Connect();
        }

        public void Disconnect()
        {
            Channels = new ObservableCollection<ChannelListEntry>();
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                treeSpeak.ItemsSource = null;
            });
            me = null;

            if (SyncQueryDispatcher != null)
            {
                SyncQueryDispatcher.Dispose();
                SyncQueryDispatcher = null;
            }

            if (SyncQueryRunner != null)
            {
                SyncQueryRunner.Dispose();
                SyncQueryRunner = null;
            }

            if (AsyncClientQueryRunner != null)
                AsyncClientQueryRunner.Notifications.ChannelTalkStatusChanged -= Notifications_ChannelTalkStatusChanged;
            if (AsyncClientQueryRunner != null)
                AsyncClientQueryRunner.Dispose();
            if (AsyncClientQueryRunner != null)
                AsyncClientQueryRunner = null;
            
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientConnectionLost -= Notifications_ClientConnectionLost;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientDisconnect -= Notifications_ClientDisconnect;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientJoined -= Notifications_ClientJoined;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientKick -= Notifications_ClientKick;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientMoved -= Notifications_ClientMoved;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientMovedByTemporaryChannelCreate -= Notifications_ClientMovedByTemporaryChannelCreate;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Notifications.ClientMoveForced -= Notifications_ClientMoveForced;
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner.Dispose();
            if (AsyncServerQueryRunner != null)
                AsyncServerQueryRunner = null;
            

            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.BanDetected -= QueryDispatcher_BanDetected;
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.ReadyForSendingCommands -= QueryDispatcher_ReadyForSendingCommands;
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.ServerClosedConnection -= QueryDispatcher_ServerClosedConnection;
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.SocketError -= QueryDispatcher_SocketError;
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.NotificationReceived -= QueryDispatcher_NotificationReceived;
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher.Dispose();
            if (AsyncQueryDispatcher != null)
                AsyncQueryDispatcher = null;
            
        }

        private void UpdateClientTalkStatus(ObservableCollection<ChannelListEntry> channels, uint clientId, TS3QueryLib.Core.Client.Notification.Enums.TalkStatus talkStatus)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].Clients.Any(c => c.ClientId == clientId))
                {
                    //we found our client that we need to change
                    for (int j = 0; j < channels[i].Clients.Count; j++)
                    {
                        if (channels[i].Clients[j].ClientId == clientId)
                        {
                            channels[i].Clients[j].IsClientTalking = talkStatus == TS3QueryLib.Core.Client.Notification.Enums.TalkStatus.TalkStarted;
                            return;
                        }
                    }
                }
                else
                {
                    //keep looking in subchannels until we find what we need
                    UpdateClientTalkStatus(channels[i].Subchannels, clientId, talkStatus);
                }
            }

            return;
        }
    }

}
