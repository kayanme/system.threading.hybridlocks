using System.Threading.Tasks;
using FakeItEasy;
using System.IO.Paging.PhysicalLevel.Classes;
using System.IO.Paging.PhysicalLevel.Configuration.Builder;
using System.IO.Paging.PhysicalLevel.Classes.Pages.Contracts;
using System.IO.Paging.PhysicalLevel.Implementations;
using NUnit.Framework;

namespace Test.Paging.PhysicalLevel.Locks
{
    [TestFixture]
    public class LockTest
    {
        
        private ILockManager<int> CreateLock(bool readerWriteLockScheme)
        {

            var rules = A.Fake<LockRuleset>();
            if (!readerWriteLockScheme)
            {
                A.CallTo(() => rules.GetLockLevelCount()).Returns<byte>(1);
                A.CallTo(() => rules.AreShared(0, 0)).Returns(false);
            }
            else
            {
                A.CallTo(() => rules.GetLockLevelCount()).Returns<byte>(2);
                A.CallTo(() => rules.AreShared(0, 0)).Returns(true);
                A.CallTo(() => rules.AreShared(1, 0)).Returns(false);
                A.CallTo(() => rules.AreShared(0, 1)).Returns(false);
                A.CallTo(() => rules.AreShared(1, 1)).Returns(false);
            }

            matrix = rules;
            return new LockManager<int>();
        }


        private LockRuleset matrix;
        
        [Test]
        public void AcquireRecordLock()
        {
            var manager = CreateLock(false);

            Assert.IsTrue(manager.TryAcqureLock(1,  matrix, 0, out var token));
            Assert.AreEqual(0, token.LockLevel);
            Assert.AreEqual(1, token.LockedObject);
        }


