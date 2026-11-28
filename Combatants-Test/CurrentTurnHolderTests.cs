using Combatants;
using CoreMetrics;
using Meters;
using Rationalz;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Combatants_Test;

/// <summary>
/// Tests for CurrentTurnHolder, the one thing shared across a combat that says whose turn it is: claiming the turn, clearing it, and cloning.
/// </summary>
/// <remarks>Claude written, Romaji approved</remarks>
public class CurrentTurnHolderTests
{
    #region Claiming and clearing
    [Fact(DisplayName = "A new turn holder has nobody's turn, and claiming it makes it the claimer's turn")]
    public void ClaimMakesItTheClaimersTurn()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        Assert.Null(holder.Current);

        holder.Claim(combatant);

        Assert.Same(combatant, holder.Current);
    }
    [Fact(DisplayName = "A turn that is already claimed can't be claimed again, by someone else or by the same one, and stays with whoever had it")]
    public void ClaimedTurnCannotBeClaimedAgain()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> firstDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> firstHealth = new(timeline, 100, 0, 100, firstDefense, new ModifierGroupCounter());
        BaseCombatant<int> first = new(firstHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> secondDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> secondHealth = new(timeline, 100, 0, 100, secondDefense, new ModifierGroupCounter());
        BaseCombatant<int> second = new(secondHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        holder.Claim(first);

        Assert.Throws<TurnClashException>(() => holder.Claim(second));
        Assert.Throws<TurnClashException>(() => holder.Claim(first));

        Assert.Same(first, holder.Current);
    }
    [Fact(DisplayName = "Clearing a claimed turn leaves it nobody's, so that someone else can claim it")]
    public void ClearLetsSomeoneElseClaim()
    {
        Timeline<int> timeline = new();
        MaxStatAggregator<int, SpeedMeter<int>> speeds = new();
        StatModificationMeter<int> firstDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> firstHealth = new(timeline, 100, 0, 100, firstDefense, new ModifierGroupCounter());
        BaseCombatant<int> first = new(firstHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        StatModificationMeter<int> secondDefense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> secondHealth = new(timeline, 100, 0, 100, secondDefense, new ModifierGroupCounter());
        BaseCombatant<int> second = new(secondHealth, speeds, 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.EnemyKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        holder.Claim(first);

        holder.Clear();

        Assert.Null(holder.Current);
        holder.Claim(second);
        Assert.Same(second, holder.Current);
    }
    [Fact(DisplayName = "A turn that nobody has claimed can't be cleared, whether it never was claimed or was already cleared")]
    public void UnclaimedTurnCannotBeCleared()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();

        Assert.Throws<TurnClashException>(() => holder.Clear());

        holder.Claim(combatant);
        holder.Clear();
        Assert.Throws<TurnClashException>(() => holder.Clear());
        Assert.Null(holder.Current);
    }
    #endregion
    #region Cloning
    [Fact(DisplayName = "A cloned turn holder holds the clone of whoever held the turn, and clearing the clone leaves the original claimed")]
    public void CloneHoldsTheClonedCombatant()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        holder.Claim(combatant);

        CloneContext context = new();
        timeline.Clone(context);
        context.GetOrClone(combatant);
        CurrentTurnHolder<AbstractCombatant<int>> holderClone = context.GetOrClone(holder);
        timeline.RewireAllEvents(context);
        Assert.True(context.TryGet<AbstractCombatant<int>>(combatant, out AbstractCombatant<int> combatantClone));

        Assert.NotSame(holder, holderClone);
        Assert.Same(holderClone, context.GetOrClone(holder));
        Assert.Same(combatantClone, holderClone.Current);
        Assert.NotSame(combatant, holderClone.Current);

        holderClone.Clear();

        Assert.Null(holderClone.Current);
        Assert.Same(combatant, holder.Current);
    }
    [Fact(DisplayName = "A cloned turn holder clones whoever held the turn if that hasn't been done yet")]
    public void CloneClonesTheCombatantIfNeeded()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        holder.Claim(combatant);

        CloneContext context = new();
        timeline.Clone(context);
        CurrentTurnHolder<AbstractCombatant<int>> holderClone = context.GetOrClone(holder);
        timeline.RewireAllEvents(context);

        Assert.True(context.TryGet<AbstractCombatant<int>>(combatant, out AbstractCombatant<int> combatantClone));
        Assert.Same(combatantClone, holderClone.Current);
    }
    [Fact(DisplayName = "A turn holder that nobody has claimed clones to one that nobody has claimed, which can then be claimed without touching the original")]
    public void EmptyHolderClonesEmpty()
    {
        Timeline<int> timeline = new();
        StatModificationMeter<int> defense = new(timeline, 10, new Rational<long>(-6), new Rational<long>(6));
        HealthMeter<int> health = new(timeline, 100, 0, 100, defense, new ModifierGroupCounter());
        BaseCombatant<int> combatant = new(health, new MaxStatAggregator<int, SpeedMeter<int>>(), 10, new Rational<long>(-6), new Rational<long>(6), PriorityRank.PlayerKO);
        CurrentTurnHolder<AbstractCombatant<int>> holder = new();
        CloneContext context = new();

        CurrentTurnHolder<AbstractCombatant<int>> holderClone = context.GetOrClone(holder);

        Assert.Null(holderClone.Current);
        holderClone.Claim(combatant);
        Assert.Null(holder.Current);
    }
    #endregion
}
