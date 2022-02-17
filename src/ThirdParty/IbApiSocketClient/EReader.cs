/* Copyright (C) 2019 Interactive Brokers LLC. All rights reserved. This code is subject to the terms
 * and conditions of the IB API Non-Commercial License or the IB API Commercial License, as applicable. */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;

namespace IBApi
{
    /**
    * @brief Captures incoming messages to the API client and places them into a queue.
    */
    public class EReader
    {
        EClientSocket eClientSocket;
        EReaderSignal eReaderSignal;
        Queue<EMessage> msgQueue = new Queue<EMessage>();
        EDecoder processMsgsDecoder;
        const int defaultInBufSize = ushort.MaxValue / 8;

        bool UseV100Plus
        {
            get
            {
                return eClientSocket.UseV100Plus;
            }
        }


        static EWrapper defaultWrapper = new DefaultEWrapper();

        public EReader(EClientSocket clientSocket, EReaderSignal signal)
        {
            eClientSocket = clientSocket;
            eReaderSignal = signal;
            processMsgsDecoder = new EDecoder(eClientSocket.ServerVersion, eClientSocket.Wrapper, eClientSocket);
        }

        public void Start()
        {
            new Thread(() =>
            {
                try
                {
                    while (eClientSocket.IsConnected())
                        if (!putMessageToQueue())
                            break;
                }
                catch (Exception ex)
                {
                    // 1. System.IO.IOException is expected when main thread exits and after it calls ClientSocket.eDisconnect();
                    // in the msg processing loop this throws the exception: IbApiSocketClient.dll!IBApi.EClient.ReadInt()
                    // Fine. It seems we cannot prevent it. We cannot cancel this putMessageToQueue() loop. 
                    // It is written that once it is started, it always listen. No way to cancel it.

                    // 2. System.IO.EndOfStreamException happens when a previous disconnection went wrong and IbGateway still hangs-on the connection, not realizing it was disconnected.
                    // Succesive connections have problems. Solution: restart the IbGateway/TWS that is stuck on that open connection.

                    eClientSocket.Wrapper.error(ex);
                    eClientSocket.eDisconnect();
                }

                eReaderSignal.issueSignal();
            }) { IsBackground = true }.Start();
        }

        EMessage getMsg()
        {
            lock (msgQueue)
                return msgQueue.Count == 0 ? null : msgQueue.Dequeue();
        }

        public void processMsgs()
        {
            EMessage msg = getMsg();

            while (msg != null && processMsgsDecoder.ParseAndProcessMsg(msg.GetBuf()) > 0)
                msg = getMsg();
        }

        public bool putMessageToQueue()
        {
            try
            {
                EMessage msg = readSingleMessage();

                if (msg == null)
                    return false;

                lock (msgQueue)
                    msgQueue.Enqueue(msg);

                eReaderSignal.issueSignal();

                return true;
            }
            catch (Exception ex)
            {
                if (eClientSocket.IsConnected())
                    // eClientSocket.Wrapper.error(ex.Message); in 2019 API version it calls error(string) version. That is bad!! It is for errors from IbGateway.
                    eClientSocket.Wrapper.error(ex); // in 2015 API version, it called error(Exception) that is the good one. It is for C# client runtime exceptions.

                return false;
            }
        }

        List<byte> inBuf = new List<byte>(defaultInBufSize);

        private EMessage readSingleMessage()
        {
            var msgSize = 0;

            if (UseV100Plus)
            {
                msgSize = eClientSocket.ReadInt();

                if (msgSize > Constants.MaxMsgSize)
                {
                    throw new EClientException(EClientErrors.BAD_LENGTH);
                }

                return new EMessage(eClientSocket.ReadByteArray(msgSize));
            }

            if (inBuf.Count == 0)
                AppendInBuf();

            while (true)
                try
                {
                    msgSize = new EDecoder(this.eClientSocket.ServerVersion, defaultWrapper).ParseAndProcessMsg(inBuf.ToArray());
                    break;
                }
                catch (EndOfStreamException)
                {
                    if (inBuf.Count >= inBuf.Capacity * 3/4)
                        inBuf.Capacity *= 2;

                    AppendInBuf();
                }
            
            var msgBuf = new byte[msgSize];

            inBuf.CopyTo(0, msgBuf, 0, msgSize);
            inBuf.RemoveRange(0, msgSize);
          
            if (inBuf.Count < defaultInBufSize && inBuf.Capacity > defaultInBufSize)
                inBuf.Capacity = defaultInBufSize;

            return new EMessage(msgBuf);
        }

        private void AppendInBuf()
        {
            inBuf.AddRange(eClientSocket.ReadAtLeastNBytes(inBuf.Capacity - inBuf.Count));
        }
    }
}
