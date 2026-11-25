using CoreMetrics;
using Meters;
using Rationalz;
namespace Meters_Test;

/// <summary>
/// Tests for ModifierGroup, the bookkeeping for one category of ModInstances.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class ModifierGroupTests
{
    [Fact(DisplayName = "A new group has no modifiers and a sum of exactly zero")]
    public void StartsEmpty()
    {
        ModifierGroup<int, int> group = new();

        Assert.Empty(group.Modifiers);
        Assert.Equal(Rational<long>.Zero, group.Sum);
        Assert.Equal(Rational<long>.Zero, group.MaxMagnitude);
    }
    [Fact(DisplayName = "Adding to a group gives the exact sum, including modifiers that take away")]
    public void AddGivesTheExactSum()
    {
        ModifierGroup<int, int> group = new();

        group.Add(new ModInstance<int, int>(1, 3));
        group.Add(new ModInstance<int, int>(-1, 4));

        Assert.Equal(2, group.Modifiers.Count);
        Assert.Equal(new Rational<long>(1, 12), group.Sum);
    }
    [Fact(DisplayName = "The strongest modifier in a group is the one with the largest size, whichever way it points")]
    public void StrongestIsBySize()
    {
        ModifierGroup<int, int> group = new();

        group.Add(new ModInstance<int, int>(1, 5));
        group.Add(new ModInstance<int, int>(-7, 3));
        group.Add(new ModInstance<int, int>(2, 25));

        Assert.Equal(new Rational<long>(7, 3), group.MaxMagnitude);
        Assert.Equal(3, group.FastestTickRateAtMax);
    }
    [Fact(DisplayName = "When two modifiers in a group are equally strong, the one that ticks more often is the fastest, whichever was added first")]
    public void TieGoesToTheFastest()
    {
        ModifierGroup<int, int> slowFirst = new();
        slowFirst.Add(new ModInstance<int, int>(20, 2));
        slowFirst.Add(new ModInstance<int, int>(10, 1));
        ModifierGroup<int, int> fastFirst = new();
        fastFirst.Add(new ModInstance<int, int>(10, 1));
        fastFirst.Add(new ModInstance<int, int>(20, 2));

        Assert.Equal(1, slowFirst.FastestTickRateAtMax);
        Assert.Equal(1, fastFirst.FastestTickRateAtMax);
    }
    [Fact(DisplayName = "Removing the strongest modifier from a group makes the next strongest the strongest")]
    public void RemovingTheStrongestFindsTheNext()
    {
        ModifierGroup<int, int> group = new();
        group.Add(new ModInstance<int, int>(1, 5));
        ModifierKey strongest = group.Add(new ModInstance<int, int>(-7, 3));
        group.Add(new ModInstance<int, int>(2, 4));

        Assert.True(group.Remove(strongest));

        Assert.Equal(new Rational<long>(1, 2), group.MaxMagnitude);
        Assert.Equal(4, group.FastestTickRateAtMax);
        Assert.Equal(new Rational<long>(7, 10), group.Sum);
    }
    [Fact(DisplayName = "Removing everything from a group brings its sum back to exactly zero")]
    public void RemovingEverythingGivesZero()
    {
        ModifierGroup<int, int> group = new();
        ModifierKey first = group.Add(new ModInstance<int, int>(1, 3));
        ModifierKey second = group.Add(new ModInstance<int, int>(-1, 7));
        ModifierKey third = group.Add(new ModInstance<int, int>(5, 11));

        group.Remove(second);
        group.Remove(first);
        group.Remove(third);

        Assert.Empty(group.Modifiers);
        Assert.Equal(Rational<long>.Zero, group.Sum);
        Assert.Equal(Rational<long>.Zero, group.MaxMagnitude);
    }
    [Fact(DisplayName = "Replacing a modifier in a group keeps its key and changes the sum and the strongest")]
    public void ReplaceKeepsTheKey()
    {
        ModifierGroup<int, int> group = new();
        ModifierKey key = group.Add(new ModInstance<int, int>(3, 5));
        group.Add(new ModInstance<int, int>(1, 5));

        Assert.True(group.Replace(key, new ModInstance<int, int>(-9, 2)));

        Assert.Equal(new ModInstance<int, int>(-9, 2), group.Modifiers[key]);
        Assert.Equal(new Rational<long>(-43, 10), group.Sum);
        Assert.Equal(new Rational<long>(9, 2), group.MaxMagnitude);
        Assert.Equal(2, group.FastestTickRateAtMax);
    }
    [Fact(DisplayName = "Removing or replacing with a key a group doesn't have changes nothing and says so")]
    public void UnknownKeyChangesNothing()
    {
        ModifierGroup<int, int> group = new();
        group.Add(new ModInstance<int, int>(3, 5));

        Assert.False(group.Remove(new ModifierKey(999)));
        Assert.False(group.Replace(new ModifierKey(999), new ModInstance<int, int>(8, 2)));

        Assert.Single(group.Modifiers);
        Assert.Equal(new Rational<long>(3, 5), group.Sum);
    }
    [Fact(DisplayName = "A group refuses a modifier that would make its sum impossible to store exactly, and changes nothing")]
    public void AddThatCannotBeExactThrows()
    {
        ModifierGroup<int, int> group = new();
        //Three tick rates with nothing in common, so the total needs a denominator of all three multiplied together
        group.Add(new ModInstance<int, int>(1, 9999991));
        group.Add(new ModInstance<int, int>(1, 9999990));
        Rational<long> sumBefore = group.Sum;

        Assert.Throws<OverflowException>(() => group.Add(new ModInstance<int, int>(1, 9999989)));

        Assert.Equal(2, group.Modifiers.Count);
        Assert.Equal(sumBefore, group.Sum);
    }
    [Fact(DisplayName = "Clearing a group removes everything at once, and the keys it gave before stop working and are never given again")]
    public void ClearRemovesEverythingAndRetiresTheKeys()
    {
        ModifierGroup<int, int> group = new();
        ModifierKey first = group.Add(new ModInstance<int, int>(1, 3));
        ModifierKey second = group.Add(new ModInstance<int, int>(-7, 2));

        group.Clear();

        Assert.Empty(group.Modifiers);
        Assert.Equal(Rational<long>.Zero, group.Sum);
        Assert.Equal(Rational<long>.Zero, group.MaxMagnitude);
        Assert.False(group.Remove(first));
        Assert.False(group.Replace(second, new ModInstance<int, int>(8, 2)));

        ModifierKey afterClear = group.Add(new ModInstance<int, int>(2, 5));

        Assert.NotEqual(first, afterClear);
        Assert.NotEqual(second, afterClear);
        Assert.Equal(new Rational<long>(2, 5), group.Sum);
        Assert.Equal(5, group.FastestTickRateAtMax);
    }
    [Fact(DisplayName = "A group made without a counter is group 0, and groups made with one counter each get their own number starting from 1")]
    public void GroupsAreNumberedByTheirCounter()
    {
        ModifierGroupCounter counter = new();
        ModifierGroup<int, int> alone = new();
        ModifierGroup<int, int> first = new(counter);
        ModifierGroup<int, int> second = new(counter);

        Assert.Equal(0, alone.GroupNumber);
        Assert.Equal(1, first.GroupNumber);
        Assert.Equal(2, second.GroupNumber);
        Assert.Equal(2, counter.GroupsMade);
        Assert.Equal(new ModifierKey(0), alone.Add(new ModInstance<int, int>(1, 3)));
        Assert.Equal(new ModifierKey(0, 1), first.Add(new ModInstance<int, int>(1, 3)));
        Assert.Equal(new ModifierKey(0, 2), second.Add(new ModInstance<int, int>(1, 3)));
    }
    [Fact(DisplayName = "A key from one group does nothing on another group made with the same counter, even though both gave it as their first key")]
    public void KeysFromAnotherGroupDoNothing()
    {
        ModifierGroupCounter counter = new();
        ModifierGroup<int, int> first = new(counter);
        ModifierGroup<int, int> second = new(counter);
        ModifierKey fromFirst = first.Add(new ModInstance<int, int>(1, 3));
        ModifierKey fromSecond = second.Add(new ModInstance<int, int>(-7, 2));

        Assert.NotEqual(fromFirst, fromSecond);
        Assert.False(second.Remove(fromFirst));
        Assert.False(first.Replace(fromSecond, new ModInstance<int, int>(8, 2)));

        Assert.Equal(new Rational<long>(1, 3), first.Sum);
        Assert.Equal(new Rational<long>(-7, 2), second.Sum);
        Assert.True(first.Remove(fromFirst));
        Assert.True(second.Remove(fromSecond));
    }
    [Fact(DisplayName = "Cloned groups keep their numbers whatever order they are cloned in, without counting as new groups on the original's counter or its clone")]
    public void ClonesKeepTheirGroupNumbers()
    {
        ModifierGroupCounter counter = new();
        ModifierGroup<int, int> first = new(counter);
        ModifierGroup<int, int> second = new(counter);
        ModifierKey firstKey = first.Add(new ModInstance<int, int>(3, 5));
        ModifierKey secondKey = second.Add(new ModInstance<int, int>(-7, 2));
        CloneContext context = new();

        //The other way round from how they were made
        ModifierGroup<int, int> secondClone = context.GetOrClone(second);
        ModifierGroup<int, int> firstClone = context.GetOrClone(first);
        ModifierGroupCounter counterClone = context.GetOrClone(counter);

        Assert.Equal(1, firstClone.GroupNumber);
        Assert.Equal(2, secondClone.GroupNumber);
        Assert.Equal(2, counter.GroupsMade);
        Assert.Equal(2, counterClone.GroupsMade);
        Assert.False(firstClone.Remove(secondKey));
        Assert.True(firstClone.Remove(firstKey));
        Assert.True(secondClone.Remove(secondKey));
        Assert.Single(first.Modifiers);
        Assert.Single(second.Modifiers);
    }
    [Fact(DisplayName = "A cloned counter carries on from the same count as its original, and neither changes the other")]
    public void ClonedCounterCountsSeparately()
    {
        ModifierGroupCounter counter = new();
        ModifierGroup<int, int> before = new(counter);
        CloneContext context = new();

        ModifierGroupCounter counterClone = context.GetOrClone(counter);

        Assert.NotSame(counter, counterClone);
        Assert.Same(counterClone, context.GetOrClone(counter));
        ModifierGroup<int, int> onOriginal = new(counter);
        ModifierGroup<int, int> onClone = new(counterClone);
        ModifierGroup<int, int> anotherOnClone = new(counterClone);
        Assert.Equal(2, onOriginal.GroupNumber);
        Assert.Equal(2, onClone.GroupNumber);
        Assert.Equal(3, anotherOnClone.GroupNumber);
        Assert.Equal(2, counter.GroupsMade);
        Assert.Equal(3, counterClone.GroupsMade);
        Assert.Equal(1, before.GroupNumber);
    }
    [Fact(DisplayName = "A RateManagedMeter given a counter puts its group's number in its keys, and one given none uses group 0")]
    public void MeterUsesTheCounterIfGiven()
    {
        Timeline.Timeline<int> timeline = new();
        ModifierGroupCounter counter = new();
        RateManagedMeter<int, int> plain = new(timeline, 0, -1000, 1000);
        RateManagedMeter<int, int> first = new(timeline, 0, -1000, 1000, counter);
        RateManagedMeter<int, int> second = new(timeline, 0, -1000, 1000, counter);

        ModifierKey plainKey = plain.Add(new ModInstance<int, int>(1, 3));
        ModifierKey firstKey = first.Add(new ModInstance<int, int>(1, 3));
        ModifierKey secondKey = second.Add(new ModInstance<int, int>(1, 3));

        Assert.Equal(new ModifierKey(0), plainKey);
        Assert.Equal(new ModifierKey(0, 1), firstKey);
        Assert.Equal(new ModifierKey(0, 2), secondKey);
        Assert.False(second.Remove(firstKey));
        Assert.True(first.Remove(firstKey));
    }
    [Fact(DisplayName = "A cloned group has the same modifiers under the same keys, is the one clone the context gives every time, and the two change separately afterwards")]
    public void CloneMatchesAndThenIsSeparate()
    {
        ModifierGroup<int, int> original = new();
        ModifierKey kept = original.Add(new ModInstance<int, int>(3, 5));
        ModifierKey removedFromClone = original.Add(new ModInstance<int, int>(-7, 3));
        CloneContext context = new();

        ModifierGroup<int, int> clone = context.GetOrClone(original);

        Assert.NotSame(original, clone);
        Assert.Same(clone, context.GetOrClone(original));
        Assert.Equal(original.Sum, clone.Sum);
        Assert.Equal(original.MaxMagnitude, clone.MaxMagnitude);
        Assert.Equal(original.FastestTickRateAtMax, clone.FastestTickRateAtMax);
        Assert.Equal(new ModInstance<int, int>(3, 5), clone.Modifiers[kept]);

        Assert.True(clone.Remove(removedFromClone));
        ModifierKey addedToClone = clone.Add(new ModInstance<int, int>(1, 2));
        ModifierKey addedToOriginal = original.Add(new ModInstance<int, int>(1, 4));

        Assert.Equal(3, original.Modifiers.Count);
        Assert.Equal(2, clone.Modifiers.Count);
        //Both carried on from the same point, so the next key each gives is the same one
        Assert.Equal(addedToOriginal, addedToClone);
    }
}
