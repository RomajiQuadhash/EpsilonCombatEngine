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
    [Fact(DisplayName = "A group copied from another has the same modifiers under the same keys, and the two change separately afterwards")]
    public void CopyFromMatchesAndThenIsSeparate()
    {
        ModifierGroup<int, int> original = new();
        ModifierKey kept = original.Add(new ModInstance<int, int>(3, 5));
        ModifierKey removedFromCopy = original.Add(new ModInstance<int, int>(-7, 3));
        ModifierGroup<int, int> copy = new();
        copy.Add(new ModInstance<int, int>(100, 1)); //Copying replaces whatever was there

        copy.CopyFrom(original);

        Assert.Equal(original.Sum, copy.Sum);
        Assert.Equal(original.MaxMagnitude, copy.MaxMagnitude);
        Assert.Equal(original.FastestTickRateAtMax, copy.FastestTickRateAtMax);
        Assert.Equal(new ModInstance<int, int>(3, 5), copy.Modifiers[kept]);

        Assert.True(copy.Remove(removedFromCopy));
        ModifierKey addedToCopy = copy.Add(new ModInstance<int, int>(1, 2));
        ModifierKey addedToOriginal = original.Add(new ModInstance<int, int>(1, 4));

        Assert.Equal(3, original.Modifiers.Count);
        Assert.Equal(2, copy.Modifiers.Count);
        //Both carried on from the same point, so the next key each gives is the same one
        Assert.Equal(addedToOriginal, addedToCopy);
    }
}
