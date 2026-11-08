using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timeline.OkazoOptionalProperties;

namespace Timeline_Test
{
    /// <summary>
    /// Testing of TransitionData and its comparison logic, including constructor validation and normalization.
    /// </summary>
    /// <remarks>AI generated code but checked line by line by real human eyeballs and brain</remarks>
    public class TransitionDataTests
    {
        #region Constructor Validation
        [Fact(DisplayName = "Constructor throws when range is zero or negative")]
        public void ConstructorThrowsOnInvalidRange()
        {
            Assert.Throws<ArgumentException>(() => new TransitionData<int>(1, 2, 1, 0, 1));
            Assert.Throws<ArgumentException>(() => new TransitionData<int>(1, 2, 1, -5, 1));
        }

        [Fact(DisplayName = "Constructor throws when there is absolutely no change (instant and progressive are 0)")]
        public void ConstructorThrowsOnNoChange()
        {
            Assert.Throws<ArgumentException>(() => new TransitionData<int>(0, 10, 0, 10, 5));
        }

        [Fact(DisplayName = "Constructor throws when the after value didn't cross the threshold (moving up)")]
        public void ConstructorThrowsWhenNotCrossedUpward()
        {
            Assert.Throws<ArgumentException>(() => new TransitionData<int>(5, 8, 1, 10, 10));
        }

        [Fact(DisplayName = "Constructor throws when the after value didn't cross the threshold (moving down)")]
        public void ConstructorThrowsWhenNotCrossedDownward()
        {
            Assert.Throws<ArgumentException>(() => new TransitionData<int>(-5, -8, 1, 10, -10));
        }
        #endregion

        #region Normalization
        [Fact(DisplayName = "Constructor normalizes a positive instant change correctly")]
        public void ConstructorNormalizesPositiveChange()
        {
            TransitionData<int> data = new(5, 12, 2, 20, 10);
            Assert.Equal(2, data.After); // 12 - 10
            Assert.Equal(-3, data.Before); // 2 - 5
            Assert.Equal(2, data.ProgressiveChange);
            Assert.Equal(20, data.Range);
        }

        [Fact(DisplayName = "Constructor normalizes a negative instant change to be positive internally")]
        public void ConstructorNormalizesNegativeChange()
        {
            TransitionData<int> data = new(-5, -12, -2, 20, -10);
            Assert.Equal(2, data.After); // -(-12) - (-(-10)) = 12 - 10
            Assert.Equal(-3, data.Before); // 2 - 5
            Assert.Equal(2, data.ProgressiveChange); // -(-2)
            Assert.Equal(20, data.Range);
        }

        [Fact(DisplayName = "Constructor handles zero instant change by setting Before and After to zero")]
        public void ConstructorHandlesZeroInstantChange()
        {
            TransitionData<int> data = new(0, 15, 3, 20, 10);
            Assert.Equal(0, data.After);
            Assert.Equal(0, data.Before);
            Assert.Equal(3, data.ProgressiveChange);
        }
        #endregion

        #region Comparison (Overshoots)
        [Fact(DisplayName = "Overshoot beats no overshoot (in both directions)")]
        public void OvershootBeatsNoOvershoot()
        {
            TransitionData<int> overshoot = new(5, 15, 0, 20, 10);
            TransitionData<int> noOvershoot = new(0, 15, 5, 20, 10);

            Assert.Equal(-1, overshoot.CompareTo(noOvershoot));
            Assert.Equal(1, noOvershoot.CompareTo(overshoot));
        }

        [Fact(DisplayName = "When both overshoot, larger overshoot-to-change ratio wins")]
        public void BothOvershoot_LargerRatioWins()
        {
            TransitionData<int> a = new(10, 12, 0, 20, 10); // Ratio 2/10
            TransitionData<int> b = new(10, 11, 0, 20, 10); // Ratio 1/10

            Assert.Equal(-1, a.CompareTo(b));
            Assert.Equal(1, b.CompareTo(a));
        }

        [Fact(DisplayName = "When ratios are equal, larger absolute (scaled by the ranges) overshoot wins")]
        public void BothOvershoot_EqualRatioLargerOvershootWins()
        {
            TransitionData<int> a = new(20, 14, 0, 20, 10); // Ratio 4/20
            TransitionData<int> b = new(10, 12, 0, 20, 10); // Ratio 2/10

            Assert.Equal(-1, a.CompareTo(b));
            Assert.Equal(1, b.CompareTo(a));
        }

