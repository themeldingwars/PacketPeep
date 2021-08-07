using System;
using System.Threading.Tasks;

namespace PacketPeep
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                await PacketPeepTool.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}