using CoreMetrics;
using System.Numerics;
using Timeline;
using Timeline.OkazoOptionalProperties;

namespace Timeline_Test
{
    public class IOkazoComparisonTests
    {
        [Fact(DisplayName ="Earlier events sort first")]
        public void Test1()
        {
            Timeline<int> timeline = new();
            BaseCard<int> alpha = new(0,timeline);
            BaseCard<int> beta = new(1, timeline);
            alpha.UUID = 0;
            beta.UUID = 0; //Should never happen, but make sure we compare times first.
            Assert.Equal(-1, alpha.CompareTo(beta));
            Assert.Equal(1, beta.CompareTo(alpha));
        }
        [Fact(DisplayName = "If nothing else can be used, compare UUIDs")]
        public void Test2()
        {
            Timeline<int> timeline = new();
            BaseCard<int> alpha = new(0, timeline);
            BaseCard<int> beta = new(0, timeline);
            alpha.UUID = 1;
            beta.UUID = 2;
            Assert.Equal(-1, alpha.CompareTo(beta));
            Assert.Equal(1, beta.CompareTo(alpha));
        }
        [Fact(DisplayName = "If two different kinds of event have the same time and UUID, an ArgumentException is raised")]
        public void Test3()
        {
            Timeline<int> timeline = new();
            BaseCard<int> alpha = new(0, timeline);
            FakeBaseCardSubclass<int> beta = new(0, timeline); //We don't care about the face, since they are different types.
            alpha.UUID = 0;
            beta.UUID = 0;
            Assert.Throws<ArgumentException>(() => alpha.CompareTo(beta));
        }
        /// <summary>
        /// Different subclass of BaseCard<T> to test that two cards of different types with the same time and UUID raise an ArgumentException when compared.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class FakeBaseCardSubclass<T>(T timeToEvent, Timeline<T> owningTimeline) : BaseCard<T>(timeToEvent, owningTimeline) where T : INumber<T>
        {
        }
        [Fact(DisplayName = "Earlier events ignore any other properties")]
        public void Test4()
        {
            Timeline<int> timeline = new();
            Card<int,int> alpha = new(0, timeline);
            Card<int,int> beta = new(1, timeline);
            //Add all the other properties so if they were checked first, the test would fail.
            alpha.Priority = PrioityRank.DebugHigh;
            beta.Priority = PrioityRank.ReturnPlayerControl; //DebugLow might be removed in the future, so use a priority that won't be removed.
            //Beta not having a face should make it sort first if Alpha has a face
            alpha.Face = 1;
            alpha.UUID = 1;
            beta.UUID = 0;

            Assert.Equal(-1, alpha.CompareTo(beta));
            Assert.Equal(1, beta.CompareTo(alpha));
        }
        [Fact(DisplayName ="If the other event is null,ArgumentNullException is raised")]
        public void Test5()
        {
            Timeline<int> timeline = new();
            BaseCard<int> alpha = new(0, timeline);
            Assert.Throws<ArgumentNullException>(() => alpha.CompareTo(null));
        }
        [Fact(DisplayName = "If both events are Never, comparison throws NeverIsNotATimeException")]
        public void Test6()
        {
            Timeline<int> timeline = new();
            TimeOrNever<int> never = new()
            {
                IsNever = true
            };
            PassiveDynamicEvent<int> alpha = new(never,true, timeline);
            PassiveDynamicEvent<int> beta = new(never, false, timeline);
            Assert.Throws<NeverIsNotATimeException>(() => alpha.CompareTo(beta));
        }
        [Fact(DisplayName = "If one event is Never, it sorts after any valid event, with ±10")]
        public void Test7()
        {
            Timeline<int> timeline = new();
            TimeOrNever<int> never = new()
            {
                IsNever = true
            };
            PassiveDynamicEvent<int> alpha = new(never, true, timeline);
            BaseCard<int> beta = new(0, timeline);
            Assert.Equal(-10, beta.CompareTo(alpha));
            Assert.Equal(10, alpha.CompareTo(beta));
        }
        [Fact(DisplayName = "If two events are equal, comparing them leads to 0")]
        public void Test8()
        {
            Timeline<int> timeline = new();
            BaseCard<int> alpha = new(0, timeline);
            BaseCard<int> beta = new(0, timeline);
            alpha.UUID = 1;
            beta.UUID = 1;
            Assert.Equal(0, alpha.CompareTo(beta));
            Assert.Equal(0, beta.CompareTo(alpha));
        }
        [Fact(DisplayName = "Second check after time is priority")] 
        public void Test9()
        {
            Timeline<int> timeline = new();
            Card<int,int> alpha = new(0, timeline);
            Card<int,int> beta = new(0, timeline);
            alpha.Priority = PrioityRank.DebugHigh;
            beta.Priority = PrioityRank.ReturnPlayerControl; //DebugLow might be removed in the future, so use a priority that won't be removed.
            alpha.Face = 1; //Add a face to make sure that the priority is checked before the face.
            alpha.UUID = 1;
            beta.UUID = 0;
            Assert.Equal(-1, alpha.CompareTo(beta));
            Assert.Equal(1, beta.CompareTo(alpha));
        }
        //TransitionData comparison will be in its own test class, since no Okazo<T> implementation currently implements it and it is a bit more complex than the other comparisons.
        //Add other tests for the other optional properties as they are added
        [Fact(DisplayName = "Second to last, use object specific tiebreakers")]
        public void Test10() { 
            Timeline<int> timeline = new();
            Card<int,int> alpha = new(0, timeline);
            Card<int,int> beta = new(0, timeline);
            //Set UUIDs so if the tiebreaker is not used, the test will fail.
            alpha.UUID = 1;
            beta.UUID = 0;
            //Card has a simple tiebreaker based on the Face property, so set them to sort alpha first.
            //Unit testing for the tiebreaker for card would be elsewhere if needed.
            alpha.Face = 0;
            beta.Face = 1;
            Assert.Equal(-1, alpha.CompareTo(beta));
            Assert.Equal(1, beta.CompareTo(alpha));
        }

    }
}