using System.Collections.Generic;
using Aero.Gen;
using Aero.Gen.Attributes;
using Vortice.Direct3D11.Debug;

namespace PacketPeep.Systems.Tinker
{
    public class Entity
    {
        public ulong          Id;
        public string         Name;
        public List<Keyframe> Keyframes = new();
        public List<Message>  Messages  = new();
    }

    public class Keyframe
    {
        public Keyframe(byte id, IAero frame)
        {
            Id    = id;
            Frame = frame;
        }

        public Keyframe(byte id)
        {
            Id    = id;
            Frame = PacketParser.GetMessageFromIds(AeroMessageIdAttribute.MsgType.GSS, AeroMessageIdAttribute.MsgSrc.Message, 3, id);
            ;
        }

        public byte  Id;
        public IAero Frame;
    }

    public class Message
    {
        public Message(uint timestamp, byte controllerId, byte msgId, IAero msg)
        {
            Timestamp    = timestamp;
            Msg          = msg;
            ControllerId = controllerId;
            MsgId        = msgId;
        }

        public uint  Timestamp;
        public byte  ControllerId;
        public byte  MsgId;
        public IAero Msg;
    }
}