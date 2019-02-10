using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO.Paging.PhysicalLevel.Classes;
using System.IO.Paging.PhysicalLevel.Classes.Pages.Contracts;
using System.IO.Paging.PhysicalLevel.Configuration.Builder;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Paging.PhysicalLevel.Implementations
{


    [InheritedExport(typeof(ILockManager<>))]
    public sealed class LockManager<T>:ILockManager<T>
    {
        private class LockHolder
        {
            public volatile int LockInfo;
            private readonly int[] SelfSharedLocks;
            public long LastUsage;
            public LockHolder(byte selfSharedLocks)
            {
                 SelfSharedLocks = new int[selfSharedLocks];
            }
            
            public int GetSharedLockCount(int i) => SelfSharedLocks[i];

            public int IncrLock(int i) => Interlocked.Increment(ref SelfSharedLocks[i]);
            public int DecrLock(int i) => Interlocked.Decrement(ref SelfSharedLocks[i]);
        }
        private ConcurrentDictionary<T, LockHolder> _locks = new ConcurrentDictionary<T, LockHolder>();

        public bool TryAcqureLock(T lockingObject, LockRuleset rules, byte lockType, out LockToken<T> token)
        {
            var h = _locks.GetOrAdd(lockingObject, _ => new LockHolder(rules.LockMatrix.SelfSharedLocks));
            h.LastUsage = Stopwatch.GetTimestamp();

            return EnterIfPossible(lockingObject, lockType, rules, out token, h);
        }

        private bool EnterIfPossible(T lockingObject, byte lockType, LockRuleset rules, out LockToken<T> token, LockHolder h)
        {
            var entrancePair = rules.LockMatrix.EntrancePair(lockType, h.LockInfo);//найден (или не найден) допустимый переход из текущего состояния блокировок в требуемый тип
            if (entrancePair.HasValue)//если найден
            {
                
                unchecked
                {
                    var blockExit = (int)entrancePair.Value.BlockExit;
                    Debug.Assert((entrancePair.Value.BlockEntrance & LockMatrix.SharenessCheckLock) == 0, "(entrancePair.Value.BlockEntrance & LockMatrix.SharenessCheckLock) == 0","Не должно быть флага разделяемой блокировки в допустимом входном состоянии");
                    //делаем непосредственно переход
                    if (Interlocked.CompareExchange(ref h.LockInfo, blockExit, (int)entrancePair.Value.BlockEntrance) == (int)entrancePair.Value.BlockEntrance)
                    {
                        int sharedLockCount = 0;
                        //если взяли разделяемую блокировку с уже существующей
                        if ((blockExit & LockMatrix.SharenessCheckLock) == LockMatrix.SharenessCheckLock)
                        {
                            sharedLockCount = h.IncrLock(rules.LockMatrix.SelfSharedLockShift(lockType));//добавляем счётчик к количеству этих блокировок на заданном типе
                            Debug.Print($"sharedLockCount");
                            Debug.Assert(h.LockInfo == blockExit, "h.LockInfo == blockExit", "Состояние блокировок изменилось там, где не должно");
                            h.LockInfo = (int)((uint)h.LockInfo & LockMatrix.SharenessCheckLockDrop);//и сбрасываем этот флажок обратно. Никто не должен поменять LockInfo, т.к. с этим поднятым флажков не должно быть допустимых состояний.
                        }
                        token = new LockToken<T>(lockType, lockingObject, this, rules,sharedLockCount);
                        return true;      
                    }
                }
            }
            token = default(LockToken<T>);
            return false;
        }

        public async Task<LockToken<T>> WaitLock(T lockingObject, LockRuleset rules, byte lockType)
        {
            var h = _locks.GetOrAdd(lockingObject, _ => new LockHolder(rules.LockMatrix.SelfSharedLocks));
            h.LastUsage = Stopwatch.GetTimestamp();
            LockToken<T> token;
            while (!EnterIfPossible(lockingObject, lockType, rules, out token, h)) await Task.Delay(1);
            return token;

        }
        
        public void ReleaseLock(LockToken<T> token, LockRuleset rules)
        {
            if (!_locks.TryGetValue(token.LockedObject,out var h))
            {
                throw new InvalidOperationException("Object not locked");
            }
            var lockType = token.LockLevel;
            var matrix = rules.LockMatrix;
            unchecked
            {
                if (matrix.IsSelfShared(lockType))
                {
                    int i;
                    Thread.BeginCriticalRegion();

                    do //переводим комбинацию блокировок в новое состояние и поднимаем флаг проверки разделямых блокировок
                    {
                        i = (int)( h.LockInfo & LockMatrix.SharenessCheckLockDrop);//мы исключаем из возможных переходы, результатом которых будет поднятый флаг проверки разделяемой блокировки (проверки разделяемой блокировки смогут пересечься)
                    } while (Interlocked.CompareExchange(ref h.LockInfo,i | (int)LockMatrix.SharenessCheckLock,i)!=i);
                    
                    if (h.GetSharedLockCount(matrix.SelfSharedLockShift(lockType)) == 0)//если текущий переход убирает существующую разделяемую блокировку (т.е. их сейчас нет).
                    {
                        //делаем переход в новое состояние
                        h.LockInfo = (int)matrix.ExitPair(lockType,(int)(LockMatrix.SharenessCheckLockDrop & (uint)h.LockInfo)).Value.BlockEntrance;
                    }
                    else//если же видим, что освобождение текущей блокировки только уменьшает количество разделяемых блокировок её типа
                    {
                        var sharedLockCount = h.DecrLock(matrix.SelfSharedLockShift(lockType));//то уменьшаем счётчик блокировок
                        Debug.Print($"{sharedLockCount}");
                        h.LockInfo = h.LockInfo & (int)LockMatrix.SharenessCheckLockDrop;//и сбрасываем флаг проверки
                    }
                    Thread.EndCriticalRegion();
                }
                else
                {                     
                    while (true)
                    {
                        var entrancePair = matrix.ExitPair(lockType,(int)(h.LockInfo & LockMatrix.SharenessCheckLockDrop));
                        if (entrancePair.HasValue)
                        {
                            if (Interlocked.CompareExchange(ref h.LockInfo, (int) entrancePair.Value.BlockEntrance,
                                    (int) entrancePair.Value.BlockExit) == (int) entrancePair.Value.BlockExit)
                            {
                                break;
                            }
                        }
                    }

                }
            }

        }
        

        public bool ChangeLockLevel(ref LockToken<T> token, LockRuleset rules, byte newLevel)
        {
            if (!_locks.TryGetValue(token.LockedObject, out var h))
            {
                throw new InvalidOperationException("Object not locked");
            }
            var matrix = rules.LockMatrix;
            var excalationPair = matrix.EscalationPairs(token.LockLevel, newLevel)
                .Select(k=>(LockMatrix.MatrPair?)k)
                .FirstOrDefault(k => k.Value.BlockEntrance == h.LockInfo);
            if (excalationPair.HasValue)
            {
                if (Interlocked.CompareExchange(ref h.LockInfo, (int) excalationPair.Value.BlockExit,
                        (int) excalationPair.Value.BlockEntrance) == excalationPair.Value.BlockEntrance)
                {
                    token = new LockToken<T>(newLevel, token.LockedObject, this, rules,0);
                    return true;
                }
            }
            return false;
        }

        public async Task<LockToken<T>> WaitForLockLevelChange(LockToken<T> token, LockRuleset rules, byte newLevel)
        {
            if (!_locks.TryGetValue(token.LockedObject, out var h))
            {
                throw new InvalidOperationException("Object not locked");
            }
            var matrix = rules.LockMatrix;
            while (true)
            {
                var excalationPair = matrix.EscalationPairs(token.LockLevel, newLevel)
                    .Select(k => (LockMatrix.MatrPair?)k)
                    .FirstOrDefault(k => k.Value.BlockEntrance == h.LockInfo);
                if (excalationPair.HasValue)
                {
                    if (Interlocked.CompareExchange(ref h.LockInfo, (int)excalationPair.Value.BlockExit,
                            (int)excalationPair.Value.BlockEntrance) == excalationPair.Value.BlockEntrance)
                    {

                        return new LockToken<T>(newLevel, token.LockedObject, this, rules, 0);
                    }
                }
                await Task.Delay(1);
            }

        }
    }
}
