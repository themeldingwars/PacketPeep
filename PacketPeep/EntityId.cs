namespace PacketPeep;

public unsafe struct EntityId
{
    public ulong Backing;

    public byte ControllerId
    {
        //get => (Controller) (Backing & 0x00000000000000FF);
        get
        {
            fixed (ulong* b = &Backing) {
                return (*((byte*) b));
            }
        }
        set
        {
            fixed (ulong* b = &Backing) {
                (*((byte*) b)) = (byte) value;
            }
        }
    }

    public ulong Id
    {
        get => Backing >> 8;
        set => Backing = (Backing & 0x00000000000000FF) | (value << 8);
    }

    public override string ToString()
    {
        return Backing == 0 ? "None" : $"{ControllerId}:{Id}";
    }
}