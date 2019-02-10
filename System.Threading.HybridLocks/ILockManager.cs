using System.IO.Paging.PhysicalLevel.Configuration.Builder;
using System.Threading.Tasks;

namespace System.IO.Paging.PhysicalLevel.Classes.Pages.Contracts
{
    public interface ILockManager<T>
    {
        bool TryAcqureLock(T lockingObject, LockRuleset rules, byte lockType, out LockToken<T> token);
        Task<LockToken<T>> WaitLock(T lockingObject, LockRuleset rules, byte lockType);
        void ReleaseLock(LockToken<T> token, LockRuleset rules);     
        bool ChangeLockLevel(ref LockToken<T> token, LockRuleset rules, byte newLevel);
        Task<LockToken<T>> WaitForLockLevelChange(LockToken<T> token, LockRuleset rules, byte newLevel);
    }
}
