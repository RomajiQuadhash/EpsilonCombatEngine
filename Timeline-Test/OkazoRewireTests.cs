using CoreMetrics;
using System.Numerics;
using Timeline;
namespace Timeline_Test;

[TestClass]
public class OkazoRewireTests
{
    //This method just exists to make sure we throw if a static method is used
    private static void FakeStaticOkazoSubscriber(object? _, Okazo<int> __) { }
    [Fact(DisplayName ="If a subscriber is a static method, we throw when checking subscription or cloning")]
    public void RewireThrowsOnStaticSubscriber()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(5, timeline);
        timeline.AddEvent(card);
        card.Occurring += FakeStaticOkazoSubscriber;  // static method
        Assert.Throws<InvalidOperationException>(() => card.GetOccurringSubscribers());

        CloneContext context = new();
        timeline.Clone(context); //We don't actually need the timeline clone

        Assert.Throws<UnrecoverableCloneException>(() => card.Rewire(context));
    }
    [Fact(DisplayName ="If a subscriber doesn't have a clone, then throw")]
    public void RewireThrowsOnUnclonedSubscriber()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(5, timeline);
        timeline.AddEvent(card);

        MostRecentEventHolder<int> holder = new();
        card.Occurring += holder.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);  // clones the timeline and card, but not the holder

        Assert.Throws<UnrecoverableCloneException>(() => card.Rewire(context));
    }
    [Fact(DisplayName ="If there are no subscribers, just return.")]
    public void RewireNoSubscribers()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(5, timeline);
        timeline.AddEvent(card);

        CloneContext context = new();
        timeline.Clone(context);

        Assert.True(context.TryGet(card, out BaseCard<int> clonedCard));
        card.Rewire(context);
        Assert.Empty(clonedCard.GetOccurringSubscribers());
    }
    [Fact(DisplayName ="If there is one or more subscribers to an event, their clones are all subscribed to the clone")]
    public void RewireSubscribers()
    {
        Timeline<int> timeline = new();
        BaseCard<int> card = new(2, timeline);
        timeline.AddEvent(card);

        MostRecentEventHolder<int> holder1 = new();
        MostRecentEventHolder<int> holder2 = new();
        card.Occurring += holder1.OnTrigger;
        card.Occurring += holder2.OnTrigger;

        CloneContext context = new();
        timeline.Clone(context);
        //Clone the holders since they're not included in the timeline's cloning scope
        MostRecentEventHolder<int> cloneHolder1 = (MostRecentEventHolder<int>)holder1.Clone(context);
        MostRecentEventHolder<int> cloneHolder2 = (MostRecentEventHolder<int>)holder2.Clone(context);

        Assert.True(context.TryGet(card, out BaseCard<int> clonedCard));
        card.Rewire(context);
        var subscribers = clonedCard.GetOccurringSubscribers();
        Assert.Equal(2, subscribers.Count);
        Assert.Contains(cloneHolder1, subscribers);
        Assert.Contains(cloneHolder2, subscribers);
        // Also verify the originals aren't there.
        Assert.DoesNotContain(holder1, subscribers);
        Assert.DoesNotContain(holder2, subscribers);
    }
}
