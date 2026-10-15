using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;
namespace Timeline_Test;

[TestClass]
public class TimelineCloneTests
{
    [Fact(DisplayName ="Timelines can only be cloned when not Terminated")]
    public void TerminatedTimelineCannotBeCloned()
    {
        Timeline<int> timeline = new();
        for (TerminationType terminationType = TerminationType.Escape; terminationType <= TerminationType.UnknownEnding; terminationType++)
        {
            timeline.Terminate(terminationType);
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.Clone(new CloneContext()));
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(TimelinePhase.Open));
        }
    }
    [Fact(DisplayName = "Timelines can only be cloned when Open")]
    public void TimelineCanOnlyBeClonedWhenOpen()
    {
        Timeline<int> timeline = new();
        for (TimelinePhase phase = TimelinePhase.Purged; phase <=TimelinePhase.Display; phase++) //Terminated is tested above
        {
            Assert.Throws<InvalidOperationException>(() => timeline.DebugChangePhase(phase));
            Assert.Throws<InvalidPhaseForActionException>(() => timeline.Clone(new CloneContext()));
        }
    }
    [Fact(DisplayName = "If a timeline already exists in the clone context, it is returned")]
    public void CloneTakesFromContext()
    {
        Timeline<int> timeline = new();
        Timeline<int> fakeClone = new();

        CloneContext context = new();
        context.Register(timeline, fakeClone);
        object clone = timeline.Clone(context);
        Assert.Same(fakeClone, clone);
        Assert.NotSame(timeline, clone);
    }
    [Fact(DisplayName="An empty timeline clones into a new empty timeline")]
    public void EmptyTimelineClones()
    {
        Timeline<int> timeline = new();
        CloneContext context = new();
        object clone = timeline.Clone(context);
        Assert.NotSame(timeline, clone);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            Assert.Empty(timelineClone.Events);
            Assert.Empty(timelineClone.PossibleEvents);
        }
        else
        {
            Assert.Fail("Clone is not of type Timeline<int>");
        }
    }
    [Fact(DisplayName ="Cloning a timeline twice gets the same reference")]
    public void EmptyEventsClones()
    {
        Timeline<int> timeline = new();
        CloneContext context = new();
        object clone1 = timeline.Clone(context);
        object clone2 = timeline.Clone(context);
        Assert.Same(clone1, clone2);
    }
    [Fact(DisplayName = "A timeline with Events clones into a new timeline with cloned events")]
    public void EventsClone()
    {
        Timeline<int> timeline = new();
        //Note, we need to only add events that are not Never, since those would be brought to PossibleEvents and that'll be tested later.
        //Added out of order since we don't want cloning to secretly sort them (even though that will happen when advancing)
        BaseCard<int> card1 = new(5, timeline);
        BaseCard<int> card2 = new(3, timeline);
        BaseCard<int> card3 = new(10, timeline);

        card2.HaltsInstantAction = true; //To test that property is cloned as well

        //Add them to the timeline
        timeline.AddEvent(card1);
        timeline.AddEvent(card2);
        timeline.AddEvent(card3);

        //Keep the context
        CloneContext context = new();
        object clone = timeline.Clone(context);
        Assert.NotSame(timeline, clone);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            //Check the subscribers of advance to make sure they are what we want.
            var cloneSubscribers = timelineClone.GetAdvanceSubscribers();
            var originalSubscribers = timeline.GetAdvanceSubscribers();
            CheckSubscribersConverted<int>(originalSubscribers, cloneSubscribers, context);

            //Now check the events
            Assert.Equal(3, timelineClone.Events.Count);
            //Do equality checks to ensure that UUIDs and types are preserved
            Assert.Equal(card1, timelineClone.Events[0]);
            Assert.Equal(card2, timelineClone.Events[1]);
            Assert.Equal(card3, timelineClone.Events[2]);
            //Check that the cards are actually different instances (cloned)
            Assert.NotSame(card1, timelineClone.Events[0]);
            Assert.NotSame(card2, timelineClone.Events[1]);
            Assert.NotSame(card3, timelineClone.Events[2]);
            //Check HaltsInstantAction (since not part of equality)
            Assert.False(timelineClone.Events[0].HaltsInstantAction);
            Assert.True(timelineClone.Events[1].HaltsInstantAction);
            Assert.False(timelineClone.Events[2].HaltsInstantAction);
            //Check TimeToEvent since that isn't part of the equality check (since it is mutable)
            //If I had more checks, I'd do a typecast after checking equality and not typecast each time.
            Assert.Equal(card1.TimeToEvent, ((BaseCard<int>)timelineClone.Events[0]).TimeToEvent);
            Assert.Equal(card2.TimeToEvent, ((BaseCard<int>)timelineClone.Events[1]).TimeToEvent);
            Assert.Equal(card3.TimeToEvent, ((BaseCard<int>)timelineClone.Events[2]).TimeToEvent);
        }
        else
        {
            Assert.Fail("Clone is not of type Timeline<int>");
        }
    }
    [Fact(DisplayName ="Complex events (Cards) clone too")]
    public void CardCloning()
    {
        Timeline<int> timeline = new();
        //Create cards, including ICombatCloneable and non-clonable (values)
        Card<int, double> valueCard = new(5, timeline) {Face = 3.0};
        Card<int, double> valuePriority = new(6, timeline, PriorityRank.ReturnAIControl);
        Card<int, double> nullFaceValue = new(7, timeline) { HaltsInstantAction=true}; //ensure lower properties are cloned
        Card<int, ClonableClass> refCard = new(2, timeline) { Face = new("hi") };
        Card<int, ClonableClass> nullRefCard = new(8, timeline,PriorityRank.InstantLoss);

        //Add all the cards
        timeline.AddEvent(valueCard);
        timeline.AddEvent(valuePriority);
        timeline.AddEvent(nullFaceValue);
        timeline.AddEvent(refCard);
        timeline.AddEvent(nullRefCard);

        //Create the clone context (we'll need it later).
        CloneContext context = new();

        //Commit the clone
        object clone = timeline.Clone(context);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            //Check the subscribers of advance to make sure they are what we want.
            var cloneSubscribers = timelineClone.GetAdvanceSubscribers();
            var originalSubscribers = timeline.GetAdvanceSubscribers();
            CheckSubscribersConverted<int>(originalSubscribers, cloneSubscribers, context);

            //Now check the events
            Assert.Equal(5, timelineClone.Events.Count);
            //First, check base equality, so we know our type casts won't explode
            Assert.Equal(valueCard, timelineClone.Events[0]);
            Assert.Equal(valuePriority, timelineClone.Events[1]);
            Assert.Equal(nullFaceValue, timelineClone.Events[2]);
            Assert.Equal(refCard, timelineClone.Events[3]);
            Assert.Equal(nullRefCard, timelineClone.Events[4]);

            //Helper function to be less repetitive.
            CheckCards(valueCard, (Card<int, double>)timelineClone.Events[0]);
            CheckCards(valuePriority, (Card<int, double>)timelineClone.Events[1]);
            CheckCards(nullFaceValue, (Card<int, double>)timelineClone.Events[2]);
            CheckCards(refCard, (Card<int, ClonableClass>)timelineClone.Events[3]); //Since this one got cloned, check it on its own
            CheckCards(nullRefCard, (Card<int, ClonableClass>)timelineClone.Events[4]);

            //Check that the one reference got cloned correctly
            Assert.True(context.TryGet(refCard.Face, out var clonedFace));
            Assert.Same(clonedFace, ((Card<int, ClonableClass>)timelineClone.Events[3]).Face);
            Assert.NotSame(refCard.Face, clonedFace);
            Assert.Equal(refCard.Face.Value, clonedFace.Value);
        }
        else
        {
            Assert.Fail("Somehow, we cloned and got not a timeline");
        }
    }
    [Fact(DisplayName ="If an event is Never and in Events, it is cloned into PossibleEvents")]
    public void NeverEventInEventsMovesToPossibleEvents()
    {
        Timeline<int> timeline = new();
        PassiveDynamicEvent<int> neverEvent = new(new TimeOrNever<int>(5), true, timeline);
        timeline.AddEvent(neverEvent);
        neverEvent.TimeRemaining = new TimeOrNever<int>(true); //if added as just never, it would be in PossibleEvents

        //Create the clone context (we'll need it later).
        CloneContext context = new();

        //Commit the clone
        object clone = timeline.Clone(context);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            //Check the subscribers of advance to make sure they are what we want.
            var cloneSubscribers = timelineClone.GetAdvanceSubscribers();
            var originalSubscribers = timeline.GetAdvanceSubscribers();
            CheckSubscribersConverted<int>(originalSubscribers, cloneSubscribers, context);

            //Now, check the event is in the right place
            Assert.Empty(timelineClone.Events);
            Assert.Single(timelineClone.PossibleEvents);
            Assert.True(context.TryGet(neverEvent, out PassiveDynamicEvent<int> clonedNever));
            Assert.Contains(clonedNever, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);

            //Check the event itself
            Assert.Equal(neverEvent, clonedNever); //Equality failure ???
            Assert.NotSame(neverEvent, clonedNever);
            Assert.Equal(neverEvent.TimeRemaining, clonedNever.TimeRemaining);
            Assert.Equal(neverEvent.CouldOccur, clonedNever.CouldOccur);
            Assert.Equal(neverEvent.HaltsInstantAction, clonedNever.HaltsInstantAction);
        }
        else
        {
            Assert.Fail("Somehow, we cloned and got not a timeline");
        }
    }
    [Fact(DisplayName = "Events in PossibleEvents remain in PossibleEvents, regardless of if they are Never")]
    public void EventsInPossibleEventsStayInPossibleEvents()
    {
        Timeline<int> timeline = new();
        PassiveDynamicEvent<int> neverEvent = new(true, timeline); //Auto adds and is never
        PassiveDynamicEvent<int> impossibleEvent = new(false, timeline);
        BaseCard<int> occurringEvent = new(5,timeline);
        timeline.AddPossibleEvent(occurringEvent); //Force adding it as possible

        //Create the clone context (we'll need it later).
        CloneContext context = new();

        //Commit the clone
        object clone = timeline.Clone(context);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            //Check the subscribers of advance to make sure they are what we want.
            var cloneSubscribers = timelineClone.GetAdvanceSubscribers();
            var originalSubscribers = timeline.GetAdvanceSubscribers();
            CheckSubscribersConverted<int>(originalSubscribers, cloneSubscribers, context);

            //Check where the events are
            Assert.Empty(timelineClone.Events);
            Assert.Equal(3, timelineClone.PossibleEvents.Count);
            Assert.True(context.TryGet(neverEvent, out PassiveDynamicEvent<int> clonedNever));
            Assert.True(context.TryGet(impossibleEvent, out PassiveDynamicEvent<int> clonedImpossible));
            Assert.True(context.TryGet(occurringEvent, out BaseCard<int> clonedOther));
            Assert.Contains(clonedNever, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);
            Assert.Contains(clonedImpossible, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);
            Assert.Contains(clonedOther, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);

            //Check contents
            Assert.Equal(neverEvent, clonedNever);
            Assert.NotSame(neverEvent, clonedNever);
            Assert.Equal(neverEvent.TimeRemaining, clonedNever.TimeRemaining);
            Assert.Equal(neverEvent.CouldOccur, clonedNever.CouldOccur);
            Assert.Equal(neverEvent.HaltsInstantAction, clonedNever.HaltsInstantAction);
            Assert.Equal(impossibleEvent, clonedImpossible);
            Assert.NotSame(impossibleEvent, clonedImpossible);
            Assert.Equal(impossibleEvent.TimeRemaining, clonedImpossible.TimeRemaining);
            Assert.Equal(impossibleEvent.CouldOccur, clonedImpossible.CouldOccur);
            Assert.Equal(impossibleEvent.HaltsInstantAction, clonedImpossible.HaltsInstantAction);
            Assert.Equal(occurringEvent, clonedOther);
            Assert.NotSame(occurringEvent, clonedOther);
            Assert.Equal(occurringEvent.TimeToEvent, clonedOther.TimeToEvent);
            Assert.Equal(occurringEvent.HaltsInstantAction, clonedOther.HaltsInstantAction);
        }
        else
        {
            Assert.Fail("Somehow, we cloned and got not a timeline");
        }
    }
    [Fact(DisplayName ="When Never events are moved to PossibleEvents, the other events remain")]
    public void RelativeOrderKept()
    {
        Timeline<int> timeline = new();

        //Build five events. We'll make indices 1 and 3 (B and D) Never after adding them.
        //Each PassiveDynamicEvent is constructed with a real time so it goes into Events.
        //The times are "out of order" to ensure no sorting happened
        PassiveDynamicEvent<int> a = new(new TimeOrNever<int>(6), true, timeline);
        PassiveDynamicEvent<int> b = new(new TimeOrNever<int>(2), true, timeline);
        PassiveDynamicEvent<int> c = new(new TimeOrNever<int>(3), true, timeline);
        PassiveDynamicEvent<int> d = new(new TimeOrNever<int>(1), true, timeline);
        PassiveDynamicEvent<int> e = new(new TimeOrNever<int>(5), true, timeline);

        //Add them in order. AddEvent makes them all in Events.
        timeline.AddEvent(a);
        timeline.AddEvent(b);
        timeline.AddEvent(c);
        timeline.AddEvent(d);
        timeline.AddEvent(e);

        // Now flip B and D to Never. They stay in Events (the original's invariant is now
        // inconsistent, but that's the state we want to test cloning against).
        b.TimeRemaining = new TimeOrNever<int>(true);
        d.TimeRemaining = new TimeOrNever<int>(true);

        //Clone and keep the context
        CloneContext context = new();
        object clone = timeline.Clone(context);
        if (clone is Timeline<int> timelineClone)
        {
            Assert.Equal(TimelinePhase.Open, timelineClone.Phase);
            Assert.Equal(TerminationType.NotTerminated, timelineClone.TerminationReason);
            //Check the subscribers of advance to make sure they are what we want.
            var cloneSubscribers = timelineClone.GetAdvanceSubscribers();
            var originalSubscribers = timeline.GetAdvanceSubscribers();
            CheckSubscribersConverted<int>(originalSubscribers, cloneSubscribers, context);

            // The clone's Events should have A, C, E in that order.
            Assert.Equal(3, timelineClone.Events.Count);
            Assert.True(context.TryGet(a, out PassiveDynamicEvent<int> clonedA));
            Assert.True(context.TryGet(c, out PassiveDynamicEvent<int> clonedC));
            Assert.True(context.TryGet(e, out PassiveDynamicEvent<int> clonedE));
            Assert.Equal(new[] { clonedA, clonedC, clonedE }, timelineClone.Events);

            // The clone's PossibleEvents should have B and D.
            Assert.Equal(2, timelineClone.PossibleEvents.Count);
            Assert.True(context.TryGet(b, out PassiveDynamicEvent<int> clonedB));
            Assert.True(context.TryGet(d, out PassiveDynamicEvent<int> clonedD));
            Assert.Contains(clonedB, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);
            Assert.Contains(clonedD, timelineClone.PossibleEvents, ReferenceEqualityComparer.Instance);

            //Assert that none of the clones are the same objects
            Assert.NotSame(a,clonedA);
            Assert.NotSame(b,clonedB);
            Assert.NotSame(c,clonedC);
            Assert.NotSame(d,clonedD);
            Assert.NotSame(e,clonedE);
        }
        else
        {
            Assert.Fail("Somehow, we cloned and got not a timeline");
        }
    }
    [Fact(DisplayName ="Ensure advancing a clone doesn't move the original")]
    public void AdvancingCloneDecrementsClonedEventTimes() //AI generated
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(5, timeline);
        timeline.AddEvent(card);

        CloneContext context = new();
        Timeline<int> cloned = (Timeline<int>)timeline.Clone(context); //Normally I'd structure this more, rather than just casting

        // Advance the clone's time by running to Zeroed, which invokes Advance.
        foreach (var _ in cloned.TakeSteps())
        {
            if (cloned.Phase == TimelinePhase.Zeroed) break;
        }

        // The clone's card should have decremented by 5 (to time 0).
        BaseCard<int> clonedCard = (BaseCard<int>)cloned.Events[0];
        Assert.Equal(0, clonedCard.TimeToEvent);

        // The original's card should be unchanged.
        Assert.Equal(5, card.TimeToEvent);
    }
    [Fact(DisplayName ="Clones of cards without their timelines fails")]
    public void CloningCardWithoutTimelineCloneFails()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(5, timeline);

        CloneContext context = new();
        // Don't clone the timeline.

        Assert.Throws<UnrecoverableCloneException>(() => card.Clone(context));
    }
    #region helpers
    /// <summary>
    /// A class that is possible to clone that is as simple as possible
    /// </summary>
    /// <param name="startingValue"></param>
    private class ClonableClass(string startingValue) : ICombatCloneable
    {
        public string Value { get; set; } = startingValue;
        public object Clone(CloneContext context)
        {
            if (context.TryGet(this,out ClonableClass existingClone))
            {
                return existingClone;
            }
            ClonableClass clone = new(Value);
            context.Register(this, clone);
            return clone;
        }
    }
    /// <summary>
    /// Ensures that a cloned card has all the mutable properties of the orignal *
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="F"></typeparam>
    /// <param name="original"></param>
    /// <param name="clone"></param>
    private static void CheckCards<T,F>(Card<T,F> original,Card<T,F> clone) where T : INumber<T>
    {
        //Check that it isn't the same so we know it is cloned
        Assert.NotSame(original, clone);
        //Normal mutable properties
        Assert.Equal(original.TimeToEvent, clone.TimeToEvent);
        Assert.Equal(original.Priority, clone.Priority);

        //If we can, check the face
        if (original.Face is null)
        {
            Assert.Null(clone.Face);
        }
        else if (original.Face is not ICombatCloneable)
        {
            Assert.Equal(original.Face, clone.Face);
        } //If it is combat cloneable and not null, test it on your own
    }
    /// <summary>
    /// Ensure that the subscribers to a timeline are cloned and only the clones are linked.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="original"></param>
    /// <param name="cloned"></param>
    /// <param name="context"></param>
    private static void CheckSubscribersConverted<T>(IReadOnlyList<IAdvanceable<T>> original,
                        IReadOnlyList<IAdvanceable<T>> cloned,
                        CloneContext context) where T : INumber<T>
    {
        Assert.Equal(original.Count, cloned.Count);
        //Check with reference equality since the clone/original might be the same under their original equality.
        HashSet<IAdvanceable<T>> clonedContent = new(ReferenceEqualityComparer.Instance);
        foreach (var origSub in original)
        {
            Assert.NotNull(origSub);
            Assert.True(context.TryGet(origSub, out IAdvanceable<T> clone), $"Original has {origSub} but it was not cloned.");
            clonedContent.Add(clone);
        }
        //Ensure every item in the cloned list is a clone
        foreach (var item in cloned)
        {
            Assert.Contains(item, clonedContent);
        }
        //Ensure every item in the original list isn't a clone.
        foreach (var item in original)
        {
            Assert.DoesNotContain(item, clonedContent);
        }
    }
    #endregion
    #region Helper tests (AI originally designed, hence why they look different)
    [Fact(DisplayName = "CheckSubscribersConverted fails when counts differ")]
    public void FailsWhenCountsDiffer()
    {
        var original = new Timeline<int>();
        var card = new BaseCard<int>(5, original);
        original.AddEvent(card);

        var context = new CloneContext();
        var cloned = (Timeline<int>)original.Clone(context);

        var originalSubs = original.GetAdvanceSubscribers();
        var clonedSubs = cloned.GetAdvanceSubscribers().ToList();
        clonedSubs.Add(clonedSubs[0]); // duplicate, so counts differ

        Assert.Throws<Xunit.Sdk.EqualException>(() =>
            CheckSubscribersConverted(originalSubs, clonedSubs, context));
    }
    [Fact(DisplayName = "CheckSubscribersConverted fails when an timeline has no clone in context")]
    public void FailsWhenOriginalHasNoClone()
    {
        var original = new Timeline<int>();
        var card = new BaseCard<int>(5, original);
        original.AddEvent(card);

        var context = new CloneContext();
        var cloned = (Timeline<int>)original.Clone(context);

        var originalSubs = original.GetAdvanceSubscribers();
        var clonedSubs = cloned.GetAdvanceSubscribers();

        // Create a fresh context that doesn't know about the clone.
        var emptyContext = new CloneContext();

        Assert.Throws<Xunit.Sdk.TrueException>(() =>
            CheckSubscribersConverted(originalSubs, clonedSubs, emptyContext));
    }
    [Fact(DisplayName = "CheckSubscribersConverted fails when cloned list has an uncloned subscriber")]
    public void FailsWhenClonedListHasForeignSubscriber()
    {
        var original = new Timeline<int>();
        var card = new BaseCard<int>(5, original);
        original.AddEvent(card);

        var context = new CloneContext();
        var cloned = (Timeline<int>)original.Clone(context);

        var originalSubs = original.GetAdvanceSubscribers();
        var clonedSubs = cloned.GetAdvanceSubscribers().ToList();

        // Inject a subscriber that isn't a clone of anything in the original list.
        var foreignTimeline = new Timeline<int>();
        var foreignCard = new BaseCard<int>(1, foreignTimeline);
        clonedSubs.Add(foreignCard);

        Assert.Throws<Xunit.Sdk.EqualException>(() =>
            CheckSubscribersConverted(originalSubs, clonedSubs, context));
    }
    [Fact(DisplayName = "CheckSubscribersConverted fails when an timeline appears in the cloned list")]
    public void FailsWhenOriginalAppearsInClonedList()
    {
        var original = new Timeline<int>();
        var card = new BaseCard<int>(5, original);
        original.AddEvent(card);

        var context = new CloneContext();
        var cloned = (Timeline<int>)original.Clone(context);

        var originalSubs = original.GetAdvanceSubscribers();
        var clonedSubs = cloned.GetAdvanceSubscribers().ToList();

        // Replace the cloned subscriber with the original. Same count, but wrong identity.
        clonedSubs[0] = originalSubs[0];

        Assert.Throws<Xunit.Sdk.ContainsException>(() =>
            CheckSubscribersConverted(originalSubs, clonedSubs, context));
    }
    [Fact(DisplayName = "CheckCards fails if the two cards are the same reference")]
    public void FailsIfSameReference()
    {
        var timeline = new Timeline<int>();
        var card = new Card<int, double>(5, timeline) { Face = 3.0 };

        Assert.Throws<Xunit.Sdk.NotSameException>(() => CheckCards(card, card));
    }
    [Fact(DisplayName = "CheckCards fails if TimeToEvent differs")]
    public void FailsIfTimeToEventDiffers()
    {
        var timeline = new Timeline<int>();
        var card = new Card<int, double>(5, timeline) { Face = 3.0 };
        var clonedCard = new Card<int, double>(6, new Timeline<int>()) { Face = 3.0 };

        Assert.Throws<Xunit.Sdk.EqualException>(() => CheckCards(card, clonedCard));
    }

    [Fact(DisplayName = "CheckCards fails if Priority differs")]
    public void FailsIfPriorityDiffers()
    {
        var timeline = new Timeline<int>();
        var card = new Card<int, double>(5, timeline, PriorityRank.None) { Face = 3.0 };
        var clonedCard = new Card<int, double>(5, new Timeline<int>(), PriorityRank.ReturnAIControl) { Face = 3.0 };

        Assert.Throws<Xunit.Sdk.EqualException>(() => CheckCards(card, clonedCard));
    }

    [Fact(DisplayName = "CheckCards fails if Face differs")]
    public void FailsIfFaceDiffers()
    {
        var timeline = new Timeline<int>();
        var card = new Card<int, double>(5, timeline) { Face = 3.0 };
        var clonedCard = new Card<int, double>(5, new Timeline<int>()) { Face = 4.0 };

        Assert.Throws<Xunit.Sdk.EqualException>(() => CheckCards(card, clonedCard));
    }

    //Human test(s). You can tell because I prefer type name =new instead of var name = new type
    [Fact(DisplayName ="If the face is ICombatCloneable, it doesn't check the faces")]
    public void FaceDifferenceOfClonableFaceIgnored()
    {
        Timeline<int> timeline = new();
        Card<int, ClonableClass> card = new(5, timeline) { Face = new("hi") };
        Card<int, ClonableClass> cloneCard = new(5, new Timeline<int>()) { Face = new("bye")};

        CheckCards(card, cloneCard); //Shouldn't throw
    }
    #endregion
}