using Meters;
using Timeline.OkazoOptionalProperties;
namespace Meters_Test;

/// <summary>
/// Tests for DiscreteTransitionData, which says how a discrete meter crossed a threshold so that crossings at the same moment can be put in order.
/// The plain TransitionData it is built on is tested in Timeline-Test.
/// </summary>
/// <remarks>Claude written, Romaji Approved</remarks>
public class DiscreteTransitionDataTests
{
    #region Making one
    [Fact(DisplayName = "DiscreteTransitionData keeps its tick rate and whether it had just ticked")]
    public void KeepsTickRateAndJustTicked()
    {
        DiscreteTransitionData<int, int> ticked = new(0, 102, 5, 100, 100, 10, true);
        DiscreteTransitionData<int, int> notTicked = new(10, 107, 5, 100, 100, 4, false);

        Assert.Equal(10, ticked.TickRate);
        Assert.True(ticked.JustTicked);
        Assert.Equal(4, notTicked.TickRate);
        Assert.False(notTicked.JustTicked);
    }
    [Fact(DisplayName = "When it had just ticked, the tick counts as part of the jump over the threshold")]
    public void ATickCountsAsPartOfTheJump()
    {
        //97 before, and a tick of 5 took it to 102, past a threshold of 100
        DiscreteTransitionData<int, int> data = new(0, 102, 5, 100, 100, 10, true);

        Assert.Equal(2, data.After);
        Assert.Equal(-3, data.Before);
        Assert.Equal(5, data.ProgressiveChange);
        Assert.Equal(0, data.InstantChange); //But it isn't an instant change
    }
    [Fact(DisplayName = "When it hadn't just ticked, only the instant change counts as the jump")]
    public void WithoutATickOnlyTheInstantChangeCounts()
    {
        //97 before, and an instant change of 10 took it to 107
        DiscreteTransitionData<int, int> data = new(10, 107, 5, 100, 100, 10, false);

        Assert.Equal(7, data.After);
        Assert.Equal(-3, data.Before);
        Assert.Equal(10, data.InstantChange);
    }
    [Fact(DisplayName = "The instant change is kept as a size, whichever way the meter was going")]
    public void InstantChangeIsASize()
    {
        //103 before, and an instant change of -10 took it to 93, down past a threshold of 100
        DiscreteTransitionData<int, int> data = new(-10, 93, 0, 100, 100, 10, false);

        Assert.Equal(10, data.InstantChange);
        Assert.Equal(7, data.After);
        Assert.Equal(-3, data.Before);
    }
    [Fact(DisplayName = "An instant change that went against the tick that crossed the threshold is kept as negative")]
    public void InstantChangeAgainstTheTickIsNegative()
    {
        //The tick of 5 took it over, even though an instant change of -3 at the same moment pulled the other way
        DiscreteTransitionData<int, int> against = new(-3, 102, 5, 100, 100, 10, true);
        //Here the instant change of 3 and the tick of 5 went the same way
        DiscreteTransitionData<int, int> with = new(3, 108, 5, 100, 100, 10, true);

        Assert.Equal(-3, against.InstantChange);
        Assert.Equal(3, with.InstantChange);
    }
    [Fact(DisplayName = "DiscreteTransitionData can't be made with a tick rate of zero or less")]
    public void TickRateMustBePositive()
    {
        Assert.Throws<ArgumentException>(() => new DiscreteTransitionData<int, int>(0, 102, 5, 100, 100, 0, true));
        Assert.Throws<ArgumentException>(() => new DiscreteTransitionData<int, int>(0, 102, 5, 100, 100, -10, true));
    }
    [Fact(DisplayName = "DiscreteTransitionData can't be made for a tick and an instant change that cancelled each other out")]
    public void TickAndInstantChangeCannotCancelOut()
    {
        Assert.Throws<ArgumentException>(() => new DiscreteTransitionData<int, int>(-5, 100, 5, 100, 100, 10, true));
    }
    #endregion
    #region Putting them in order
    [Fact(DisplayName = "The crossing with the bigger instant change comes first")]
    public void BiggerInstantChangeFirst()
    {
        DiscreteTransitionData<int, int> bigger = new(10, 105, 0, 100, 100, 5, false);
        DiscreteTransitionData<int, int> smaller = new(4, 102, 0, 100, 100, 5, false);

        Assert.True(bigger.CompareTo(smaller) < 0);
        Assert.True(smaller.CompareTo(bigger) > 0);
    }
    [Fact(DisplayName = "The size of an instant change is judged against the range of its meter")]
    public void InstantChangeIsJudgedAgainstTheRange()
    {
        DiscreteTransitionData<int, int> tenOfAHundred = new(10, 105, 0, 100, 100, 5, false);
        DiscreteTransitionData<int, int> fifteenOfAThousand = new(15, 1010, 0, 1000, 1000, 5, false);

        Assert.True(tenOfAHundred.CompareTo(fifteenOfAThousand) < 0);
        Assert.True(fifteenOfAThousand.CompareTo(tenOfAHundred) > 0);
    }
    [Fact(DisplayName = "No instant change at all comes before an instant change that went against the tick")]
    public void NoInstantChangeBeatsOneAgainstTheTick()
    {
        DiscreteTransitionData<int, int> none = new(0, 102, 5, 100, 100, 10, true);
        DiscreteTransitionData<int, int> against = new(-3, 102, 5, 100, 100, 10, true);

        Assert.True(none.CompareTo(against) < 0);
        Assert.True(against.CompareTo(none) > 0);
    }
    [Fact(DisplayName = "With the same instant change, the crossing on the faster tick rate comes first")]
    public void FasterTickRateFirst()
    {
        DiscreteTransitionData<int, int> faster = new(10, 105, 0, 100, 100, 2, false);
        DiscreteTransitionData<int, int> slower = new(10, 105, 0, 100, 100, 5, false);

        Assert.True(faster.CompareTo(slower) < 0);
        Assert.True(slower.CompareTo(faster) > 0);
    }
    [Fact(DisplayName = "The instant change is looked at before the tick rate")]
    public void InstantChangeBeforeTickRate()
    {
        DiscreteTransitionData<int, int> biggerButSlower = new(10, 105, 0, 100, 100, 50, false);
        DiscreteTransitionData<int, int> smallerButFaster = new(4, 102, 0, 100, 100, 1, false);

        Assert.True(biggerButSlower.CompareTo(smallerButFaster) < 0);
    }
    [Fact(DisplayName = "With the same instant change and tick rate, the ordinary TransitionData rules decide")]
    public void FallsBackToTheOrdinaryRules()
    {
        //Both jumped 10, but one ended 8 past the threshold and the other only 3 past
        DiscreteTransitionData<int, int> furtherPast = new(10, 108, 0, 100, 100, 5, false);
        DiscreteTransitionData<int, int> lessPast = new(10, 103, 0, 100, 100, 5, false);

        Assert.True(furtherPast.CompareTo(lessPast) < 0);
        Assert.True(lessPast.CompareTo(furtherPast) > 0);
    }
    [Fact(DisplayName = "Two crossings that are the same in every way compare as equal")]
    public void IdenticalCrossingsAreEqual()
    {
        DiscreteTransitionData<int, int> first = new(10, 105, 3, 100, 100, 5, false);
        DiscreteTransitionData<int, int> second = new(10, 105, 3, 100, 100, 5, false);

        Assert.Equal(0, first.CompareTo(second));
    }
    [Fact(DisplayName = "Comparing against nothing throws")]
    public void ComparingToNullThrows()
    {
        DiscreteTransitionData<int, int> data = new(10, 105, 0, 100, 100, 5, false);

        Assert.Throws<ArgumentNullException>(() => data.CompareTo(null));
    }
    [Fact(DisplayName = "Comparing against plain TransitionData that has no rate of change uses the ordinary rules, whichever one is asked")]
    public void ComparingToPlainDataWithNoRate()
    {
        //A discrete meter jumped 10 and ended 7 past, and a meter that only jumps went 30 and ended 20 past.
        //7 of 10 is further past for its size than 20 of 30, so the discrete one comes first.
        DiscreteTransitionData<int, int> discrete = new(10, 107, 5, 100, 100, 10, false);
        TransitionData<int> plain = new(30, 80, 0, 100, 60);

        Assert.True(discrete.CompareTo(plain) < 0);
        Assert.True(plain.CompareTo(discrete) > 0);
    }
    [Fact(DisplayName = "Against plain TransitionData with no rate of change, the plain one comes first when it went further past for its size")]
    public void ComparingToPlainDataWithNoRateTheOtherWay()
    {
        DiscreteTransitionData<int, int> discrete = new(10, 103, 5, 100, 100, 10, false); //3 of 10
        TransitionData<int> plain = new(30, 80, 0, 100, 60);                               //20 of 30

        Assert.True(discrete.CompareTo(plain) > 0);
        Assert.True(plain.CompareTo(discrete) < 0);
    }
    [Fact(DisplayName = "Comparing against plain TransitionData that has a rate of change throws, whichever one is asked, since that meter should have been using the discrete kind")]
    public void ComparingToPlainDataWithARateThrows()
    {
        DiscreteTransitionData<int, int> discrete = new(0, 102, 5, 100, 100, 10, true);
        TransitionData<int> plain = new(0, 102, 5, 100, 100);

        Assert.Throws<InvalidOperationException>(() => discrete.CompareTo(plain));
        Assert.Throws<InvalidOperationException>(() => plain.CompareTo(discrete));
    }
    #endregion
}
