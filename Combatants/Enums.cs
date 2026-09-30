namespace Combatants
{
    /// <summary>
    /// Meters that a combatant could reasonably have but doesn't have to (unlike Health which all need to have)
    /// </summary>
    public enum MeterTypes
    {
        Mana, //possibly player only?
        Shield, //This meter determines if the shield stat should be applied along with defense. Any DOT should subtract from this value when it starts, then return to the prior level after the DOT ends "gradually" (definine a constant for how long the heal should take). If the value is at 0, then you can break the shield and set the min to "negative infinity" and just do the rest normally. Once the HP climbs back above 0, set the minimum back to 0 and the break ends. This should exist on all but test combatants probably
        IncomingDamage, //Might not be needed but it could be useful to split this off from Health, to make health a simpler meter and just use stat/shield changes and this to change the HP drain. Or maybe this will be merged into the HP meter, possibly with shield

        //Player Only
        Vibe, //controls when a set of actions can be taken, represents a mood

        //AI Only
        Charge, //built up and spent on stronger attacks
        Progression, //built up over a battle, represents phase changes
    }
    /// <summary>
    /// Special flags not common enough to be hard coded in (like KO, Dead, or Bleeding). Events should set or unset these
    /// Note that stat changes are not Status Effects and they should be things that don't need extra information.
    /// </summary>
    public enum StatusEffects
    {
       Sleep, //TODO: Decide the mechanics around waking
       Silence, //No MP can be used 
       Unaware, //Can't start new actions until Unaware is cleared
       Intangible, //Can't be dealt damage
       Nullified, //Can't deal damage
       Paused, //Doesn't do anything while paused, continues afterwards
    }
    /// <summary>
    /// These are the stats that can be scaled by buffs/debuffs by the "stages" stat buff/debuff system.
    /// This means Health and Mana are not included since applying a multiplicative buff to those gets messy
    /// </summary>
    public enum CombatStats
    {
        Attack, //Note that this is for all attacks, not just physical
        Defense, //Same here, there's no separate Resistance to psychic
        ShieldResistance, //This controls how much attacks do to the shield not the amount it contributes to defense or the max of the Shield meter. This would likely be shown to players as just "shield"
        Speed,
        AccuracyCrit, //Note that since the closest to "crits" is direct hits, influenced by accuracy, so the "crit" is there to remind of that
        Evasion
    }
}