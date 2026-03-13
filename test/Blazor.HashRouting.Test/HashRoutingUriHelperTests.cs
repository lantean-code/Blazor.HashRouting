using AwesomeAssertions;

namespace Blazor.HashRouting.Test
{
    public sealed class HashRoutingUriHelperTests
    {
        [Theory]
        [InlineData(null, "/")]
        [InlineData("", "/")]
        [InlineData("/", "/")]
        [InlineData("route", "/route/")]
        [InlineData("/route", "/route/")]
        [InlineData("/route/", "/route/")]
        public void GIVEN_HashPrefix_WHEN_Normalized_THEN_ReturnsExpectedPrefix(string? value, string expected)
        {
            var result = HashRoutingUriHelper.NormalizeHashPrefix(value);

            result.Should().Be(expected);
        }

        [Fact]
        public void GIVEN_HashRouteAbsoluteUri_WHEN_ConvertedToPathAbsoluteUri_THEN_RouteMovesToPath()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/#/settings?tab=General", "http://localhost/", "/");

            result.Should().Be("http://localhost/settings?tab=General");
        }

        [Fact]
        public void GIVEN_HashRouteAbsoluteUriUnderBasePath_WHEN_ConvertedToPathAbsoluteUri_THEN_RouteMovesToPathWithinBase()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/proxy/app/#/settings?tab=General", "http://localhost/proxy/app/", "/");

            result.Should().Be("http://localhost/proxy/app/settings?tab=General");
        }

        [Fact]
        public void GIVEN_LegacyFragmentAbsoluteUri_WHEN_ConvertedToPathAbsoluteUri_THEN_PathAndQueryRemainUnchanged()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/?download=http://example.com/file.torrent#download=legacy", "http://localhost/", "/");

            result.Should().Be("http://localhost/?download=http://example.com/file.torrent");
        }

        [Fact]
        public void GIVEN_ExternalAbsoluteUriWithoutHashRoute_WHEN_ConvertedToPathAbsoluteUri_THEN_OriginalHostIsPreserved()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("https://example.com/path?query=value#fragment", "http://localhost/", "/");

            result.Should().Be("https://example.com/path?query=value");
        }

        [Fact]
        public void GIVEN_PathAbsoluteUri_WHEN_ConvertedToHashAbsoluteUri_THEN_PathMovesToHashFragment()
        {
            var result = HashRoutingUriHelper.ToHashAbsoluteUri("http://localhost/details/ABC?tab=Peers", "http://localhost/", "/");

            result.Should().Be("http://localhost/#/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_PathAbsoluteUriUnderBasePath_WHEN_ConvertedToHashAbsoluteUri_THEN_BasePathIsNotDuplicatedInHash()
        {
            var result = HashRoutingUriHelper.ToHashAbsoluteUri("http://localhost/proxy/app/details/ABC?tab=Peers", "http://localhost/proxy/app/", "/");

            result.Should().Be("http://localhost/proxy/app/#/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_RootPathAbsoluteUri_WHEN_ConvertedToHashAbsoluteUri_THEN_UsesRootHashRoute()
        {
            var result = HashRoutingUriHelper.ToHashAbsoluteUri("http://localhost/", "http://localhost/", "/");

            result.Should().Be("http://localhost/#/");
        }

        [Fact]
        public void GIVEN_BasePathRootAbsoluteUri_WHEN_ConvertedToHashAbsoluteUri_THEN_UsesRootHashRouteWithinBasePath()
        {
            var result = HashRoutingUriHelper.ToHashAbsoluteUri("http://localhost/proxy/app/", "http://localhost/proxy/app/", "/");

            result.Should().Be("http://localhost/proxy/app/#/");
        }

        [Fact]
        public void GIVEN_CustomHashPrefix_WHEN_ConvertedToHashAbsoluteUri_THEN_PrefixSeparatesFromRoute()
        {
            var result = HashRoutingUriHelper.ToHashAbsoluteUri("http://localhost/details/ABC?tab=Peers", "http://localhost/", "route");

            result.Should().Be("http://localhost/#/route/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_CustomHashPrefixAbsoluteUri_WHEN_ConvertedToPathAbsoluteUri_THEN_RouteMovesToPath()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/#/route/details/ABC?tab=Peers", "http://localhost/", "route");

            result.Should().Be("http://localhost/details/ABC?tab=Peers");
        }

        [Fact]
        public void GIVEN_HashPrefixOnlyAbsoluteUri_WHEN_ConvertedToPathAbsoluteUri_THEN_UsesRootPath()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/#/route/", "http://localhost/", "route");

            result.Should().Be("http://localhost/");
        }

        [Fact]
        public void GIVEN_DoubleSlashRouteInHash_WHEN_ConvertedToPathAbsoluteUri_THEN_PreservesRoutePath()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/#/route//settings?tab=General", "http://localhost/", "route");

            result.Should().Be("http://localhost/settings?tab=General");
        }

        [Fact]
        public void GIVEN_BaseUriWithoutTrailingSlash_WHEN_ConvertedToPathAbsoluteUri_THEN_AppendsBasePathSeparator()
        {
            var result = HashRoutingUriHelper.ToPathAbsoluteUri("http://localhost/proxy/app#/settings", "http://localhost/proxy/app", "/");

            result.Should().Be("http://localhost/proxy/app/settings");
        }
    }
}
