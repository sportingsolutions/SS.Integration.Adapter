using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class MarketStateTest
    {

        MarketState state = new MarketState();
        

        public MarketStateTest()
        {
        


        }
        //new List<Mock<IUpdatableSelectionState>>(){ _sel1ActiveWithPrice0, _sel1SettledWithPrice0 , _sel1ActiveWithPrice1 }


        [Test,TestCaseSource(typeof(SelectionStateFactory), "TestCases")]
        public bool IsResultedTest_OneMarketNotSettled(List<IUpdatableSelectionState> selections/*, bool expected*/)
        {
            state._selectionStates.Clear();
            foreach (var selection in selections)
            {
                state._selectionStates.Add(selection.GetHashCode().ToString(), selection);
            }

            return state.IsResulted;
            //state.IsResulted.Should().Be(expected);
        }
    }



    public class SelectionStateFactory
    {
        static Mock<IUpdatableSelectionState> _settled1Price1 = new Mock<IUpdatableSelectionState>();
        static Mock<IUpdatableSelectionState> _settled1Price0 = new Mock<IUpdatableSelectionState>();
        static Mock<IUpdatableSelectionState> _settled2Price0 = new Mock<IUpdatableSelectionState>();
        static Mock<IUpdatableSelectionState> _settled3Price0 = new Mock<IUpdatableSelectionState>();
        static Mock<IUpdatableSelectionState> _active1Price1 = new Mock<IUpdatableSelectionState>();
        static Mock<IUpdatableSelectionState> _active1Price0 = new Mock<IUpdatableSelectionState>();

        static SelectionStateFactory()
        {
            _settled1Price1.SetupGet(p => p.Status).Returns(SelectionStatus.Settled);
            _settled1Price1.SetupGet(p => p.Price).Returns(1.0);

            _settled1Price0.SetupGet(p => p.Status).Returns(SelectionStatus.Settled);
            _settled1Price0.SetupGet(p => p.Price).Returns(0.0);

            _settled2Price0.SetupGet(p => p.Status).Returns(SelectionStatus.Settled);
            _settled2Price0.SetupGet(p => p.Price).Returns(0.0);

            _settled3Price0.SetupGet(p => p.Status).Returns(SelectionStatus.Settled);
            _settled3Price0.SetupGet(p => p.Price).Returns(0.0);

            _active1Price1.SetupGet(p => p.Status).Returns(SelectionStatus.Active);
            _active1Price1.SetupGet(p => p.Price).Returns(1.0);

            _active1Price0.SetupGet(p => p.Status).Returns(SelectionStatus.Active);
            _active1Price0.SetupGet(p => p.Price).Returns(0.0);
        }

        public static IEnumerable TestCases
        {
            get
            {
                // 2 settled 1 active
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _active1Price0.Object, _settled2Price0.Object, _active1Price1.Object }).Returns(false);
                // 3 settled 1 with price 1
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _settled1Price0.Object, _settled2Price0.Object, _settled1Price1.Object }).Returns(true);
                // 3 settled 1 with price 1
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _settled1Price0.Object, _settled2Price0.Object, _settled3Price0.Object }).Returns(false);
                // 3 settled 1 with price 1
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _settled1Price1.Object}).Returns(true);
                // 3 settled 1 with price 1
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _settled1Price0.Object}).Returns(true);
                // 3 settled 1 with price 1
                yield return new TestCaseData(new List<IUpdatableSelectionState>() { _active1Price1.Object}).Returns(false);

            }
        }
    }
}
