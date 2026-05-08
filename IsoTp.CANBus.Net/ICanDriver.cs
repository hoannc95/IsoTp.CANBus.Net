using System;

namespace IsoTp.CANBus.Net
{
    public class ICanDriver
    {
        public Action<uint, byte[], byte> SendCanMessage { get; set; }
        public event Action<uint, byte[]> OnFrameReceived;

        public void InvokeReceive(uint id, byte[] data)
        {
            OnFrameReceived?.Invoke(id, data);
        }
    }
}
