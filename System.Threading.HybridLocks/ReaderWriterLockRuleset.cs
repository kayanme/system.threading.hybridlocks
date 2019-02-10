namespace System.IO.Paging.PhysicalLevel.Configuration.Builder
{
    public sealed class ReaderWriterLockRuleset:LockRuleset
    {
        public override byte GetLockLevelCount() => 2;

        public override bool AreShared(byte heldLockType, byte acquiringLockType)
        {
            if (heldLockType == acquiringLockType && acquiringLockType == 0)
                return true;
            return false;
            
        }
    }
}
