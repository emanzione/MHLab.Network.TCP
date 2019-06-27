namespace MHLab.Network.TCP
{
    internal class ReceiveBuffer
    {
        internal byte[] Buffer;
        internal int ReceivedBytes;
        
        public static ReceiveBuffer Create(int size)
        {
            var buffer = new ReceiveBuffer
            {
                Buffer = new byte[size], 
                ReceivedBytes = 0
            };

            return buffer;
        }

        public static void Release(ReceiveBuffer buffer)
        {
            
        }
    }
}