﻿namespace Cloudtoid.Foid.UnitTests
{
    using Cloudtoid.Foid.Routes;
    using FluentAssertions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RouteNormalizerTests
    {
        private readonly IRouteNormalizer normalizer;

        public RouteNormalizerTests()
        {
            var services = new ServiceCollection().AddTest();
            var serviceProvider = services.BuildServiceProvider();
            normalizer = serviceProvider.GetRequiredService<IRouteNormalizer>();
        }

        [TestMethod]
        public void NormalizeTests()
        {
            Normalize(string.Empty, "/");
            Normalize("/", "/");
            Normalize("/ ", "/");
            Normalize(" /", "/");
            Normalize(" / ", "/");
            Normalize("/product/", "/product/");
            Normalize(" /product/ ", "/product/");
            Normalize("/product", "/product/");
            Normalize("/product/1234/", "/product/1234/");
        }

        private void Normalize(string route, string expected)
        {
            normalizer.Normalize(route).Should().Be(expected);
        }
    }
}
