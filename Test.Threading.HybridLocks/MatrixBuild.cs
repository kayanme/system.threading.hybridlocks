using System.Linq;
using FakeItEasy;
using System.IO.Paging.PhysicalLevel.Classes;
using System.IO.Paging.PhysicalLevel.Configuration.Builder;

using NUnit.Framework;

namespace Test.Paging.PhysicalLevel.Locks
{
    [TestFixture]
    public class MatrixBuild
    {
        
        public void SimpleLockRule()
        {
            var r = A.Fake<LockRuleset>();
            A.CallTo(() => r.GetLockLevelCount()).Returns<byte>(1);
            A.CallTo(() => r.AreShared(0, 0)).Returns(false);

            var matrix = new LockMatrix(r);

            Assert.IsFalse(matrix.IsSelfShared(0));
            var res = matrix.EntrancePairs(0).ToArray();
            var exp = new[] { new LockMatrix.MatrPair(0, 0b1) };
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(0, 0).ToArray();
            exp = new LockMatrix.MatrPair[0];
            CollectionAssert.AreEquivalent(exp, res);
        }


        [Test]
        public void SimpleLockRule_WithSelfShared()
        {
            var r = A.Fake<LockRuleset>();
            A.CallTo(() => r.GetLockLevelCount()).Returns<byte>(1);
            A.CallTo(() => r.AreShared(0, 0)).Returns(true);

            var matrix = new LockMatrix(r);

            Assert.IsTrue(matrix.IsSelfShared(0));
            var res = matrix.EntrancePairs(0).ToArray();
            var exp = new[] { new LockMatrix.MatrPair(0, 0b1),
                              new LockMatrix.MatrPair(0b1, LockMatrix.SharenessCheckLock | 0b1) };
            CollectionAssert.AreEquivalent(exp, res);
        }


        [Test]
        public void Reader_Writer_LockScheme()
        {
            var r = A.Fake<LockRuleset>();
            A.CallTo(() => r.GetLockLevelCount()).Returns<byte>(2);
            A.CallTo(() => r.AreShared(0, 0)).Returns(true);
            A.CallTo(() => r.AreShared(1, 0)).Returns(false);
            A.CallTo(() => r.AreShared(0, 1)).Returns(false);
            A.CallTo(() => r.AreShared(1, 1)).Returns(false);

            var matrix = new LockMatrix(r);

            Assert.IsTrue(matrix.IsSelfShared(0));
            var res = matrix.EntrancePairs(0).ToArray();
            var exp = new[] {
                                    new LockMatrix.MatrPair(0, 0b1),
                                    new LockMatrix.MatrPair(0b1, LockMatrix.SharenessCheckLock | 0b1) };
            CollectionAssert.AreEquivalent(exp, res);

            Assert.IsFalse(matrix.IsSelfShared(1));
            res = matrix.EntrancePairs(1).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0, 0b10) };
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(0, 1).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b1, 0b10) };
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(1, 0).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b10, 0b1) };
            CollectionAssert.AreEquivalent(exp, res);

        }

        [Test]
        public void ThreeLocks_TwoPairsShared_OneUnshared()
        {
            var r = A.Fake<LockRuleset>();
            A.CallTo(() => r.GetLockLevelCount()).Returns<byte>(3);
            A.CallTo(() => r.AreShared(0, 0)).Returns(false);
            A.CallTo(() => r.AreShared(1, 0)).Returns(true);
            A.CallTo(() => r.AreShared(0, 1)).Returns(true);
            A.CallTo(() => r.AreShared(1, 1)).Returns(false);
            A.CallTo(() => r.AreShared(1, 2)).Returns(true);
            A.CallTo(() => r.AreShared(2, 1)).Returns(true);
            A.CallTo(() => r.AreShared(2, 2)).Returns(false);
            A.CallTo(() => r.AreShared(0, 2)).Returns(false);
            A.CallTo(() => r.AreShared(2, 0)).Returns(false);

            var matrix = new LockMatrix(r);

            Assert.IsFalse(matrix.IsSelfShared(0));
            var res = matrix.EntrancePairs(0).ToArray();
            var exp = new[] { new LockMatrix.MatrPair(0,    0b001),
                              new LockMatrix.MatrPair(0b10, 0b011) };
            CollectionAssert.AreEquivalent(exp, res);

            Assert.IsFalse(matrix.IsSelfShared(1));
            res = matrix.EntrancePairs(1).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0,     0b010),
                          new LockMatrix.MatrPair(0b001, 0b011),
                          new LockMatrix.MatrPair(0b100, 0b110)};
            CollectionAssert.AreEquivalent(exp, res);

            Assert.IsFalse(matrix.IsSelfShared(2));
            res = matrix.EntrancePairs(2).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0, 0b100),
                new LockMatrix.MatrPair(0b010, 0b110)};
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(0, 1).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b1, 0b10) };
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(1, 2).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b10, 0b100) };
            CollectionAssert.AreEquivalent(exp, res);


            res = matrix.EscalationPairs(0, 2).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b1, 0b100), new LockMatrix.MatrPair(0b11, 0b110) };
            CollectionAssert.AreEquivalent(exp, res);

            res = matrix.EscalationPairs(2, 0).ToArray();
            exp = new[] { new LockMatrix.MatrPair(0b100, 0b1), new LockMatrix.MatrPair(0b110, 0b11) };
            CollectionAssert.AreEquivalent(exp, res);

        }

    }
}