        [Fact(DisplayName = "When both overshoot and all else is equal, progressive change acts as tiebreaker")]
        public void BothOvershoot_ProgressiveChangeTiebreaker()
        {
            TransitionData<int> a = new(5, 15, 3, 20, 10);
            TransitionData<int> b = new(5, 15, 7, 20, 10);

            Assert.Equal(1, a.CompareTo(b));
            Assert.Equal(-1, b.CompareTo(a));
        }
        #endregion

        #region Comparison (No Overshoots)
        [Fact(DisplayName = "When neither overshoots, larger change (smaller Before value) wins")]
        public void NoOvershoot_LargerChangeWins()
        {
            // A: Instant 10, after 10. After = 0, Before = -10.
            TransitionData<int> a = new(10, 10, 0, 20, 10);

            // B: Instant 5, after 10. After = 0, Before = -5.
            TransitionData<int> b = new(5, 10, 0, 20, 10);

            Assert.Equal(-1, a.CompareTo(b));
            Assert.Equal(1, b.CompareTo(a));
        }

        [Fact(DisplayName = "When neither overshoots and Before values match, progressive change wins")]
        public void NoOvershoot_ProgressiveChangeTiebreaker()
        {
            TransitionData<int> a = new(0, 10, 3, 20, 10);
            TransitionData<int> b = new(0, 10, 8, 20, 10);

            Assert.Equal(1, a.CompareTo(b));
            Assert.Equal(-1, b.CompareTo(a));
        }
        #endregion

        #region Integer-Safe Cross-Range Scaling
        [Fact(DisplayName = "Cross-range comparison uses integer-safe math (no division) and scales correctly")]
        public void CrossRangeScaling_Integers()
        {
            // A: Range 100. Instant 10, after 20. After = 10, Before = 0. Ratio = 10/10 = 1.0
            TransitionData<int> a = new(10, 20, 0, 100, 10);

            // B: Range 200. Instant 20, after 40. After = 20, Before = 0. Ratio = 20/20 = 1.0
            TransitionData<int> b = new(20, 40, 0, 200, 20);

            // Since both have a ratio of 1.0, the cross-multiplication will be equal.
            // The tiebreaker is the absolute overshoot (scaled to the other's range).
            // A's overshoot (10) scaled to B's range (200) = 2000.
            // B's overshoot (20) scaled to A's range (100) = 2000.
            Assert.Equal(0, a.CompareTo(b));
            Assert.Equal(0, b.CompareTo(a));

            // C: Range 200, Instant 20, after 50. After = 30, Before = 10.
            TransitionData<int> c = new(20, 50, 0, 200, 20);

            // A has overshoot 10/100 (10%). c has overshoot 30/200 (15%).
            // c should win because its ratio (30/20) is larger than A's (10/10).
            Assert.Equal(1, a.CompareTo(c));
            Assert.Equal(-1, c.CompareTo(a));
        }

        [Fact(DisplayName = "Progressive change scaling uses integer-safe math across massive range differences")]
        public void CrossRangeScalingProgressive_Integers()
        {
            // Both have instant 0 (so Before = 0, After = 0).
            // A: Range 100, progressive 10. (10% of range)
            // B: Range 200, progressive 15. (7.5% of range)
            TransitionData<int> a = new(0, 10, 10, 100, 10);
            TransitionData<int> b = new(0, 10, 15, 200, 10);

            // A's progressive scaled to B's range = 10 * 200 = 2000
            // B's progressive scaled to A's range = 15 * 100 = 1500
            // 1500.CompareTo(2000) -> A comes first.
            Assert.Equal(-1, a.CompareTo(b));
            Assert.Equal(1, b.CompareTo(a));
        }

        [Fact(DisplayName = "Massive integer scaling doesn't overflow or lose precision")]
        public void CrossRangeScaling_MassiveIntegers()
        {
            // A: Range 10,000, overshoot 1. Ratio = 1/10,000
            TransitionData<int> a = new(10_000, 10_001, 0, 10_000, 1);

            // B: Range 10,000,000, overshoot 1001. Ratio = 1001/10,000,000 (approx 1/10,000)
            TransitionData<int> b = new(10_000_000, 10_001_001, 0, 10_000_000, 1);

            // Cross multiplication check:
            // A's overshoot (1) scaled to B's range = 1 * 10,000,000 = 10,000,000
            // B's overshoot (1001) scaled to A's range = 1001 * 10,000 = 10,010,000
            // B's ratio is slightly larger.
            Assert.Equal(1, a.CompareTo(b));
            Assert.Equal(-1, b.CompareTo(a));
        }
        #endregion
    }
}
