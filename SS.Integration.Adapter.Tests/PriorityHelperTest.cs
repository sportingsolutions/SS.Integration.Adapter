﻿using System;
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
		public void SortByMatchDateTest()
		{
			var cd = DateTime.Now.Date.AddHours(12);
			var r1 = GetResource(40, cd.AddHours(-1));
			var r2 = GetResource(40, cd);
			var r3 = GetResource(40, cd.AddHours(1));
			var r4 = GetResource(40, cd.AddHours(-24));
			var r5 = GetResource(40, cd.AddHours(-23));
			var r6 = GetResource(40, cd.AddHours(24));
			var r7 = GetResource(40, cd.AddHours(25));

			var testList = new List<IResourceFacade>()
			{
				r7, r6, r5, r4, r3, r2, r1,
			};

			testList.SortByMatchStatus();

			Assert.AreEqual(r1, testList[0]);
			Assert.AreEqual(r2, testList[1]);
			Assert.AreEqual(r3, testList[2]);
			Assert.AreEqual(r4, testList[3]);
			Assert.AreEqual(r5, testList[4]);
			Assert.AreEqual(r6, testList[5]);
			Assert.AreEqual(r7, testList[6]);
			
		}

		[Test]
		public void CompareToByStatusAndDateInvertioTest()
		{
			var cd = DateTime.Now.Date.AddHours(12);
			var r1 = GetResource(40, cd.AddHours(-1));
			var r2 = GetResource(40, cd);
			var r3 = GetResource(40, cd.AddHours(1));
			var r4 = GetResource(40, cd.AddHours(-24));
			var r5 = GetResource(40, cd.AddHours(-23));
			var r6 = GetResource(40, cd.AddHours(24));
			var r7 = GetResource(40, cd.AddHours(25));

			var testList = new List<IResourceFacade>()
			{
				r7, r6, r5, r4, r3, r2, r1,
			};

			for (int i = 0; i < testList.Count; i++)
			{
				for (int j = i+1; j < testList.Count; j++)
				{
					Assert.AreEqual(testList[i].CompareToByStatusAndDate(testList[j]), -testList[j].CompareToByStatusAndDate(testList[i]));
				}
			}

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
