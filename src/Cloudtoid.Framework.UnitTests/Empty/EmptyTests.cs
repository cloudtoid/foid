﻿namespace Cloudtoid.Framework.UnitTests
{
    using Cloudtoid.Framework;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EmptyTests
    {
        [TestMethod]
        public void SingleValueList_Smoke()
        {
            var list = List.Empty<string>();
            list.Should().BeEmpty();
            list.Should().BeOfType(typeof(string[]));
        }

        [TestMethod]
        public void SingleValueList_SameType()
        {
            var list1 = List.Empty<string>();
            var list2 = List.Empty<string>();
            list1.Should().BeSameAs(list2);
        }

        [TestMethod]
        public void SingleValueSet_Smoke()
        {
            var set = Set.Empty<string>();
            set.Should().BeEmpty();
            set.IsReadOnly.Should().BeTrue();
        }

        [TestMethod]
        public void SingleValueSet_SameType()
        {
            var set1 = List.Empty<string>();
            var set2 = List.Empty<string>();
            set1.Should().BeSameAs(set2);
        }
    }
}
