using System;
using System.Collections.Generic;

namespace IsoTp.CANBus.Net
{
    public class IsoTpProcessor
    {
        /************************************************************************
	    @ ISO 156765-2																												
        ************************************************************************/
        private const byte SINGLE_FRAME         = 0x00;
        private const byte FIRST_FRAME          = 0x10;
        private const byte CONSECUTIVE_FRAME    = 0x20;
        private const byte FLOW_CONTROL         = 0x30;
        /************************************************************************
	    @ Constant Define																												
        ************************************************************************/
        enum TX_STATE
        {
            Normal,
            FlowControl,
            ConsecutiveFrame
        }
        /************************************************************************
	    @ Parameters and Drivers																												
        ************************************************************************/
        private readonly IsoTpParams _params;
        private readonly ICanDriver _canDriver;
        public event Action<byte[]> OnMessageReceived;
        /************************************************************************
	    @ Sequence Parameters																												
        ************************************************************************/
        private class STCAN
        {
            public ushort Len;
            public List<byte> Data = new List<byte>();
            public STCAN()
            {

            }
            public STCAN(ushort len, List<byte> data)
            {
                Len = len;
                Data = data;
            }
        }
        private STCAN CANTx = new STCAN();
        private STCAN CANRx = new STCAN();

        private ushort CANTransferDataLen = 0;
        private byte CANSeqNum = 0, CANTransferDataIdx = 0, CANFcBs = 8;
        private TX_STATE TxSts = TX_STATE.Normal;
        private System.Threading.Timer _consecutiveFrameTimer;
        /************************************************************************
																												
        ************************************************************************/

        /// <summary>
        /// Initializes a new instance of the <see cref="IsoTpProcessor"/> class.
        /// </summary>
        /// <param name="udsParams">The configuration parameters for ISO-TP communication (IDs, Timing, etc.).</param>
        /// <param name="canDriver">The CAN hardware driver interface used for sending and receiving frames.</param>
        /// <remarks>
        /// This constructor automatically subscribes to the <see cref="ICanDriver.OnFrameReceived"/> event 
        /// to ensure that incoming raw CAN frames are processed immediately by the ISO-TP logic.
        /// </remarks>
        public IsoTpProcessor(IsoTpParams udsParams, ICanDriver canDriver)
        {
            _params = udsParams;
            _canDriver = canDriver;
            _canDriver.OnFrameReceived += HandleRawCanFrame;
        }
        /// <summary>
        /// Transmits a data payload using the ISO-TP (ISO 15765-2) transport protocol.
        /// </summary>
        /// <param name="DLC">The length of the data payload in bytes.</param>
        /// <param name="data">The byte array to be transmitted.</param>
        /// <param name="useFunctionalAddressing">
        /// Determines the target CAN ID. If <c>true</c>, uses the Functional Address; 
        /// otherwise, uses the Physical Address for point-to-point communication.
        /// </param>
        /// <remarks>
        /// This method initiates the ISO-TP transmission process, including segmentation into 
        /// First Frame (FF) and Consecutive Frames (CF) if the payload exceeds the single frame limit.
        /// </remarks>
        public void SendRequest(ushort DLC, byte[] data, bool isFunctional = false)
        {
            uint targetId = isFunctional ? _params.FunctionalId : _params.PhysicalId;

            CANTx = new STCAN(DLC, new List<byte>(data));
            CANcontrolSend(DLC, data, targetId);
        }
        /************************************************************************
        @ Send CAN Frame
        ************************************************************************/
        private void SendFrame(uint id, byte[] data, byte dlc)
        {
            if (_canDriver.SendCanMessage != null)
            {
                _canDriver.SendCanMessage(id, data, dlc);
            }
        }
        /************************************************************************
        @ CAN Message Processing
        ************************************************************************/
        private void HandleRawCanFrame(uint id, byte[] data)
        {
            if (id != _params.ResponseId) return;
            if (data.Length == 0) return;
            int frameType = (data[0] & 0xF0);

            switch (frameType)
            {
                case SINGLE_FRAME: // Single Frame
                    HandleSingleFrame(data);
                    break;
                case FIRST_FRAME: // First Frame
                    HandleFirstFrame(data);
                    break;
                case CONSECUTIVE_FRAME: // Consecutive Frame
                    HandleConsecutiveFrame(data);
                    break;
                case FLOW_CONTROL: // Flow Control
                    HandleFlowControl(data);
                    break;
            }
        }
        private void CANcontrolSend(ushort DLC, byte[] Data, uint ID)
        {
            if (DLC == 0)   return;

            byte SID = Data[0];
            uint CanMsgID = ID;
            byte[] CanMsgData = new byte[8];

            switch (TxSts)
            {
                case TX_STATE.Normal:
                    if (DLC < 8)
                    {
                        // Send SF
                        CanMsgData[0] = (byte)(SINGLE_FRAME | (DLC & 0x0F));
                        byte[] temp = (byte[])Data.Clone();

                        if (ID == _params.FunctionalId)
                        {
                            temp[1] |= 0x80;
                        }
                        Array.Copy(temp, 0, CanMsgData, 1, DLC);
                    }
                    else
                    {
                        CANTransferDataLen = DLC;
                        CanMsgData[0] = (byte)(FIRST_FRAME | (DLC >> 8));
                        CanMsgData[1] = (byte)(DLC & 0xFF);
                        Array.Copy(Data, 0, CanMsgData, 2, 6);
                        CANSeqNum = 0;
                        CANTransferDataIdx = 6;
                        CANTransferDataLen -= 6;
                    }
                    break;
                case TX_STATE.FlowControl:
                    Array.Copy(Data, 0, CanMsgData, 0, DLC);
                    TxSts = TX_STATE.Normal;
                    break;
                case TX_STATE.ConsecutiveFrame:
                    Array.Copy(Data, 0, CanMsgData, 0, DLC);
                    CANTransferDataLen -= (ushort)(DLC - 1);
                    TxSts = TX_STATE.Normal;
                    break;
                default: break;
            }

            // Send CAN msg
            SendFrame(CanMsgID, CanMsgData, 8);
        }

