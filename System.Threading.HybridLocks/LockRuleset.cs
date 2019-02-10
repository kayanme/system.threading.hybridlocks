using System.IO.Paging.PhysicalLevel.Classes;
using System.Threading;

namespace System.IO.Paging.PhysicalLevel.Configuration.Builder
{
    public abstract class LockRuleset
    {
        public abstract byte GetLockLevelCount();

        public abstract bool AreShared(byte heldLockType, byte acquiringLockType);

        private LockMatrix _lockMatrix;
        internal LockMatrix LockMatrix
        {
            get
            {
                if (_lockMatrix != null)
                    return _lockMatrix;
                var newMatrix = new LockMatrix(this);
                Interlocked.CompareExchange(ref _lockMatrix, newMatrix, null);
                return _lockMatrix;
            }
        }
        
    }
}
