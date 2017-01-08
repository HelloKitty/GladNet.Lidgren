using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GladNet.Lidgren.Engine.Common
{
	public interface INetworkThread
	{
		/// <summary>
		/// Ordered collection of incoming messages.
		/// </summary>
		IThreadedQueue<LidgrenMessageContext, AutoResetEvent> IncomingMessageQueue { get; }

		/// <summary>
		/// Indicates if the network thread is running.
		/// </summary>
		bool isAlive { get; }

		/// <summary>
		/// Peer context to use for the network thread.
		/// </summary>
		/// <param name="outgoingPeerContext"></param>
		void Start(NetPeer incomingPeerContext);

		/// <summary>
		/// Stops the network thread.
		/// </summary>
		void Stop();
	}
}
