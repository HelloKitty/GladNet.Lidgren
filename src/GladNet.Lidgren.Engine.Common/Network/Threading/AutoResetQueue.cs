using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GladNet.Lidgren.Engine.Common
{
	public class AutoResetQueue<T> : Queue<T>, IThreadedQueue<T, AutoResetEvent>
	{
		/// <summary>
		/// A manual-managed semaphore for the <see cref="Queue{T}"/>.
		/// </summary>
		public AutoResetEvent QueueSemaphore { get; } = new AutoResetEvent(false);

		/// <summary>
		/// A read/write optimized syncronization queue.
		/// </summary>
		public ReaderWriterLockSlim syncRoot { get; } = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion); //Unity requires no recursion

		WaitHandle IThreadedQueue<T>.QueueSemaphore { get { return QueueSemaphore; } }

		/// <summary>
		/// Creates a default <see cref="Queue{T}"/> with a <see cref="AutoResetEvent"/>.
		/// </summary>
		public AutoResetQueue()
		{

		}

		public IEnumerable<T> DequeueAll()
		{
			IEnumerable<T> dequeued = this.ToList();
			this.Clear();
			return dequeued;
		}

		bool IThreadedQueue<T>.Enqueue(T obj)
		{
			this.Enqueue(obj);

			return true;
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
					this.Clear();
				}

				this.syncRoot.Dispose();
				this.QueueSemaphore.Close();

				disposedValue = true;
			}
		}

		 ~AutoResetQueue()
		{
		   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		   Dispose(false);
		 }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);

			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
