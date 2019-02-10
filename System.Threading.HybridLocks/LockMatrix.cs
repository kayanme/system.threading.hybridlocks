using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Paging.PhysicalLevel.Configuration.Builder;
using System.Linq;
using System.Runtime.CompilerServices;

namespace System.IO.Paging.PhysicalLevel.Classes
{
    internal sealed class LockMatrix
    {
        /// <summary>
        /// Описывает один допустимый вариант переходы из одной комбинации блокировок в другую.
        /// </summary>
        internal struct MatrPair
        {
            /// <summary>
            /// Битовая маска, описывающая начальную комбинацию блокировок.
            /// </summary>
            /// <remarks>Бит в каждой позиции означает удерживаемую блокировку соответсвующего типа (кроме самого первого бита, <see cref="SharenessCheckLock"/>).</remarks>
            public readonly uint BlockEntrance;
            /// <summary>
            /// Битовая маска, описывающая конечную комбинацию блокировок.
            /// </summary>
            public readonly uint BlockExit;

            public MatrPair(uint blockEntrance, uint blockExit)
            {
                BlockEntrance = blockEntrance;
                BlockExit = blockExit;
            }

            public override string ToString() => $"{BlockEntrance} -> {BlockExit}";
           
        }

        private readonly byte[] _lockSelfSharedFlag;

        private readonly MatrPair[][] _lockSwitchMatrix;

        private readonly IReadOnlyDictionary<int,MatrPair>[] _lockSwitchDictionary;
        private readonly IReadOnlyDictionary<int, MatrPair>[] _lockSwitchBackDictionary;

        private MatrPair[] _singleLockSwitchPaths;

        private readonly MatrPair[,][] _lockEscalationMatrix;

        /// <summary>
        /// Является ли передаваемый тип блокировки разделяемым сам с собой.
        /// </summary>
        /// <param name="lockType">Тип блокировки.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsSelfShared(byte lockType) => _lockSelfSharedFlag[lockType]!=255;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte SelfSharedLockShift(byte lockType) => _lockSelfSharedFlag[lockType];

        internal readonly byte SelfSharedLocks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MatrPair? EntrancePair(byte acquiringLockType, int startPattern)
        {
            if (_lockSwitchDictionary[acquiringLockType] == null)
            {
                if (_singleLockSwitchPaths[acquiringLockType].BlockEntrance == startPattern)
                    return _singleLockSwitchPaths[acquiringLockType];
                return default(MatrPair?);
            }
            if (_lockSwitchDictionary[acquiringLockType].ContainsKey(startPattern))
            {
                return _lockSwitchDictionary[acquiringLockType][startPattern];
            }
            return null;
        }

        internal MatrPair? ExitPair(byte acquiringLockType, int exitPattern)
        {
            if (_lockSwitchDictionary[acquiringLockType] == null)
            {
                if (_singleLockSwitchPaths[acquiringLockType].BlockExit == exitPattern)
                    return _singleLockSwitchPaths[acquiringLockType];
                return default(MatrPair?);
            }
            if (_lockSwitchBackDictionary[acquiringLockType].ContainsKey(exitPattern))
            {
                return _lockSwitchBackDictionary[acquiringLockType][exitPattern];
            }
            return null;
        }

        /// <summary>
        /// Все возможные начальные сочетания блокировок, для которых возможно получить данную.
        /// </summary>
        /// <param name="acquiringLockType"></param>
        /// <returns>Переходы для соответствующих начальных сочетаний.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IEnumerable<MatrPair> EntrancePairs(byte acquiringLockType)
        {
          
            foreach (var matrPair in _lockSwitchMatrix[acquiringLockType])
            {
                yield return matrPair;
            }
        }

        /// <summary>
        /// Все возможные переходы из начального сочетания блокировок в конечное для эскалации блокировки указанного типа в следующий.
        /// </summary>
        /// <param name="initialLockType"></param>
        /// <param name="newLockType"></param>
        /// <returns>Все соответствующие переходы.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IEnumerable<MatrPair> EscalationPairs(byte initialLockType,byte newLockType)
        {

            foreach (var matrPair in _lockEscalationMatrix[initialLockType,newLockType])
            {
                yield return matrPair;
            }
        }

      

        /// <summary>
        /// Флаг, который сигнализирует запрет изменения текущей комбинации блокировок (на время проверки количества разделямых блокировок).
        /// </summary>
        /// <remarks>Это работает, т.к. он формирует такое входное состояние, которое точно будет недопустимым к переходу из него.</remarks>
        internal const uint SharenessCheckLock = 0b10000000000000000000000000000000;
        internal const uint SharenessCheckLockDrop = ~SharenessCheckLock;

        

