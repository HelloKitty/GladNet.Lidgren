using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using GladNet.Common;
using GladNet.Payload;
using GladNet.Engine.Common;
using System.Threading;
using GladNet.Serializer;

namespace GladNet.Lidgren.Engine.Common
{
	public class ManagedNetworkThread : INetworkThread, IDisposable
	{
		/// <summary>
		/// The ordered awaitable collection of <see cref="LidgrenMessageContext"/>s.
		/// </summary>
		public IThreadedQueue<LidgrenMessageContext, AutoResetEvent> IncomingMessageQueue { get; } = new AutoResetQueue<LidgrenMessageContext>();

		/// <summary>
		/// The ordered awaitable collection of <see cref="Action"/>s that dispatch outgoing messages.
		/// </summary>
		private IThreadedQueue<Action, AutoResetEvent> outgoingMessageQueue { get; } = new AutoResetQueue<Action>();

		/// <summary>
		/// Strategy used to select the sending service for a message.
		/// </summary>
		private ISendServiceSelectionStrategy sendServiceStrategy { get; }

		/// <summary>
		/// Serialization strategy.
		/// </summary>
		private ISerializerStrategy serializer { get; }

		/// <summary>
		/// Exception callback.
		/// </summary>
		private Action<Exception> OnException { get; }

		private ILidgrenMessageContextFactory messageContextFactory { get; }

		public bool isAlive { get; private set; } = false;

		private readonly object internalSyncObj = new object();

		public ManagedNetworkThread(ISerializerStrategy serializerStrategy, ILidgrenMessageContextFactory lidgrenMessageContextFactory, ISendServiceSelectionStrategy sendStrategy, Action<Exception> onException = null)
		{
			if (serializerStrategy == null)
				throw new ArgumentNullException(nameof(serializerStrategy), $"Provided {nameof(ISerializerStrategy)} cannot be null.");

			if (lidgrenMessageContextFactory == null)
				throw new ArgumentNullException(nameof(lidgrenMessageContextFactory), $"Provided {nameof(ILidgrenMessageContextFactory)} cannot be null.");

			if (sendStrategy == null)
				throw new ArgumentNullException(nameof(sendStrategy), $"Provided {nameof(ISendServiceSelectionStrategy)} cannot be null.");

			OnException = onException;

			sendServiceStrategy = sendStrategy;
			messageContextFactory = lidgrenMessageContextFactory;
			serializer = serializerStrategy;
		}


		public void Start(NetPeer incomingPeerContext)
		{
			lock(internalSyncObj)
			{
				isAlive = true;
			}
		}

		public void Stop()
		{
			lock(internalSyncObj)
			{
				isAlive = false;
			}
		}

		public NetSendResult EnqueueMessage(OperationType opType, PacketPayload payload, DeliveryMethod method, bool encrypt, byte channel, int connectionId)
		{
			//you do not need a full internal lock because if the network thread is dead, or should die, then it just won't read what was queued.

			outgoingMessageQueue.syncRoot.EnterWriteLock();
			try
			{
				//This is similar to how Photon works on the Unity client.
				//They enqueue actions
				outgoingMessageQueue.Enqueue(() =>
				{
					INetworkMessageRouterService sender = sendServiceStrategy.GetRouterService(connectionId);

					sender.TrySendMessage(opType, payload, method, encrypt, channel);
				});

				//signal any handlers that a message is in the queue
				//it is important to do this in a lock. Reset could be called after Set
				//in the other thread and we'd end up with potential unhandled messages in that race condition
				outgoingMessageQueue.QueueSemaphore.Set();
			}
			finally
			{
				outgoingMessageQueue.syncRoot.ExitWriteLock();
			}

			return NetSendResult.Queued;
		}

		private void NetworkThreadOperation(NetPeer peer)
		{
			try
			{
				//Spin while alive
				while(isAlive)
				{
					//once inside of the loop we should do an internal lock to prevent shutdowns or stops
					lock(internalSyncObj)
					{
						//double check locking
						if (!isAlive)
							break; //break from the network thread operation if if we stopped.

						//This is a single threaded setup where we read and write on the same thread.
						//In the future this can be extended to dedicated read/write threads, especially for servers,
						//but for simiplicity we use a single thread right now

						if (outgoingMessageQueue.QueueSemaphore.WaitOne(20))
							;

						//Now try to refill the incoming queue from messages from the internal network client
						
					}

					lock (internalSyncObj)
					{
						//double check locking
						if (!isAlive)
							break; //break from the network thread operation if if we stopped.

						if (TryFillIncomingNetQueue(peer))
							;
					}
				}
			}
			catch(Exception e)
			{
				OnException?.Invoke(e);
			}
		}

		private bool TryFillIncomingNetQueue(NetPeer peer)
		{
			//Wait on the handle to tell us there were messages
			if (!peer.MessageReceivedEvent.WaitOne(20))
				return false;

			//Read the entire chunk of messages; not just one
			//The wait handle has been consumed so we have no choice.
			//Can't do one at a time.
			List<NetIncomingMessage> messages = new List<NetIncomingMessage>(10); //instead of 10 find the average or maybe make this dynamic.

			if (peer.ReadMessages(messages) == 0)
				return false;

			foreach (NetIncomingMessage message in messages)
			{
				//If the context factory cannot create a context for this
				//message type then we should not enter the lock and attempt to create a context for it.
				if (!messageContextFactory.CanCreateContext(message.MessageType))
					continue;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~ManagedNetworkThread() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
