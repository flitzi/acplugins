﻿using acPlugins4net.configuration;
using acPlugins4net.helpers;
using acPlugins4net.messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace acPlugins4net
{
    public abstract class AcServerPlugin : MD5Hashable
    {
        private DuplexUDPClient _UDP = null;
        public IConfigManager Config { get; private set; }
        private WorkaroundHelper _Workarounds = null;
        private ILog _log = null;
        protected internal byte[] _fingerprint = null;

        #region Cache and Helpers

        public string Track { get; private set; }
        public string TrackLayout { get; private set; }
        public int MaxClients { get; private set; }

        private ConcurrentDictionary<int, MsgCarInfo> _CarInfo = null;
        public IDictionary<int, MsgCarInfo> CarInfo { get { return _CarInfo; } }


        #endregion

        public AcServerPlugin()
        {
            _log = new ConsoleLogger();
            Config = new AppConfigConfigurator();
            _Workarounds = new WorkaroundHelper(Config);
            _CarInfo = new ConcurrentDictionary<int, MsgCarInfo>(10, 64);
            _fingerprint = Hash(Config.GetSetting("ac_server_directory") + Config.GetSetting("acServer_port"));
        }

        public void RunUntilAborted()
        {
            _log.Log("AcServerPlugin starting up...");
            Init();
            _log.Log("Initialized, start UDP connection...");
            Connect();
            _log.Log("... ok, we're good to go.");

            var input = Console.ReadLine();
            while (input != "x" && input != "exit")
            {
                // Basically we're blocking the Main Thread until exit.
                // Ugly, but pretty easy to use by the deriving Plugin

                // To have a bit of functionality we'll let the server admin 
                // type in commands that can be understood by the deriving plugin
                if(!string.IsNullOrEmpty(input))
                    OnConsoleCommand(input);

                input = Console.ReadLine();
            }
        }

        private void Init()
        {
#if DEBUG
            Track = "mugello";
            TrackLayout = "mugello";
            MaxClients = 24;
#else
            Track = _Workarounds.FindServerConfigEntry("TRACK=");
            TrackLayout = _Workarounds.FindServerConfigEntry("CONFIG_TRACK=");
            _log.Log("Track/Layout is " + Track + "[" + TrackLayout + "] (by workaround)");
            CarCount = _Workarounds.FindServerConfigEntry("MAX_CLIENTS=");
#endif
            OnInit();
        }

        private void Connect()
        {
            // First we're getting the configured ports (app.config)
            var acServerPort = Config.GetSettingAsInt("acServer_port");
            var pluginPort = Config.GetSettingAsInt("plugin_port");
            
            _UDP = new DuplexUDPClient();
            _UDP.Open(pluginPort, acServerPort, MessageReceived, OnError);
        }

        protected virtual void OnError(Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }

        private void MessageReceived(byte[] data)
        {
            AcMessageParser.Activate(this, data);
        }

        #region base event handlers - usually a call of base.EventHandler(), but this one is more secure

        internal void OnNewSessionBase(MsgNewSession msg)
        {
            // If we're empty on a NewSession, we'll need to ask for all current car definitions
            if(CarInfo.Count == 0)
            {
                for (byte i = 0; i < MaxClients; i++)
                {
                    _UDP.TrySend(new RequestCarInfo() { CarId = i }.ToBinary());
                }
            }
            OnNewSession(msg);
        }

        internal void OnCarInfoBase(MsgCarInfo msg)
        {
            _CarInfo.AddOrUpdate(msg.CarId, msg, (key,val) => val);
            OnCarInfo(msg);
        }

        internal void OnNewConnectionBase(MsgNewConnection msg)
        {
            var carInfo = new MsgCarInfo()
            {
               CarId = msg.CarId,
               CarModel = msg.CarModel,
               CarSkin = msg.CarSkin, 
               DriverGuid = msg.DriverGuid,
               DriverName = msg.DriverName,
               IsConnected = true,
            };
            _CarInfo.AddOrUpdate(msg.CarId, carInfo, (key, val) => val);
            OnNewConnection(msg);
        }

        internal void OnConnectionClosedBase(MsgConnectionClosed msg)
        {
            var carInfo = new MsgCarInfo()
            {
                CarId = msg.CarId,
                CarModel = msg.CarModel,
                CarSkin = msg.CarSkin,
                DriverGuid = string.Empty,
                DriverName = string.Empty,
                IsConnected = false,
            };
            _CarInfo.AddOrUpdate(msg.CarId, carInfo, (key, val) => val);
            OnConnectionClosed(msg);
        }

        #endregion

        #region overridable event handlers

        public virtual void OnInit() { }
        public virtual void OnConsoleCommand(string cmd) { }
        public virtual void OnNewSession(MsgNewSession msg) { }
        public virtual void OnConnectionClosed(MsgConnectionClosed msg) { }
        public virtual void OnNewConnection(MsgNewConnection msg) { }
        public virtual void OnCarInfo(MsgCarInfo msg) { }
        public virtual void OnCarUpdate(MsgCarUpdate msg) { }
        public virtual void OnLapCompleted(MsgLapCompleted msg) { }
        public virtual void OnCollision(MsgClientEvent msg) { }

        #endregion

        #region Requests to the AcServer

        protected internal void BroadcastChatMessage(string msg)
        {
            var chatRequest = new RequestBroadcastChat() { ChatMessage = msg };
            _UDP.TrySend(chatRequest.ToBinary());
            Console.WriteLine("Broadcasted " + chatRequest.ToString());
        }

        protected internal void SendChatMessage(byte car_id, string msg)
        {
            var chatRequest = new RequestSendChat() { CarId = car_id, ChatMessage = msg };
            _UDP.TrySend(chatRequest.ToBinary());
            Console.WriteLine("Broadcasted " + chatRequest.ToString());
        }

        #endregion
    }
}