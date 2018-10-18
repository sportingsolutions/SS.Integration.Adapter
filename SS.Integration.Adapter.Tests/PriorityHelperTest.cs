using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Interface;
using Moq;
using SS.Integration.Adapter.Helpers;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Tests
{
	[TestFixture]
	public class PriorityHelperTest
	{

		[SetUp]
		public void SetupTest()
		{
			
		}

		[Test]
		public void SortByMatchStatusTest()
		{
			var cd = DateTime.Now;

			var r1 = GetResource(20, cd.AddHours(0));
			var r2 = GetResource(30, cd.AddHours(-2));
			var r3 = GetResource(40, cd.AddHours(-2));
			var r4 = GetResource(50, cd.AddHours(0));
			var r5 = GetResource(10, cd.AddHours(-2));
			var r6 = GetResource(40, cd.AddHours(0));
			var r7 = GetResource(30, cd.AddHours(0));
			var r8 = GetResource(40, cd.AddHours(2));


			var testList = new List<IResourceFacade>()
			{
				r8, r7, r6, r5, r4, r3, r2, r1
			};

			testList.SortByMatchStatus();


			Assert.AreEqual(r3, testList[0]);
			Assert.AreEqual(r6, testList[1]);
			Assert.AreEqual(r8, testList[2]);
			Assert.AreEqual(r2, testList[3]);
			Assert.AreEqual(r7, testList[4]);
			Assert.AreEqual(r5, testList[5]);
			Assert.AreEqual(r1, testList[6]);
			Assert.AreEqual(r4, testList[7]);
		}

		[Test]
		// First shoul be Todas matches order by start time the other order by start time
		public void SortByTimeTest()
		{
			var cd = DateTime.Now;

			var r1 = GetResource(40, cd.AddHours(2));
			var r2 = GetResource(40, cd.AddHours(0));
			var r3 = GetResource(40, cd.AddHours(-2));
			var r4 = GetResource(40, cd.AddDays(-1));


			var testList = new List<IResourceFacade>()
			{
				r4, r3, r2, r1
			};

			testList.SortByMatchStatus();
			
			Assert.AreEqual(r1, testList[2]);
			Assert.AreEqual(r2, testList[1]);
			Assert.AreEqual(r3, testList[0]);
			Assert.AreEqual(r4, testList[3]);
		}

		private IResourceFacade GetResource(int status, DateTime date)
		{
			var r1 = new Mock<IResourceFacade>();
			var c1 = new Summary()
			{
				MatchStatus = status,
				StartTime = date.ToString()
			};
			r1.Setup(p => p.Content).Returns(c1);
			return r1.Object;
		}
	}
}
