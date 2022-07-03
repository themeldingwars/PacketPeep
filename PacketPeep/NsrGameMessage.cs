using System;
using FauCap;

namespace PacketPeep;

public class NsrGameMessage : GameMessage
{
    public NsrGameMessage(int id, GamePacket packet) : base(id, packet)
    {
    }

    public NsrGameMessage(int id, GamePacket[] packets) : base(id, packets)
    {
    }

    public NsrGameMessage(int id) : base(id)
    {
    }


    public byte[] MainData;

    public override Channel    Channel     => Channel.ReliableGss;
    public override bool       IsSplit     => false;
    public override bool       IsReliable  => true;
    public override bool       IsSequenced => true;
    public override Server     Server      => Server.Game;
    public override bool       FromServer  => true;
    public override DateTime   Time        => DateTime.Now;
    public override Span<byte> Raw         => MainData;
    public override Span<byte> Data        => MainData;
}