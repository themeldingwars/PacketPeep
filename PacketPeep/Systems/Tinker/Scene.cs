using System.Collections.Generic;
using PacketPeep.Systems.Tinker;

namespace PacketPeep.Systems.Tinker
{
    public class Scene
    {
        public string Name = "New Scene";
        public int    ZoneId = 12;
        public double  TimeOfDay = 0d;

        public List<Entity> Entities = new();
    }
}