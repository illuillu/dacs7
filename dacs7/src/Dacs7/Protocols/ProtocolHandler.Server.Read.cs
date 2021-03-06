﻿// Copyright (c) Benjamin Proemmer. All rights reserved.
// See License in the project root for license information.

using Dacs7.Domain;
using Dacs7.Protocols.SiemensPlc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Dacs7.Protocols
{
    internal sealed partial class ProtocolHandler
    {
        
        private Task ReceivedReadJob(Memory<byte> buffer)
        {
            if (_provider != null)
            {
                var data = S7ReadJobDatagram.TranslateFromMemory(buffer);
                Task.Run(() => HandleReadJobAsync(data).ConfigureAwait(false)); // here we do not have to wayt because the receive buffer is fully converted and is not needed anymore
            }
            return Task.CompletedTask;
        }

        private async Task HandleReadJobAsync(S7ReadJobDatagram data)
        {
            var readRequests = data.Items.Select(rq => new ReadRequestItem((PlcArea)rq.Area, rq.DbNumber, rq.ItemSpecLength, rq.Offset, (ItemDataTransportSize)rq.TransportSize, rq.Address)).ToList();
            var results = await _provider.ReadAsync(readRequests).ConfigureAwait(false);
            await SendReadJobAck(results, data.Header.ProtocolDataUnitReference).ConfigureAwait(false);
        }

        private async Task SendReadJobAck(List<ReadResultItem> readItems, ushort id)
        {
            using (var dgmem = S7ReadJobAckDatagram.TranslateToMemory(S7ReadJobAckDatagram.Build(_s7Context, id, readItems), out var commemLength))
            {
                using (var sendData = _transport.Build(dgmem.Memory.Slice(0, commemLength), out var sendLength))
                {
                    var result = await _transport.Connection.SendAsync(sendData.Memory.Slice(0, sendLength)).ConfigureAwait(false);
                    if (result == SocketError.Success)
                    {
                        // OK
                    }
                }
            }
        }

    }
}