        [Test]
        public void AcquireRecordLockAndCheckItLocked()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1, matrix, 0, out var token);
            Assert.IsFalse(manager.TryAcqureLock(1,  matrix, 0, out var _));
        }

        [Test]
        public void AcquireRecordLockAndCheckItNotLockedForAnotherRecord()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1,  matrix, 0, out var token);
            Assert.IsTrue(manager.TryAcqureLock(2,  matrix, 0, out var _));
        }

        [Test]
        public void AcquireRecordLock_AndRelease_AndReacquire()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1, matrix, 0, out var token);
            manager.ReleaseLock(token, matrix);
            Assert.IsTrue(manager.TryAcqureLock(1,  matrix, 0, out token));
        }


        [Test]
        public void AcquireRecordLock_AndRelease_AndReacquire_Alt()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1, matrix, 0, out var token);
            token.Release();
            Assert.IsTrue(manager.TryAcqureLock(1, matrix, 0, out token));
        }

        [Test]
        public async Task AcquireRecordLock_AndRelease_AndWaitForIt()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1,  matrix, 0, out var token);
            token.Release();
            token = await manager.WaitLock(1,matrix,0);
            Assert.AreEqual(1,token.LockedObject);
            Assert.AreEqual(0, token.LockLevel);
        }

        [Test]
        public void AcquireSharedRecordLockAndCheckItNotLocked()
        {
            var manager = CreateLock(true);

            manager.TryAcqureLock(1, matrix, 0, out _);
            Assert.IsTrue(manager.TryAcqureLock(1, matrix, 0, out _));
        }

        [Test]
        public void AcquireSharedRecordTwoTimesLockAndCheckItLockedForNonSharedAfterOneRelease_AndNotLockedAfterSecond()
        {
            var manager = CreateLock(true);

            manager.TryAcqureLock(1,  matrix, 0, out var token);
            manager.TryAcqureLock(1, matrix, 0, out var token2);
            token.Release();
            Assert.IsFalse(manager.TryAcqureLock(1, matrix, 1, out _));
            token2.Release();
            Assert.IsTrue(manager.TryAcqureLock(1, matrix, 1, out _));

        }

        [Test]
        public void AcquireSharedRecordLockAndCheckItLockedForNonShared()
        {
            var manager = CreateLock(true);

            manager.TryAcqureLock(1,  matrix,0, out var _);
            Assert.IsFalse(manager.TryAcqureLock(1,  matrix,1, out var _));
        }

        [Test]
        public void AcquireSharedRecordLock_ThenRelease_AndCheckItNotLockedForNonShared()
        {
            var manager = CreateLock(true);

            manager.TryAcqureLock(1,  matrix,0, out var token);
            token.Release();
            Assert.IsTrue(manager.TryAcqureLock(1,  matrix, 1,out var _));
        }

        [Test]
        public void WaitForRecordLock()
        {
            var manager = CreateLock(false);

            var token = manager.WaitLock(1,  matrix,0).Result;
            Assert.AreEqual(0, token.LockLevel);
            Assert.AreEqual(1, token.LockedObject);
        }


        [Test]
        public void WaitForRecordLockAndCheckItLocked()
        {
            var manager = CreateLock(false);

            var task = manager.WaitLock(1,  matrix,0);
            task.Wait();
            Assert.IsFalse(manager.TryAcqureLock(1,  matrix,0, out var _));
        }

        [Test]
        public void AcquireLockAndCheckThatItUnlocksForWaiterAfterRelease()
        {
            var manager = CreateLock(false);

            manager.TryAcqureLock(1,  matrix,0, out var token);
            bool lockAcquired = false;
            var task = manager.WaitLock(1,  matrix,0);
            var t2 = task.ContinueWith(t =>
            {
                token = t.Result;
                lockAcquired = true;
            });

            Assert.IsFalse(lockAcquired);
            manager.ReleaseLock(token, matrix);

            t2.Wait(1000);
            Assert.IsTrue(lockAcquired);
            Assert.AreEqual(0, token.LockLevel);
            Assert.AreEqual(1, token.LockedObject);
        }

        [Test]
        public async Task WaitForSharedRecordLockAndCheckItNotLocked()
        {
            var manager = CreateLock(true);

            await manager.WaitLock(1,  matrix,0);
            Assert.IsTrue(manager.TryAcqureLock(1,  matrix,0, out var _));
        }

        [Test]
        public void TakeSharedRecordLock_AndWaitForNonShared()
        {
            var manager = CreateLock(true);
            manager.TryAcqureLock(1,  matrix, 0,out var token);
            var acquired = false;
            var t = manager.WaitLock(1,  matrix,1);
            var t2 = t.ContinueWith(_ => acquired = true);

            Assert.IsFalse(acquired);
            token.Release();
            t2.Wait(1000);
            Assert.IsTrue(acquired);
        }

        [Test]
        public void TakeSharedLock_ReleaseIt_AndWaitForNonShared()
        {
            var manager = CreateLock(true);
            manager.TryAcqureLock(1,  matrix,0, out var token);
            
            var acquired = false;
            var t = manager.WaitLock(1,  matrix,1);
            
            var t2 = t.ContinueWith(_ => acquired = true);
            Assert.IsFalse(acquired);
            token.Release();
            t2.Wait(1000);
            Assert.IsTrue(acquired);
        }

        [Test]
        public void ChainOFSharedAndNonShared()
        {
            var manager = CreateLock(true);
            manager.TryAcqureLock(1,  matrix,0, out var token);
            token.Release();
            manager.TryAcqureLock(1,  matrix,1,out token);
            token.Release();
            manager.TryAcqureLock(1,  matrix,0, out token);
            token.Release();         
            var acquired =  manager.TryAcqureLock(1,  matrix,1,out token);
         
            Assert.IsTrue(acquired);
        }


        [Test]
        public void TakeTwoSharedLocks_AndWaitForNonShared()
        {
            var manager = CreateLock(true);

            manager.TryAcqureLock(1,  matrix,0, out var token);
            manager.TryAcqureLock(1,  matrix,0, out var token2);
            var acquired = false;
            var t = manager.WaitLock(1,  matrix,1);
            var t2 = t.ContinueWith(_ => acquired = true);

            Assert.IsFalse(acquired);
            token.Release();
            Assert.IsFalse(t2.Wait(50));
            token2.Release();
            t2.Wait(50);
            Assert.IsTrue(acquired);
        }
    }
}
