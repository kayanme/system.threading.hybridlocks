using System.Collections.Concurrent;
using System.Threading;
using BenchmarkDotNet.Attributes;
using System.IO.Paging.PhysicalLevel.Classes;
using System.IO.Paging.PhysicalLevel.Configuration.Builder;
using System.IO.Paging.PhysicalLevel.Implementations;

namespace Benchmark.Paging.PhysicalLevel
{
    public class LockBenchmark
    {
        private ConcurrentDictionary<int,ReaderWriterLockSlim> _locks = new ConcurrentDictionary<int, ReaderWriterLockSlim>();
     
        private LockManager<int> _lock = new LockManager<int>();
        private LockRuleset _matrix = new ReaderWriterLockRuleset();

        [Benchmark]
        public void NaiveReadTakeRelease()
        {
            var _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));
           _nativeLock.EnterReadLock();
            _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim());
            _nativeLock.ExitReadLock();
        }

        [Benchmark]
        public void NaiveTwoReadTakeRelease()
        {
            var _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion));
            _nativeLock.EnterReadLock();
            _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim());
            _nativeLock.EnterReadLock();
            _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim());
            _nativeLock.ExitReadLock();
            _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim());
            _nativeLock.ExitReadLock();
        }

        [Benchmark]
        public void PageReadTakeRelease()
        {
            _lock.TryAcqureLock(1, _matrix,0, out var token);
            _lock.ReleaseLock(token,_matrix);
        }


        [Benchmark]
        public void PageTwoReadTakeRelease()
        {
            _lock.TryAcqureLock(1, _matrix, 0, out var token);
            _lock.TryAcqureLock(1, _matrix, 0, out var token2);
            _lock.ReleaseLock(token, _matrix);
            _lock.ReleaseLock(token2, _matrix);
        }

        [Benchmark]
        public void NaiveWriteTakeRelease()
        {
            var _nativeLock = _locks.GetOrAdd(1, _ => new ReaderWriterLockSlim());
            _nativeLock.EnterReadLock();
            _nativeLock.ExitReadLock();
        }

        [Benchmark]
        public void PageWriteTakeRelease()
        {
            _lock.TryAcqureLock(1, _matrix, 0, out var token);
            _lock.ReleaseLock(token, _matrix);
        }
    }
}
