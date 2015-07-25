﻿using acPlugins4net.kunos;
using acPlugins4net.messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace acPlugins4net.helpers
{
    public class AcMessageParser
    {
        public static PluginMessage Parse(byte[] rawData)
        {
            if (rawData == null)
                throw new ArgumentNullException("rawData");
            if (rawData.Length == 0)
                throw new ArgumentException("rawData is empty");

            ACSProtocol.MessageType msgType;
            try
            {
                msgType = (ACSProtocol.MessageType)rawData[0];
            }
            catch (Exception)
            {
                throw new Exception("Message contains unknown/not implemented Type-Byte '" + rawData[0] + "'");
            }

            PluginMessage newMsg = CreateInstance(msgType);
            using (var m = new MemoryStream(rawData))
            using (var br= new BinaryReader(m))
            {
                newMsg.Deserialize(br);
            }

            return newMsg;
        }

        private static PluginMessage CreateInstance(ACSProtocol.MessageType msgType)
        {
            switch (msgType)
            {
                case ACSProtocol.MessageType.ACSP_NEW_SESSION: return new MsgNewSession();
                case ACSProtocol.MessageType.ACSP_NEW_CONNECTION: return new MsgNewConnection();
                case ACSProtocol.MessageType.ACSP_CONNECTION_CLOSED: return new MsgConnectionClosed();
                case ACSProtocol.MessageType.ACSP_CAR_UPDATE: return new MsgCarUpdate();
                case ACSProtocol.MessageType.ACSP_CAR_INFO: return new MsgCarInfo();
                case ACSProtocol.MessageType.ACSP_LAP_COMPLETED: return new MsgLapCompleted();
                case ACSProtocol.MessageType.ACSP_CLIENT_EVENT: return new MsgClientEvent();
                case ACSProtocol.MessageType.ACSP_REALTIMEPOS_INTERVAL: return new RequestRealtimeInfo();
                case ACSProtocol.MessageType.ACSP_GET_CAR_INFO: return new RequestCarInfo();
                case ACSProtocol.MessageType.ACSP_SEND_CHAT: return new RequestSendChat();
                case ACSProtocol.MessageType.ACSP_BROADCAST_CHAT: return new RequestBroadcastChat();
                case ACSProtocol.MessageType.ERROR:
                    throw new Exception("CreateInstance: MessageType is not set or wrong (ERROR=0)");
                case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR:
                case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_ENV:
                    throw new Exception("CreateInstance: MessageType " + msgType + " is not meant to be used as MessageType, but as Subtype to ACSP_CLIENT_EVENT");
                default:
                    throw new Exception("MessageType " + msgType + " is not known or implemented");
            }
        }
    }
}