        /************************************************************************
        @ HandleSingleFrame
        ************************************************************************/
        private void HandleSingleFrame(byte[] MsgData)
        {
            int Len = (ushort)(MsgData[0] & 0x0F);
            byte[] Data = new byte[Len];
            Array.Copy(MsgData, 1, Data, 0, Len);

            OnMessageReceived?.Invoke(Data);
        }
        /************************************************************************
	    @ HandleFirstFrame
        ************************************************************************/
        private void HandleFirstFrame(byte[] MsgData)
        {
            CANRx = new STCAN();
            CANRx.Len = (ushort)(((MsgData[0] & 0x0F) << 8) | MsgData[1]);
            CANRx.Data.AddRange(new ArraySegment<byte>(MsgData, 2, 6));

            CANTransferDataLen = (ushort)(CANRx.Len - 6);
            CANTransferDataIdx = 6;
            CANSeqNum = 0;
            TxSts = TX_STATE.FlowControl;

            ushort LEN = 3;
            byte[] DATA = new byte[LEN];
            DATA[0] = FLOW_CONTROL;
            DATA[1] = _params.BLOCK_SIZE;
            DATA[2] = _params.SEPARATION_TIME_MIN;
            CANcontrolSend(LEN, DATA, _params.PhysicalId);
        }
        /************************************************************************
	    @ HandleConsecutiveFrame
        ************************************************************************/
        private void HandleConsecutiveFrame(byte[] MsgData)
        {
            CANSeqNum++;
            if (CANSeqNum > 0x0F) { CANSeqNum = 0; }

            if ((MsgData[0] & 0x0F) == CANSeqNum)
            {
                if (CANTransferDataLen >= 7)
                {
                    CANRx.Data.AddRange(new ArraySegment<byte>(MsgData, 1, 7));

                    CANTransferDataLen -= 7;
                    CANTransferDataIdx += 7;
                }
                else
                {
                    CANRx.Data.AddRange(new ArraySegment<byte>(MsgData, 1, CANTransferDataLen));
                    CANTransferDataLen = 0;
                }

                if (CANTransferDataLen == 0)
                {
                    OnMessageReceived?.Invoke(CANRx.Data.ToArray());
                }
                else
                {
                    if ((CANSeqNum % CANFcBs) == 0)
                    {
                        TxSts = TX_STATE.FlowControl;
                        ushort LEN = 3;
                        byte[] DATA = new byte[LEN];
                        DATA[0] = FLOW_CONTROL;
                        DATA[1] = _params.BLOCK_SIZE;
                        DATA[2] = _params.SEPARATION_TIME_MIN;
                        CANcontrolSend(LEN, DATA, _params.PhysicalId);
                    }
                }
            }
        }
        /************************************************************************
	    @ HandleFlowControl
        ************************************************************************/
        private void HandleFlowControl(byte[] MsgData)
        {
            CANFcBs = MsgData[1];

            _consecutiveFrameTimer?.Dispose();
            _consecutiveFrameTimer = null;

            _consecutiveFrameTimer = new System.Threading.Timer(ConsecutiveFrameTimerProc, null, 0, 1);
        }
        /************************************************************************
	    @ ConsecutiveFrameTimerProc
        ************************************************************************/
        private void ConsecutiveFrameTimerProc(object state)
        {
            byte[] TpData = new byte[8];

            // Update TX CF status
            TxSts = TX_STATE.ConsecutiveFrame;

            // Increase Sequence Number
            CANSeqNum++;
            if (CANSeqNum > 0xF)
                CANSeqNum = 0;

            TpData[0] = (byte)(CONSECUTIVE_FRAME | CANSeqNum);

            if (CANTransferDataLen > 7)
            {
                Array.Copy(CANTx.Data.ToArray(), CANTransferDataIdx, TpData, 1, 7);
                CANTransferDataIdx += 7;
                CANcontrolSend(8, TpData, _params.PhysicalId);
            }
            else
            {
                Array.Copy(CANTx.Data.ToArray(), CANTransferDataIdx, TpData, 1, CANTransferDataLen);
                _consecutiveFrameTimer?.Dispose();
                _consecutiveFrameTimer = null;
                CANcontrolSend((ushort)(CANTransferDataLen + 1), TpData, _params.PhysicalId);
            }

            if ((CANSeqNum % CANFcBs) == 0)
            {
                if (_consecutiveFrameTimer != null)
                {
                    _consecutiveFrameTimer?.Dispose();
                    _consecutiveFrameTimer = null;
                }
            }
        }
    }
}