        public  LockMatrix(LockRuleset rules)
        {
            Debug.Assert(rules.GetLockLevelCount()<32,"rules.GetLockLevelCount()<32");
            Debug.Assert(rules.GetLockLevelCount() > 0, "rules.GetLockLevelCount()>0");
            byte selfSharedLockShift = 0;
            _lockSelfSharedFlag = new byte[rules.GetLockLevelCount()];
            _lockSwitchMatrix = new MatrPair[rules.GetLockLevelCount()][];
            _lockSwitchDictionary = new IReadOnlyDictionary<int, MatrPair>[rules.GetLockLevelCount()];
            _lockSwitchBackDictionary = new IReadOnlyDictionary<int, MatrPair>[rules.GetLockLevelCount()];
            _lockEscalationMatrix =  new MatrPair[rules.GetLockLevelCount(), rules.GetLockLevelCount()][];
            _singleLockSwitchPaths = new MatrPair[rules.GetLockLevelCount()];
            var allLockLevels = Enumerable.Range(0, rules.GetLockLevelCount()).ToArray();
            foreach (var lockLevel in allLockLevels)
            {
                _lockEscalationMatrix[lockLevel,lockLevel] = new MatrPair[0];
            }
            
            var allPossibleStates = Enumerable.Range(0, 1 << (rules.GetLockLevelCount())).ToList();
            var powersOf2 = allLockLevels.Select(k => 1 << k).ToArray();
            var twoBitMatrix = allLockLevels
                         .Join(allLockLevels, k => true, k => true, (o, i) => new {o, i})
                         .Where(k => k.o != k.i)
                         .Select(k => new {ent =(byte) k.i, ex = (byte)k.o, bits =(1<< k.i) |(1<< k.o)})
                         .ToArray();
            for (byte i = 0b0; i < rules.GetLockLevelCount(); i++)
            {               
                if (rules.AreShared(i, i))
                {
                    _lockSelfSharedFlag[i] = selfSharedLockShift++;                    
                }
                else
                {
                    _lockSelfSharedFlag[i] = 0xFF;
                }
                for (byte j = 0b0; j < rules.GetLockLevelCount(); j++)
                {
                    if (!rules.AreShared(i, j) && i != j)
                    {
                        var mask = (1 << i) | (1 << j);
                        allPossibleStates.RemoveAll(k => (k & mask) == mask);
                    }
                }
            }

            var possibleLockPath = allPossibleStates
                .Join(allPossibleStates, _ => true, _ => true, (o, i) =>new{entrance =(uint)( i),exit = (uint)(o), difference = o ^ i})
                .Where(k=> k.exit > k.entrance && powersOf2.Contains(k.difference))             
                .ToArray();

            var possibleEscalationPath = allPossibleStates
                .Join(allPossibleStates, k => k!=0, k => k != 0, (o, i) => new { entrance = (uint)(i), exit = (uint)(o), difference = o ^ i })
                .Join(twoBitMatrix,k=>k.difference,k=>k.bits,(o,i)=>new {  o.entrance, o.exit,escState1 = (uint)i.ent,escState2 = (uint)i.ex})
                .Where(k=>k.entrance>k.exit && k.escState1>k.escState2 || k.entrance < k.exit && k.escState1 < k.escState2)
                .ToArray();

            var additionalPathsForSelfSharedLocks = Enumerable.Range(0, rules.GetLockLevelCount())
                .Where(k => rules.AreShared((byte) k, (byte) k))
                .Join(allPossibleStates, _ => true, _ => true,
                    (o, i) => new { entrance = (uint)i, exit = (uint)i | SharenessCheckLock, difference = i & (1 << o)})
                .Where(k => k.difference != 0)
                .ToArray();

            foreach (var pair in possibleLockPath.Concat(additionalPathsForSelfSharedLocks).GroupBy(k => Array.FindIndex(powersOf2, k2 => k2 == k.difference)))
            {
                _lockSwitchMatrix[pair.Key] = pair.Select(k => new MatrPair(k.entrance, k.exit)).OrderBy(k=>k.BlockEntrance).ToArray();
                if (pair.Count() == 1)
                {
                    _singleLockSwitchPaths[pair.Key] = pair.Select(k => new MatrPair(k.entrance, k.exit))                       
                        .Single();
                }
                else
                {
                    _lockSwitchDictionary[pair.Key] = pair
                        .Select(k => new MatrPair(k.entrance, k.exit))
                        .ToDictionary(k => (int) k.BlockEntrance);
                    _lockSwitchBackDictionary[pair.Key] = pair
                        .Select(k => new MatrPair(k.entrance, k.exit))
                        .ToDictionary(k => (int) k.BlockExit);
                }
            }
           
            
            foreach (var pair in possibleEscalationPath.GroupBy(k=>k.escState1))
               foreach (var pair2 in pair.GroupBy(k => k.escState2))
            {
                _lockEscalationMatrix[pair.Key,pair2.Key] = pair2.Select(k => new MatrPair(k.entrance, k.exit)).ToArray();
            }

            SelfSharedLocks = selfSharedLockShift;                          
        }

    }
}
